# Email Archive Indexer

**English** · [한국어](README.ko.md) · [Español](README.es.md) · [Français](README.fr.md) · [日本語](README.ja.md) · [简体中文](README.zh.md)

Outlook data files grow large as mail accumulates. When indexing isn't working properly, even a message you know is there can fail to show up in search. Finding an old attachment can mean working through folders one by one.

Email Archive Indexer saves mail as individual files and indexes those files for search. It backs up your Outlook (classic) Inbox and Sent Items as `.msg` files, and also reads existing `.msg` / `.eml` archives. You can search subjects, senders and recipients, message bodies and attachment names without relying on Outlook's search index.

The mail files stay in ordinary folders, so you can copy them to another drive or organize them however you like. The app can give them consistent names and help you clean up duplicates. Your original mail in Outlook stays untouched.

It's a portable Windows program that runs from a single `.exe`. No installation or administrator rights are needed, and indexing and search work locally without an internet connection.

![Main window](docs/images/main-en.png)

*Screenshot taken under Mono on Linux with sample data; the real Windows look is slightly different.*

## Features

- **Fast search** across subject, people (From/To/Cc), body and attachment names. Several words = all must match; `"quotes"` = exact phrase.
- **Filters**: date, received/sent, attachments, meeting responses (invite/accepted/declined/tentative/canceled), duplicates, similar mail, read errors, folder, top senders.
- **Preview** in formatted or text view. External images, scripts and redirects are blocked, and links ask before opening. Open the mail in Outlook with Ctrl+O.
- **File-name normalization** to `YYMMDD_HHMMSS_Sender_Subject_AttY.msg`, using the received time for received mail and the sent time for sent mail. You see a before/after preview first and can undo it.
- **Duplicates**: exact copies added to the folder go to the Recycle Bin automatically (never on the first scan). A guided cleanup covers the rest. Nothing is ever deleted permanently.
- **Outlook (classic) backup**: saves your Inbox and Sent Items as `.msg`, skips mail that is already backed up, leaves Outlook untouched, and logs every success and failure. It starts Outlook if it isn't running.
- **Incremental scans** with a small local cache: after the first scan, only changed files are read.
- **6 languages**: English, 한국어, Español, Français, 日本語, 简体中文 (Settings → Language / 언어).

## Download and run

1. Download `EmailIndexer-v<version>.zip` from [Releases](https://github.com/mixqz/msg_indexer/releases) and unzip it.
2. Double-click `EmailIndexer.exe`. The app isn't code-signed, so Windows may show **"Windows protected your PC"**. Choose **More info → Run anyway**, or right-click the file → **Properties** → **Unblock**.
3. Click **Change folder** and choose your mail backup folder.

Requirements: Windows 11 (tested) or Windows 10 with .NET Framework 4.8, which Windows includes. Outlook backup needs Outlook (classic); the new Outlook has no automation interface.

User guide: [English](docs/guide/USER_GUIDE.en.md) · [한국어](docs/guide/USER_GUIDE.ko.md) · [Español](docs/guide/USER_GUIDE.es.md) · [Français](docs/guide/USER_GUIDE.fr.md) · [日本語](docs/guide/USER_GUIDE.ja.md) · [简体中文](docs/guide/USER_GUIDE.zh.md)

## Privacy

Everything stays on your PC. The app makes no network connections. Its cache and logs live in a hidden `.emailindex` folder inside your backup folder and in `%APPDATA%\EmailIndexer`.

## Build from source

Only Docker is needed; nothing is installed on the host.

```bash
./build.sh          # tests + Windows exe → dist/EmailIndexer.exe
./build.sh test     # core tests only
python3 package.py  # release zip → dist/EmailIndexer-v<version>.zip (+ .sha256)
```

- `src/EmailIndexer.Core`: mail parsing, cache, duplicates, file-name rules, translations (netstandard2.0, tested in Docker)
- `src/EmailIndexer.App`: WinForms UI and Outlook COM (.NET Framework 4.8, merged into a single exe)
- `tests/EmailIndexer.Core.Tests`: xUnit tests
- Version: bump `src/EmailIndexer.Core/AppInfo.cs`, `src/EmailIndexer.App/EmailIndexer.App.csproj` and `src/EmailIndexer.App/app.manifest` together.
- Diagnostics: `EmailIndexer.exe --scan-test <folder>` (read-only parse report) and `--index-test <folder>` (cache and duplicate report, deletes nothing).

## Contributing

Translation fixes and new languages are very welcome; the translations were written with AI assistance and have not yet been reviewed by native speakers. See [CONTRIBUTING.md](CONTRIBUTING.md).

## License

[MIT](LICENSE). Third-party components are listed in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
