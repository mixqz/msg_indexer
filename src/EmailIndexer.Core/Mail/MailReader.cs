using System;
using System.Collections.Generic;
using System.IO;

namespace EmailIndexer.Core.Mail
{
    /// <summary>확장자에 따라 msg/eml 파서를 고르는 진입점. 예외를 밖으로 던지지 않는다.</summary>
    public static class MailReader
    {
        public static bool IsMailFile(string path)
        {
            var ext = Path.GetExtension(path);
            return ext.Equals(".msg", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".eml", StringComparison.OrdinalIgnoreCase);
        }

        public static MailInfo Read(string path)
        {
            var fi = new FileInfo(path);
            var info = new MailInfo
            {
                FilePath = path,
                Format = fi.Extension.Equals(".msg", StringComparison.OrdinalIgnoreCase) ? MailFormat.Msg : MailFormat.Eml,
            };
            try
            {
                info.FileSize = fi.Length;
                info.FileModifiedUtc = fi.LastWriteTimeUtc;
                if (fi.Length == 0) throw new InvalidDataException("err.empty");

                if (info.Format == MailFormat.Msg) MsgParser.Fill(path, info);
                else EmlParser.Fill(path, info);

                Finish(info);
            }
            catch (Exception ex)
            {
                info.ParseError = Describe(ex);
            }
            return info;
        }

        private static void Finish(MailInfo info)
        {
            info.Subject = (info.Subject ?? "").Trim();
            info.SenderEmail = (info.SenderEmail ?? "").Trim();
            info.SenderName = (info.SenderName ?? "").Trim().Trim('"', '\'').Trim();
            if (info.SenderName.Length == 0) info.SenderName = info.SenderEmail;
            info.MessageId = NormalizeMessageId(info.MessageId);
        }

        internal static string NormalizeMessageId(string? id)
        {
            if (string.IsNullOrWhiteSpace(id)) return "";
            return id!.Trim().Trim('<', '>').Trim().ToLowerInvariant();
        }

        internal static void AddUnique(List<string> list, string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            list.Add(name!.Trim());
        }

        private static string Describe(Exception ex)
        {
            // 캐시에 언어와 무관한 "코드\t상세"로 저장 → 화면에서 Display.Error가 현재 언어로 표시
            if (ex is UnauthorizedAccessException) return "err.access\t";
            if (ex is FileNotFoundException) return "err.notfound\t";
            if (ex is IOException io && io.Message.IndexOf("being used", StringComparison.OrdinalIgnoreCase) >= 0) return "err.locked\t";
            if (ex is InvalidDataException && ex.Message.StartsWith("err.")) return ex.Message + "\t";
            return $"err.corrupt\t{ex.GetType().Name}: {ex.Message}";
        }
    }
}
