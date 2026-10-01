# Email Archive Indexer

[English](README.md) · **한국어** · [Español](README.es.md) · [Français](README.fr.md) · [日本語](README.ja.md) · [简体中文](README.zh.md)

Outlook을 오래 쓰다 보면 데이터 파일이 점점 커집니다. 인덱싱까지 제대로 되지 않으면 분명히 있는 메일도 검색에서 빠지고, 예전에 받은 첨부파일 하나를 찾으려고 폴더를 한참 뒤져야 할 때가 있습니다.

Email Archive Indexer는 메일을 한 통씩 파일로 저장하고, 그 파일을 직접 인덱싱해 검색하는 도구입니다. Outlook(classic)의 받은편지함과 보낸편지함을 `.msg`로 백업하며, 이미 보관 중인 `.msg` / `.eml` 파일도 함께 검색할 수 있습니다. Outlook의 검색 인덱스에 의존하지 않고 제목, 보낸사람·받는사람, 본문, 첨부파일명으로 필요한 메일을 찾습니다.

메일 파일은 일반 폴더에 남으므로 다른 드라이브에 복사하거나 원하는 폴더로 나누어 보관하기 쉽습니다. 파일 이름을 일정한 규칙으로 맞추고 중복된 사본을 정리하는 기능도 있습니다. Outlook에 있는 원본 메일은 그대로 둡니다.

Windows에서 `.exe` 파일 하나로 실행합니다. 설치나 관리자 권한이 필요 없고, 인덱싱과 검색은 인터넷 연결 없이 내 PC에서 이루어집니다.

![메인 화면](docs/images/main-en.png)

*Linux의 Mono 환경에서 샘플 데이터로 찍은 화면입니다. 실제 Windows 화면과는 조금 다릅니다.*

## 기능

- **빠른 검색:** 제목, 사람(보낸사람·받는사람·참조), 본문, 첨부파일명을 한 번에 찾습니다. 여러 단어 = 모두 포함, `"따옴표"` = 구문 그대로.
- **필터:** 기간, 받음/보냄, 첨부, 일정 응답(초대/수락/거절/미정/취소), 중복, 유사 메일, 읽기 오류, 폴더, 자주 보낸 사람.
- **미리보기:** 서식 보기와 텍스트 보기를 지원합니다. 외부 이미지·스크립트·자동 이동은 막고, 링크는 열기 전에 묻습니다. Ctrl+O로 Outlook에서 엽니다.
- **파일명 정규화:** `YYMMDD_HHMMSS_발신자_제목_첨부O.msg` 형식으로 바꿉니다. 받은 메일은 받은 시각, 보낸 메일은 보낸 시각을 씁니다. 실행 전에 '변경 전 → 후'를 보여주고, 되돌릴 수 있습니다.
- **중복 처리:** 폴더에 새로 들어온 완전히 같은 사본은 자동으로 휴지통에 보냅니다(첫 스캔에서는 하지 않음). 나머지는 확인 화면을 거쳐 정리합니다. 영구 삭제는 하지 않습니다.
- **Outlook(classic) 백업:** 받은편지함·보낸편지함을 `.msg`로 저장합니다. 이미 백업한 메일은 건너뛰고, Outlook 원본은 건드리지 않으며, 성공·실패를 모두 기록합니다. Outlook이 꺼져 있으면 켭니다.
- **증분 스캔:** 작은 캐시를 두어 첫 스캔 이후에는 바뀐 파일만 읽습니다.
- **6개 언어:** English, 한국어, Español, Français, 日本語, 简体中文 ([Settings] → Language / 언어). 처음에는 영어로 시작합니다.

## 내려받기와 실행

1. [Releases](https://github.com/mixqz/msg_indexer/releases)에서 `EmailIndexer-v<버전>.zip`을 내려받아 압축을 풉니다.
2. `EmailIndexer.exe`를 더블클릭합니다. 코드 서명이 없어서 **"Windows의 PC 보호"** 창이 뜰 수 있습니다. **[추가 정보] → [실행]**을 누르거나, 파일 오른쪽 클릭 → **속성** → **차단 해제**를 체크하세요.
3. **[폴더 변경]**을 눌러 메일 백업 폴더를 고릅니다.

필요 환경: Windows 11(확인됨) 또는 Windows 10, .NET Framework 4.8(Windows 기본 포함). Outlook 백업에는 Outlook(classic)이 필요합니다. 새 Outlook은 외부 프로그램 연동 기능이 없습니다.

사용 안내: [English](docs/guide/USER_GUIDE.en.md) · [한국어](docs/guide/USER_GUIDE.ko.md) · [Español](docs/guide/USER_GUIDE.es.md) · [Français](docs/guide/USER_GUIDE.fr.md) · [日本語](docs/guide/USER_GUIDE.ja.md) · [简体中文](docs/guide/USER_GUIDE.zh.md)

## 개인정보

모든 데이터는 내 PC 안에만 있습니다. 프로그램은 네트워크에 연결하지 않습니다. 캐시와 기록은 백업 폴더 안의 숨김 폴더 `.emailindex`와 `%APPDATA%\EmailIndexer`에 저장됩니다.

## 소스에서 빌드

Docker만 있으면 됩니다. 컴퓨터에 따로 설치하는 것은 없습니다.

```bash
./build.sh          # tests + Windows exe → dist/EmailIndexer.exe
./build.sh test     # core tests only
python3 package.py  # release zip → dist/EmailIndexer-v<version>.zip (+ .sha256)
```

- `src/EmailIndexer.Core`: 메일 읽기, 캐시, 중복, 파일명 규칙, 번역 (netstandard2.0, Docker에서 테스트)
- `src/EmailIndexer.App`: WinForms 화면과 Outlook 연동 (.NET Framework 4.8, exe 하나로 합침)
- `tests/EmailIndexer.Core.Tests`: xUnit 테스트
- 버전: `src/EmailIndexer.Core/AppInfo.cs`, `src/EmailIndexer.App/EmailIndexer.App.csproj`, `src/EmailIndexer.App/app.manifest` 세 곳을 함께 올립니다.
- 진단: `EmailIndexer.exe --scan-test <folder>`(읽기 전용 분석 결과), `--index-test <folder>`(캐시·중복 결과, 파일을 지우지 않음)

## 기여

번역 수정과 새 언어 추가를 환영합니다. 번역은 AI의 도움으로 작성했고 아직 원어민 검토를 거치지 않았습니다. [CONTRIBUTING.md](CONTRIBUTING.md)를 참고하세요.

## 라이선스

[MIT](LICENSE). 포함된 외부 구성 요소는 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)에 있습니다.
