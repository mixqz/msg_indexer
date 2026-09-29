# Contributing

Thanks for helping! Bug reports, translation fixes and new languages are all welcome. Please open an issue first for larger changes.

## Fixing a translation

The translations were written with AI assistance and reviewed for meaning, but not yet by native speakers. If something reads oddly in your language, a pull request is the fastest fix.

1. Find the text in `src/EmailIndexer.Core/Lang/<lang>*.lang` (for example `fr.main.lang`). The English source is in the matching `en*.lang` file with the same key.
2. Change only the text after `=`. One `key = value` per line; `#` starts a comment; `\n` is a line break.
3. Keep placeholders such as `{0}` or `{1:#,0}` exactly (their order may change).
4. Plural keys come in pairs, `key.one` and `key.other`. Languages without plural forms use the same text for both.
5. Use the terms in [docs/i18n/glossary.md](docs/i18n/glossary.md), which follow Microsoft's Windows and Outlook terminology. If you think a glossary term is wrong, change it there too.
6. Check your work:
   ```bash
   python3 tools/i18n_check.py fr   # missing/unknown keys, placeholders, braces
   ./build.sh test                  # full test suite (Docker)
   ```

The user guide for each language is in `docs/guide/USER_GUIDE.<lang>.md` and each README is `README.<lang>.md`. Button names quoted there should match the `.lang` files.

Note: the attachment markers in file names (`_AttY`, `_첨부O`, …) are fixed in `src/EmailIndexer.Core/Files/FileNameRule.cs`, not in the `.lang` files, because changing them would make existing files look un-normalized. Please discuss changes to them in an issue first.

## Adding a language

1. Copy every `en*.lang` file to `<code>*.lang` (e.g. `de.lang`, `de.main.lang`, …) and translate them.
2. Add the language to `L.Languages` in `src/EmailIndexer.Core/Text/L.cs` (code and its own name, e.g. `("de", "Deutsch")`) and, if it has plurals, to `L.PluralForm`.
3. Add its file-name words to `FileNameRule` (`ByLanguage`) in `src/EmailIndexer.Core/Files/FileNameRule.cs`.
4. If it needs a specific font, add it to `Ui.CreateFont` in `src/EmailIndexer.App/Ui.cs`.
5. Add glossary columns, `docs/guide/USER_GUIDE.<code>.md` and `README.<code>.md`, and link the README from the others.
6. Run `python3 tools/i18n_check.py <code>` and `./build.sh test`. The tests check every listed language automatically.

## Code changes

- Put logic in `src/EmailIndexer.Core` with tests in `tests/EmailIndexer.Core.Tests`; keep the WinForms layer thin.
- Never hard-code user-visible text: add a key to `en*.lang` (and the other languages) and use `L.T` / `L.F` / `L.P`. A test fails if Korean text is hard-coded.
- Logs stay in English so any maintainer can read a user's log.
- Anything that deletes files must go to the Recycle Bin, show a preview first, and be covered by a test.
- Don't commit real mail, names or addresses. `samples/synthetic` is generated from the tests.
