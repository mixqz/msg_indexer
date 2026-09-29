using System;
using EmailIndexer.Core.Mail;

namespace EmailIndexer.Core.Index
{
    /// <summary>중복 상태 (명세 6장).</summary>
    public enum DupStatus
    {
        None,
        /// <summary>동일 파일/동일 메일 그룹에서 남길 파일.</summary>
        Keeper,
        /// <summary>동일 파일/동일 메일 그룹에서 정리 대상.</summary>
        Duplicate,
        /// <summary>발신자·제목 같고 1분 이내, 내용은 다름 — 표시만.</summary>
        Similar,
    }

    /// <summary>캐시에 저장되는 파일 1개의 항목.</summary>
    public sealed class IndexEntry
    {
        /// <summary>백업 폴더 기준 상대 경로 (구분자 '\' 또는 '/').</summary>
        public string RelPath { get; set; } = "";
        public MailInfo Mail { get; set; } = new MailInfo();
        /// <summary>파일 내용 SHA-256 (hex).</summary>
        public string ContentHash { get; set; } = "";
        public DateTime FirstSeenUtc { get; set; }
        /// <summary>Outlook 백업 시 기록한 원본 폴더 방향 (없으면 null).</summary>
        public MailDirection? FolderHint { get; set; }

        // ---- 스캔 때마다 다시 계산 (저장 안 함) ----
        public DupStatus Dup { get; set; }
        /// <summary>같은 중복/유사 그룹끼리 같은 값. 0 = 없음.</summary>
        public int DupGroup { get; set; }
        /// <summary>Duplicate일 때 남길 파일의 상대 경로.</summary>
        public string? KeeperRelPath { get; set; }
    }
}
