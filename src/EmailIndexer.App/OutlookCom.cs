using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using EmailIndexer.Core.Mail;
using EmailIndexer.Core.Outlook;
using EmailIndexer.Core.Text;

namespace EmailIndexer.App
{
    /// <summary>
    /// 실행 중인 Outlook(classic)에 COM으로 연결 (Outlook 버전별 참조 DLL 없이 late binding).
    /// 반드시 STA 스레드에서 호출한다.
    /// </summary>
    internal static class OutlookCom
    {
        public enum ConnectState { Ok, NotInstalled, NotRunning, NewOutlookOnly, PermissionMismatch, Failed }

        private const int olFolderSentMail = 5;
        private const int olFolderInbox = 6;
        private const int olMSGUnicode = 9;
        internal const string PrInternetMessageId = "http://schemas.microsoft.com/mapi/proptag/0x1035001F";

        /// <summary>
        /// Outlook(classic)에 연결. 꺼져 있으면 자동으로 실행한 뒤 준비될 때까지 기다린다(최대 2분).
        /// </summary>
        /// <param name="status">진행 문구 표시 (작업 스레드에서 호출됨)</param>
        public static (ConnectState state, object? app, string message) Connect(Action<string>? status = null, CancellationToken ct = default)
        {
            bool classic = Process.GetProcessesByName("OUTLOOK").Any();
            bool newOutlook = Process.GetProcessesByName("olk").Any();
            var type = Type.GetTypeFromProgID("Outlook.Application");
            if (type == null)
                return (ConnectState.NotInstalled, null, newOutlook
                    ? L.T("outlook.com.newOnly")
                    : L.T("outlook.com.notInstalled"));
            if (!classic)
                return LaunchAndConnect(type, newOutlook, status, ct);
            try
            {
                var app = Marshal.GetActiveObject("Outlook.Application");
                return (ConnectState.Ok, app, "");
            }
            catch (COMException ex) when ((uint)ex.HResult == 0x800401E3) // MK_E_UNAVAILABLE
            {
                return (ConnectState.PermissionMismatch, null, PermissionMessage());
            }
            catch (Exception ex)
            {
                return (ConnectState.Failed, null, L.F("outlook.com.connectFailed", ex.Message));
            }
        }

        private static string PermissionMessage()
        {
            var who = IsAdmin() ? L.T("outlook.com.permByApp") : L.T("outlook.com.permByOutlook");
            return L.F("outlook.com.permMismatch", who);
        }

        /// <summary>
        /// outlook.exe(classic)를 실행하고 COM으로 붙을 수 있을 때까지 1초 간격으로 확인.
        /// Outlook은 시작 직후 바로 연결되지 않고(프로필 로드·실행 개체 등록), 프로필 선택 창이 뜰 수도 있다.
        /// </summary>
        private static (ConnectState, object?, string) LaunchAndConnect(Type type, bool newOutlookRunning, Action<string>? status, CancellationToken ct)
        {
            status?.Invoke(L.T("outlook.com.launching"));
            try
            {
                // App Paths에 등록된 classic Outlook 실행 (설치 경로를 몰라도 됨)
                Process.Start(new ProcessStartInfo("outlook.exe") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                return (ConnectState.Failed, null, L.F("outlook.com.launchFailed", ex.Message));
            }

            var sw = Stopwatch.StartNew();
            TimeSpan? seenAt = null;
            while (sw.Elapsed < TimeSpan.FromMinutes(2))
            {
                if (ct.WaitHandle.WaitOne(1000))
                    return (ConnectState.Failed, null, L.T("outlook.com.waitCancelled"));

                bool running = Process.GetProcessesByName("OUTLOOK").Any();
                if (!running)
                {
                    // '새 Outlook' 전환 스위치가 켜져 있으면 classic 대신 새 Outlook이 열린다
                    if (sw.Elapsed > TimeSpan.FromSeconds(20) && Process.GetProcessesByName("olk").Any())
                        return (ConnectState.NewOutlookOnly, null,
                            L.T("outlook.com.newOpened"));
                    continue;
                }
                seenAt ??= sw.Elapsed;
                status?.Invoke(L.F("outlook.com.waiting", (int)sw.Elapsed.TotalSeconds));

                try { return (ConnectState.Ok, Marshal.GetActiveObject("Outlook.Application"), ""); }
                catch (COMException) { /* 아직 등록 전 */ }
                catch (Exception ex) { return (ConnectState.Failed, null, L.F("outlook.com.connectFailed", ex.Message)); }

                // 실행 개체 등록이 늦는 경우: Outlook은 단일 인스턴스라 CreateInstance가 실행 중인 Outlook을 돌려준다
                if (sw.Elapsed - seenAt.Value > TimeSpan.FromSeconds(8))
                {
                    try { return (ConnectState.Ok, Activator.CreateInstance(type), ""); }
                    catch (COMException ex) when ((uint)ex.HResult == 0x80080005) // CO_E_SERVER_EXEC_FAILURE
                    {
                        return (ConnectState.PermissionMismatch, null, PermissionMessage());
                    }
                    catch (COMException) { /* 아직 준비 중 */ }
                }
            }
            return (ConnectState.Failed, null,
                L.T("outlook.com.timeout") +
                (newOutlookRunning ? L.T("outlook.com.timeoutNewNote") : ""));
        }

        private static bool IsAdmin()
        {
            try { using var id = WindowsIdentity.GetCurrent(); return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator); }
            catch { return false; }
        }

