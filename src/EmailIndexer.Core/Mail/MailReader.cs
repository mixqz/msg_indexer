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
                if (fi.Length == 0) throw new InvalidDataException("빈 파일");

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
            if (ex is UnauthorizedAccessException) return "파일 접근 권한 없음";
            if (ex is IOException io && io.Message.Contains("being used")) return "다른 프로그램이 사용 중";
            if (ex is FileNotFoundException) return "파일 없음";
            if (ex is InvalidDataException) return ex.Message;
            return $"손상되었거나 메일 형식이 아닌 파일 ({ex.GetType().Name}: {ex.Message})";
        }
    }
}
