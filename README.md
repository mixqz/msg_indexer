# Email Archive Indexer

Windows 11용 무설치 이메일 백업(.msg/.eml) 인덱서. 명세: `docs/spec.md`

## 빌드 (Mac, Docker만 필요)

```bash
./build.sh        # 테스트 + exe 빌드 → dist/EmailIndexer.exe
./build.sh test   # 핵심 로직 테스트만
python3 package.py  # 배포 zip → dist/EmailIndexer-v<버전>.zip (exe + 사용안내 + 테스트 체크리스트)
```

- 사용자 안내: `docs/사용안내.md` · 실기 테스트: `docs/windows-test-checklist.md` · 명세: `docs/spec.md`
- 버전: `src/EmailIndexer.Core/AppInfo.cs`, `src/EmailIndexer.App/EmailIndexer.App.csproj`, `app.manifest` 세 곳을 함께 올린다.

## 구조

- `src/EmailIndexer.Core` — 메일 읽기·캐시·중복·파일명 규칙 (netstandard2.0, Docker에서 테스트)
- `src/EmailIndexer.App` — WinForms 화면·Outlook 연동 (.NET Framework 4.8, 단일 exe)
- `tests/EmailIndexer.Core.Tests` — 핵심 로직 테스트 (xUnit)

## 진단·샘플

- 엔진 점검: `EmailIndexer.exe --scan-test <폴더>` → 폴더 안에 `scan-test.txt` 생성
- 캐시·중복 점검: `EmailIndexer.exe --index-test <폴더>` → `.emailindex` 캐시 생성, `index-test.txt`에 결과 (파일은 지우지 않음)
- 1만 건 성능: 테스트 실행 시 `EIDX_PERF=1`
- Mac에서 exe 스모크: `docker run --rm -v "$PWD":/src -w /src mono:latest mono dist/EmailIndexer.exe --scan-test samples/synthetic`
- 합성 샘플 재생성: 테스트 실행 시 `EIDX_EXPORT_SAMPLES=/src/samples/synthetic` 지정
