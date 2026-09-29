using System;
using System.IO;
using System.Text;

namespace EmailIndexer.Core.Index
{
    /// <summary>파일을 휴지통으로 보내는 기능 (Windows 구현은 App, 테스트는 가짜).</summary>
    public interface ITrash
    {
        /// <summary>성공하면 true. 실패 사유는 예외 대신 false + message.</summary>
        bool SendToRecycleBin(string path, out string? error);
    }

    /// <summary><c>.emailindex\actions.log</c>에 삭제·이동·이름변경 기록 (탭 구분, UTF-8).</summary>
    public static class ActionLog
    {
        public const string FileName = "actions.log";
        private static readonly object Gate = new object();

        public static void Write(string root, string action, string target, string detail = "")
        {
            try
            {
                var dir = IndexStore.IndexDir(root);
                Directory.CreateDirectory(dir);
                var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\t{action}\t{target}\t{detail}{Environment.NewLine}";
                lock (Gate) File.AppendAllText(Path.Combine(dir, FileName), line, Encoding.UTF8);
            }
            catch { /* 기록 실패가 작업을 막지 않게 */ }
        }
    }
}
