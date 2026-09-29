using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using EmailIndexer.Core;

namespace EmailIndexer.App
{
    /// <summary>도움말 (F1): 왼쪽 주제 목록, 오른쪽 설명. 키보드 ↑↓로 주제 이동, Esc로 닫기.</summary>
    internal sealed class HelpForm : Form
    {
        private sealed class Topic
        {
            public string Title = ""; public string Body = "";
            public override string ToString() => Title;
        }

        private static readonly List<Topic> Topics = new List<Topic>
        {
            new Topic { Title = "시작하기", Body =
@"이 프로그램은 Outlook 메일 백업 파일(.msg / .eml)을 한눈에 보고, 찾고, 정리하는 도구입니다. 설치 없이 exe 파일 하나로 실행됩니다.

1. [폴더 변경]을 눌러 메일 백업 폴더를 고릅니다.
   탐색기에서 폴더를 이 창으로 끌어다 놓아도 됩니다.
2. 폴더와 하위 폴더의 메일을 모두 읽어 목록으로 보여줍니다.
3. 다음부터는 바뀐 파일만 읽기 때문에 몇 초 안에 열립니다.

• 관리자 권한으로 실행하지 마세요. Outlook과 권한이 다르면 Outlook 백업이 되지 않습니다.
• 메일 파일은 이 프로그램이 이름을 바꾸거나 옮기거나 휴지통으로 보낼 때만 바뀝니다. 그 밖에는 읽기만 합니다." },

            new Topic { Title = "목록 보기", Body =
@"목록의 열

• 일시: 받은 메일은 받은 시각, 보낸 메일은 보낸 시각
• 구분: 받음 / 보냄 (보낸 메일 판단 방법은 '설정' 참고)
• 발신자, 제목
• 첨부: 실제 첨부 파일 개수 (서명 로고 같은 본문 속 이미지는 세지 않음)
• 일정: 초대 · 수락 · 거절 · 미정 · 취소
• 크기, 폴더
• 상태
  - 중복: 같은 메일이 또 있고, 이 파일은 정리 대상
  - 중복(원본): 같은 메일 묶음에서 남길 파일
  - 유사: 발신자·제목이 같고 1분 안에 온, 내용이 조금 다른 메일 (표시만 함)
  - 오류: 읽을 수 없는 파일 (제목 칸에 사유 표시)
  - 정규화됨: 파일명이 규칙대로임

글자색: 회색 = 중복, 주황 = 유사, 빨강 = 오류

• 열 제목을 누르면 정렬되고, 한 번 더 누르면 순서가 바뀝니다.
• 여러 개 선택: Ctrl+클릭, Shift+클릭, Ctrl+A
• 오른쪽 클릭: 보기 · 열기 · 폴더에서 보기 · 경로 복사 · 정규화 · 이동 · 삭제 · 이 발신자만 보기" },

            new Topic { Title = "검색", Body =
@"위쪽 검색창에 입력하면 바로 결과가 좁혀집니다.

• 기본은 제목, 사람(발신자·받는사람·참조), 본문, 첨부파일명을 한꺼번에 찾습니다.
• 옆 버튼 [전체 | 제목 | 사람 | 본문 | 첨부명]으로 찾을 범위를 좁힐 수 있습니다.
• 여러 단어를 넣으면 모두 포함된 메일만 찾습니다.
  예) 한빛마트 3월분
• 따옴표로 감싸면 그 문구 그대로 찾습니다.
  예) ""RE: 한빛마트""
• 대소문자는 구분하지 않습니다.

단축키: Ctrl+F = 검색창으로 이동, Esc = 검색어 지우기, ↓ = 목록으로 이동" },

            new Topic { Title = "필터", Body =
@"왼쪽 필터로 목록을 좁힙니다. 여러 필터를 함께 쓰면 모든 조건을 만족하는 메일만 남습니다.

• 기간: 오늘 / 최근 7일 / 최근 30일 / 올해 / 직접 지정
• 구분: 받음, 보냄
• 첨부: 있음, 없음
• 일정: 초대, 수락, 거절, 미정, 취소
• 상태: 중복, 유사, 오류, 미정규화
• 형식: msg, eml
• 폴더: 누르면 그 폴더와 하위 폴더만 보여줍니다.
• 발신자 TOP 30: 메일이 많은 발신자 순입니다. 누르면 그 사람 메일만 보여줍니다.

한 묶음에서 아무것도 체크하지 않으면 '전체'입니다.
적용 중인 조건은 검색창 아래 '적용:' 줄에 표시됩니다.
[초기화]를 누르면 검색어와 필터가 모두 풀립니다." },

            new Topic { Title = "미리보기와 열기", Body =
@"미리보기: 목록에서 더블클릭, Enter 또는 [미리보기]

• [서식 보기]: 메일 모양 그대로 보여줍니다. 표와 서명 이미지도 나옵니다.
• [텍스트 보기]: 글자만 빠르게 봅니다. 보안 배너 표식 같은 불필요한 문구는 뺍니다.
• [◀ 이전 / 다음 ▶]: 목록 순서대로 넘겨 봅니다. Alt+← / Alt+→도 됩니다.
• [본문 복사]: 본문 글자를 클립보드에 복사합니다.
• 첨부파일은 이름만 보여주고, 저장하려면 Outlook으로 열어야 합니다.
• 안전을 위해 외부 이미지와 스크립트는 막습니다. 메일 속 링크를 누르면 브라우저로 열지 먼저 묻습니다.

열기: Ctrl+O 또는 [열기]
실제 파일을 Outlook에서 엽니다." },

            new Topic { Title = "파일명 정규화", Body =
@"메일 파일 이름을 한 가지 규칙으로 바꿔, 이름만 봐도 내용을 알 수 있게 합니다.

규칙: 날짜_시간_발신자_제목_첨부O(또는 X).msg
예) 260823_175434_Alex Kim [Sales Team]_W35 team agenda_첨부X.msg

• 날짜·시간: 받은 메일은 받은 시각, 보낸 메일은 보낸 시각
• 제목은 RE:, FW: 같은 머리말까지 그대로 씁니다.
• 파일명에 쓸 수 없는 문자( : / ? * "" < > | )는 모양이 같은 전각 문자로 바꿉니다.
  예) : → ：
• 폴더 경로를 포함해 260자를 넘을 때만 제목 끝을 줄이고 '…'를 붙입니다.
• 같은 이름이 있으면 뒤에 (2), (3)을 붙입니다.

사용 방법
1. 바꿀 메일을 선택합니다. 선택하지 않으면 지금 보이는 전체가 대상입니다.
2. [파일명 정규화]를 누르면 '변경 전 → 변경 후' 표가 먼저 나옵니다.
3. 확인한 뒤 실행합니다.

되돌리기: [파일명 정규화] 창의 [마지막 정규화 되돌리기]를 누르면 원래 이름으로 돌아갑니다." },

            new Topic { Title = "이동과 삭제", Body =
@"선택 이동
1. 메일을 선택하고 [선택 이동]을 누릅니다.
2. 옮길 폴더를 고르고 확인합니다.
• 같은 이름이 있으면 (2)를 붙입니다.
• 백업 폴더 밖으로 옮기면 목록에서 빠집니다.

선택 삭제: Del 키 또는 [선택 삭제]
• 영구 삭제가 아니라 Windows 휴지통으로 보냅니다. 휴지통에서 언제든 복원할 수 있습니다.

모든 이름 변경·이동·삭제는 백업 폴더의 .emailindex\actions.log에 기록됩니다." },

            new Topic { Title = "중복 처리", Body =
@"같은 메일은 두 가지 기준으로 판단합니다.
• 파일 내용이 완전히 같음
• 메일 고유번호(Message-ID)가 같음 (msg와 eml 사이도 비교)

자동 정리
이미 있는 메일을 백업 폴더에 또 복사해 넣으면, 다음 스캔 때 새로 들어온 쪽을 휴지통으로 보내고 알려줍니다.
다음 경우에는 자동으로 지우지 않고 '중복' 표시만 합니다.
• 처음 스캔하는 폴더
• 새로 들어온 파일끼리만 겹칠 때
• 이름만 바꾸거나 다른 폴더로 옮긴 파일 (중복으로 보지 않음)
• 휴지통에서 복원한 파일 (다시 지우지 않음)

[중복 정리] 버튼
'중복' 표시된 파일을 표로 보여준 뒤 휴지통으로 보냅니다. 묶음마다 원본 1개는 남깁니다.
남길 파일 우선순위: 정규화된 이름 → msg 형식 → 먼저 들어온 파일

'유사' 메일은 내용이 다르므로 정리 대상이 아닙니다." },

            new Topic { Title = "Outlook 백업", Body =
@"Outlook(classic)의 [받은편지함]과 [보낸편지함] 메일을 .msg 파일로 저장합니다. Outlook에 있는 원본 메일은 그대로 둡니다.

1. [Outlook 백업]을 누릅니다.
   Outlook이 꺼져 있으면 자동으로 켭니다. 프로필 선택 창이 뜨면 선택해 주세요.
2. 기간을 고릅니다: 마지막 백업 이후(기본) / 최근 N일 / 전체
3. [백업 시작]을 누릅니다.

• 백업 폴더에 바로(하위 폴더 없이) 정규화된 이름으로 저장합니다.
• 이미 백업된 메일은 건너뜁니다.
• 저장한 파일이 실제로 생겼는지 확인한 것만 '저장'으로 셉니다. 실패한 메일은 사유를 보여줍니다.
• Outlook에 '다른 프로그램이 접근하려고 합니다' 창이 뜨면 [허용]을 누르세요.
• 새 Outlook(Windows용 새 Outlook)은 지원하지 않습니다. Outlook 오른쪽 위 '새 Outlook' 스위치를 꺼서 classic으로 전환하세요.

기록: .emailindex\outlook-backup.log" },

            new Topic { Title = "설정", Body =
@"[설정] → 내 이메일 주소 (한 줄에 하나)

발신자가 이 주소인 메일은 '보낸 메일'로 봅니다. 보낸 메일은 보낸 시각으로 정렬하고 파일명을 붙입니다.
• Outlook 백업을 실행하면 계정 주소가 자동으로 추가됩니다.
• Outlook 백업으로 가져온 보낸편지함 메일은 주소와 관계없이 '보냄'으로 표시됩니다.

[실행 환경 점검]: Windows·.NET 버전, 권한, Outlook 상태를 보여줍니다. 문제를 문의할 때 [정보 복사]로 전달해 주세요." },

            new Topic { Title = "단축키", Body =
@"F1          도움말
F5          스캔 (다시 누르면 취소)
Ctrl+F      검색창으로 이동
Esc         검색어 지우기 / 창 닫기
↓           검색창에서 목록으로 이동
Enter       미리보기
Ctrl+O      Outlook으로 열기
Del         선택한 메일을 휴지통으로
Ctrl+A      전체 선택

미리보기 창에서
Alt+← / →   이전 / 다음 메일
Ctrl+O      Outlook으로 열기
Esc         닫기" },

            new Topic { Title = "문제 해결", Body =
@"• Outlook 백업에서 '연결할 수 없습니다'
  Outlook과 이 프로그램을 둘 다 일반 권한(더블클릭)으로 다시 실행하세요.

• '새 Outlook' 안내가 나옴
  Outlook 오른쪽 위의 '새 Outlook' 스위치를 꺼서 classic으로 전환하세요.

• 목록에 빨간 '오류' 파일
  손상됐거나 메일 형식이 아닌 파일입니다. 제목 칸에 사유가 나옵니다.

• 파일이 목록에 안 보임
  [스캔(F5)]을 누르세요. 필터가 걸려 있으면 [초기화]를 누르세요.

문의할 때 보낼 기록 파일
• 백업폴더\.emailindex\last-scan.log: 마지막 스캔 결과와 오류 사유
• 백업폴더\.emailindex\actions.log: 이름 변경·이동·삭제 기록
• 백업폴더\.emailindex\outlook-backup.log: Outlook 백업 기록
• %APPDATA%\EmailIndexer\error.log: 예상하지 못한 오류

.emailindex 숨김 폴더는 목록을 빨리 여는 데 쓰는 캐시입니다. 지워도 메일에는 영향이 없습니다." },
        };

        private readonly ListBox _topics = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false, BorderStyle = BorderStyle.None, AccessibleName = "도움말 주제" };
        private readonly TextBox _body = new TextBox
        {
            Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, WordWrap = true,
            BorderStyle = BorderStyle.None, BackColor = SystemColors.Window, AccessibleName = "도움말 내용",
        };
        private readonly Label _heading = new Label { Dock = DockStyle.Top, Height = 40, Padding = new Padding(14, 12, 14, 0), AutoEllipsis = true };

        public HelpForm(string? topic = null)
        {
            Text = $"도움말 - {AppInfo.Name} {AppInfo.Version}";
            Font = Ui.Base;
            Icon = Ui.AppIcon ?? Icon;
            Size = new Size(860, 600);
            MinimumSize = new Size(560, 400);
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            KeyPreview = true;

            _heading.Font = new Font(Ui.Base.FontFamily, 13F, FontStyle.Bold);
            _body.Font = new Font(Ui.Base.FontFamily, 10F);
            _topics.Font = new Font(Ui.Base.FontFamily, 10F);
            _topics.ItemHeight = _topics.Font.Height + 10;
            _topics.DrawMode = DrawMode.OwnerDrawFixed;
            _topics.DrawItem += DrawTopic;
            foreach (var t in Topics) _topics.Items.Add(t);
            _topics.SelectedIndexChanged += (_, __) => ShowTopic();

            var left = new Panel { Dock = DockStyle.Left, Width = 190, Padding = new Padding(8, 10, 0, 10), BackColor = Theme.AppBg };
            left.Controls.Add(_topics);
            var divider = new Label { Dock = DockStyle.Left, Width = 1, BackColor = Theme.Border };
            var bodyHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16, 6, 12, 8), BackColor = SystemColors.Window };
            bodyHost.Controls.Add(_body);
            var right = new Panel { Dock = DockStyle.Fill, BackColor = SystemColors.Window };
            right.Controls.Add(bodyHost);
            right.Controls.Add(_heading);

            var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8, 6, 8, 6) };
            var close = Ui.Btn("닫기", (_, __) => Close(), 80);
            bar.Controls.Add(close);
            bar.Controls.Add(new Label
            {
                AutoSize = true, ForeColor = SystemColors.GrayText, Padding = new Padding(0, 7, 12, 0),
                Text = "↑↓ 주제 이동 · Esc 닫기 · 자세한 사용 안내는 함께 받은 '사용안내.txt' 참고",
            });
            CancelButton = close;

            Controls.Add(right);
            Controls.Add(divider);
            Controls.Add(left);
            Controls.Add(bar);

            Theme.Apply(this);
            var start = topic == null ? 0 : Math.Max(0, Topics.FindIndex(t => t.Title == topic));
            Load += (_, __) => { _topics.SelectedIndex = start; _topics.Focus(); };
        }

        private void ShowTopic()
        {
            if (!(_topics.SelectedItem is Topic t)) return;
            _heading.Text = t.Title;
            _body.Text = t.Body.Replace("\r\n", "\n").Replace("\n", Environment.NewLine);
            _body.SelectionStart = 0;
            _body.SelectionLength = 0;
        }

        /// <summary>선택 주제는 강조색 막대 + 굵은 글씨 (색만으로 구분하지 않음).</summary>
        private void DrawTopic(object? sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            bool selected = (e.State & DrawItemState.Selected) != 0;
            using (var bg = new SolidBrush(selected ? Theme.Surface : Theme.AppBg))
                e.Graphics.FillRectangle(bg, e.Bounds);
            if (selected)
                using (var bar = new SolidBrush(Color.FromArgb(0, 95, 184)))
                    e.Graphics.FillRectangle(bar, e.Bounds.Left, e.Bounds.Top + 4, 3, e.Bounds.Height - 8);
            var font = selected ? new Font(_topics.Font, FontStyle.Bold) : _topics.Font;
            TextRenderer.DrawText(e.Graphics, Topics[e.Index].Title, font,
                new Rectangle(e.Bounds.Left + 12, e.Bounds.Top, e.Bounds.Width - 12, e.Bounds.Height),
                SystemColors.ControlText, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            if (selected) font.Dispose();
            if ((e.State & DrawItemState.Focus) != 0) e.DrawFocusRectangle();
        }
    }
}