        /// <summary>계정별 받은편지함·보낸편지함 (같은 폴더 중복 제거) + 계정 SMTP 주소.</summary>
        public static (List<IOutlookFolder> folders, List<string> addresses) GetFolders(object app)
        {
            dynamic ns = Retry(() => ((dynamic)app).GetNamespace("MAPI"));
            var folders = new List<IOutlookFolder>();
            var addresses = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void Add(dynamic folder, string account, MailDirection dir)
            {
                if (folder == null) return;
                string id;
                try { id = (string)folder.StoreID + "|" + (string)folder.EntryID; } catch { id = Guid.NewGuid().ToString(); }
                if (!seen.Add(id)) { Release(folder); return; }
                folders.Add(new ComFolder(folder, account, dir));
            }

            try
            {
                dynamic accounts = ns.Accounts;
                int n = accounts.Count;
                for (int i = 1; i <= n; i++)
                {
                    dynamic acc = accounts.Item(i);
                    string smtp = "";
                    try { smtp = (string)acc.SmtpAddress ?? ""; } catch { }
                    if (string.IsNullOrEmpty(smtp)) try { smtp = (string)acc.DisplayName ?? ""; } catch { }
                    if (smtp.Contains("@")) addresses.Add(smtp);
                    dynamic? store = null;
                    try { store = acc.DeliveryStore; } catch { }
                    if (store != null)
                    {
                        try { Add(store.GetDefaultFolder(olFolderInbox), smtp, MailDirection.Received); } catch { }
                        try { Add(store.GetDefaultFolder(olFolderSentMail), smtp, MailDirection.Sent); } catch { }
                    }
                    Release(acc);
                }
            }
            catch { /* 계정 정보를 못 읽으면 기본 폴더로 */ }

            if (folders.Count == 0)
            {
                Add(Retry(() => ns.GetDefaultFolder(olFolderInbox)), L.T("outlook.com.defaultAccount"), MailDirection.Received);
                Add(Retry(() => ns.GetDefaultFolder(olFolderSentMail)), L.T("outlook.com.defaultAccount"), MailDirection.Sent);
            }
            return (folders, addresses);
        }

        /// <summary>Outlook이 바쁠 때(대화상자 표시 중 등) COM 호출이 거절되면 잠시 후 재시도.</summary>
        internal static T Retry<T>(Func<T> f)
        {
            for (int attempt = 0; ; attempt++)
            {
                try { return f(); }
                catch (COMException ex) when (attempt < 8 && ((uint)ex.HResult == 0x80010001 || (uint)ex.HResult == 0x8001010A))
                {
                    Thread.Sleep(400); // RPC_E_CALL_REJECTED / RPC_E_SERVERCALL_RETRYLATER
                }
            }
        }

        internal static void Release(object? o)
        {
            try { if (o != null && Marshal.IsComObject(o)) Marshal.ReleaseComObject(o); } catch { }
        }

        private sealed class ComFolder : IOutlookFolder, IDisposable
        {
            public void Dispose() => Release((object)_folder);

            private readonly dynamic _folder;
            public string DisplayName { get; }
            public string Key { get; }
            public MailDirection Direction { get; }

            public ComFolder(dynamic folder, string account, MailDirection dir)
            {
                _folder = folder;
                Direction = dir;
                var kind = dir == MailDirection.Sent ? L.T("outlook.folder.sent") : L.T("outlook.folder.inbox");
                DisplayName = $"{account} / {kind}";
                Key = $"{account}|{(dir == MailDirection.Sent ? "sent" : "inbox")}".ToLowerInvariant();
            }

            public int Count
            {
                get
                {
                    try { dynamic items = _folder.Items; int c = items.Count; Release(items); return c; }
                    catch { return 0; }
                }
            }

            public IEnumerable<IOutlookItem> ItemsNewestFirst()
            {
                dynamic items = Retry(() => _folder.Items);
                try
                {
                    Retry(() => { items.Sort(Direction == MailDirection.Sent ? "[SentOn]" : "[ReceivedTime]", true); return 0; });
                    // GetFirst/GetNext: 항목을 하나씩만 붙잡아 Exchange '열린 항목 수' 한도를 넘지 않음
                    object? cur = Retry(() => (object?)items.GetFirst());
                    while (cur != null)
                    {
                        yield return new ComItem(cur, Direction);
                        cur = Retry(() => (object?)items.GetNext());
                    }
                }
                finally { Release(items); }
            }
        }

        private sealed class ComItem : IOutlookItem
        {
            private object? _obj;
            private readonly MailDirection _dir;
            private dynamic D => _obj!;

            public ComItem(object obj, MailDirection dir) { _obj = obj; _dir = dir; }

            public string EntryId => Retry(() => (string)D.EntryID) ?? "";
            public string Subject { get { try { return (string)D.Subject ?? ""; } catch { return ""; } } }

            public string MessageId
            {
                get
                {
                    dynamic? pa = null;
                    try { pa = D.PropertyAccessor; return (string)pa.GetProperty(PrInternetMessageId) ?? ""; }
                    catch { return ""; }
                    finally { Release(pa); }
                }
            }

            public DateTime SortTime
            {
                get
                {
                    DateTime t;
                    try { t = _dir == MailDirection.Sent ? (DateTime)D.SentOn : (DateTime)D.ReceivedTime; }
                    catch
                    {
                        try { t = (DateTime)D.CreationTime; } catch { return DateTime.MinValue; }
                    }
                    return t.Year > 1980 && t.Year < 4000 ? t : DateTime.MinValue; // 4501-01-01 = 값 없음
                }
            }

            public void SaveAsMsg(string path) => Retry(() => { D.SaveAs(path, olMSGUnicode); return 0; });

            public void Dispose()
            {
                Release(_obj);
                _obj = null;
            }
        }
    }
}
