# Email Archive Indexer — User Guide

A portable Windows program (no installation) that lets you quickly find and organize your Outlook mail backup files (.msg / .eml), and back up mail from Outlook automatically.

## 1. Getting started

1. Put the single file `EmailIndexer.exe` anywhere you like (for example, the desktop or Documents). No installation is needed.
2. Double-click it to run.
   - If **"Windows protected your PC"** appears → **More info** → **Run anyway**
   - If it still doesn't run: right-click the file → **Properties** → check **Unblock** at the bottom → OK → run it again
3. Click **Change folder** and choose your mail backup folder. (You can also drag a folder from File Explorer onto the window.)
4. The first time, the whole folder is read. After that, only changed files are read, so it opens in seconds.

> Don't run it as administrator. If it runs at a different permission level from Outlook, Outlook backup won't work.

**Language:** The app starts in English. To change it, open **Settings** → **Language / 언어**, pick a language, and restart the app. Each language is listed in its own name (English, 한국어, Español, Français, 日本語, 简体中文).

## 2. The main window

| Area | What it does |
|---|---|
| Top | Backup folder, **Scan (F5)**, **Outlook backup**, **Settings**, **Help (F1)** |
| Filters (left) | Date · Received/Sent · Attachments · Event (invite/accepted/declined/tentative/canceled) · Status (duplicate/similar/error/not normalized) · Format · Folder · Top 30 senders |
| Search box | Searches subject, people, body and attachment names at once. Several words = all must match, `"quotes"` = exact phrase. Use the buttons next to it to narrow the scope |
| List | Click a column header to sort. Gray = duplicate, orange = similar, red = read error |
| Bottom | Total · shown · selected counts, scan result |

## 3. Everyday tasks

| To do this | Do this |
|---|---|
| Read a mail quickly | Double-click or press Enter → switch between formatted and text view, ◀ ▶ for previous/next |
| Open in Outlook | Ctrl+O or **Open** |
| Select several mails | Ctrl/Shift + click, Ctrl+A |
| Jump to the search box | Ctrl+F (Esc clears the search) |
| Right-click menu | Preview · Open · Show in folder · Copy path · Normalize · Move · Delete · Show only this sender |
| See how a feature works | F1 or **Help (F1)** |

## 4. Organizing files

- **Normalize file names**: renames files to the format `260823_175434_Alex Kim [Sales Team]_W35 team agenda_AttN.msg`.
  - Received mail uses the time received; sent mail uses the time sent.
  - The attachment marker at the end follows the app language (English `_AttY`/`_AttN`). Files already normalized in any language are left as they are.
  - If nothing is selected, every mail shown in the list is included.
  - Before anything changes, a "before → after" table is shown. **Undo last normalization** in the same window restores the original names.
  - Characters such as `: / ?` in the subject become look-alike full-width characters (`： ／ ？`).
- **Move selected**: moves files to the folder you choose. If a file with the same name exists, ` (2)` is added.
- **Delete selected (Del)**: sends files to the **Recycle Bin**, where you can restore them.
- **Clean up duplicates**: shows the files confirmed to be the same mail in a table, then sends them to the Recycle Bin. One original of each group is kept.
- **Automatic duplicate removal**: if you copy in mail that is already there, the newly added copy goes to the Recycle Bin automatically on the next scan.
  - On the very first scan nothing is deleted automatically; duplicates are only marked.
  - Files you restore from the Recycle Bin are not deleted again.
  - Files you only renamed or moved to another folder are not treated as duplicates.

## 5. Automatic Outlook backup

1. If Outlook (classic) isn't running, it starts automatically. If a profile picker appears, choose your profile. (The new Outlook is not supported.)
2. **Outlook backup** → choose a range (Since last backup / Last N days / All) → **Start backup**
3. Mail from the Inbox and Sent Items is saved **directly in the backup folder** (no subfolders) with normalized names. You can move files into subfolders yourself afterwards; the scan still finds them all.
4. Mail that is already backed up is skipped. The original mail in Outlook is left untouched.
5. If Outlook shows "A program is trying to access…", click **Allow**.

## 6. Troubleshooting

| Problem | Fix |
|---|---|
| Outlook backup: "can't connect" | Restart both Outlook and this program normally (double-click, not as administrator) |
| Outlook backup: "new Outlook" message | Turn off the **New Outlook** switch at the top right of Outlook to go back to classic |
| Red "error" files in the list | The file is damaged or isn't a mail file. The reason appears in the Subject column |
| Something behaves oddly | Send the result of **Settings** → **Check environment** and the log files below to your support contact |

**Log files** (attach them when reporting a problem; they are always written in English)
- `backup folder\.emailindex\last-scan.log` — last scan result and error reasons
- `backup folder\.emailindex\actions.log` — rename, move and delete history
- `backup folder\.emailindex\outlook-backup.log` — Outlook backup history
- `%APPDATA%\EmailIndexer\error.log` — unexpected errors

The hidden `.emailindex` folder is a cache that makes the list open quickly. Deleting it doesn't affect your mail; it is rebuilt from scratch on the next run. (After deleting it, automatic duplicate removal skips one scan, as on a first scan, and the "Since last backup" point for Outlook backup is lost.)
