using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace EmailIndexer.Core.View
{
    /// <summary>사용자 설정 (<c>%APPDATA%\EmailIndexer\settings.json</c>).</summary>
    [DataContract]
    public sealed class AppSettings
    {
        [DataMember] public string BackupRoot { get; set; } = "";
        [DataMember] public List<string> MyAddresses { get; set; } = new List<string>();
        /// <summary>열 이름 → 너비.</summary>
        [DataMember] public Dictionary<string, int> ColumnWidths { get; set; } = new Dictionary<string, int>();
        [DataMember] public int WindowX { get; set; } = -1;
        [DataMember] public int WindowY { get; set; } = -1;
        [DataMember] public int WindowW { get; set; } = 1280;
        [DataMember] public int WindowH { get; set; } = 780;
        [DataMember] public bool WindowMaximized { get; set; }
        [DataMember] public bool PreviewAsText { get; set; }

        public static string DefaultPath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EmailIndexer", "settings.json");

        public static AppSettings Load(string? path = null)
        {
            path ??= DefaultPath;
            try
            {
                if (!File.Exists(path)) return new AppSettings();
                using var fs = File.OpenRead(path);
                var s = (AppSettings?)Serializer().ReadObject(fs) ?? new AppSettings();
                s.MyAddresses ??= new List<string>();
                s.ColumnWidths ??= new Dictionary<string, int>();
                s.BackupRoot ??= "";
                return s;
            }
            catch
            {
                return new AppSettings(); // 손상되면 기본값으로
            }
        }

        public void Save(string? path = null)
        {
            path ??= DefaultPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var ms = new MemoryStream();
            Serializer().WriteObject(ms, this);
            File.WriteAllText(path, Encoding.UTF8.GetString(ms.ToArray()), new UTF8Encoding(false));
        }

        private static DataContractJsonSerializer Serializer()
            => new DataContractJsonSerializer(typeof(AppSettings),
                new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true });
    }
}
