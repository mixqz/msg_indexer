#!/usr/bin/env python3
"""Release package: dist/EmailIndexer-v<version>.zip + dist/EmailIndexer-v<version>.zip.sha256

Contents: EmailIndexer.exe, README.txt (English), user guides in every language, LICENSE, THIRD-PARTY-NOTICES.
Text files get CRLF + UTF-8 BOM so they open correctly in Notepad; zipfile sets the UTF-8 name flag.
"""
import hashlib, pathlib, re, zipfile

root = pathlib.Path(__file__).resolve().parent
version = re.search(r'Version = "([^"]+)"', (root / "src/EmailIndexer.Core/AppInfo.cs").read_text(encoding="utf-8")).group(1)
exe = root / "dist/EmailIndexer.exe"
out = root / f"dist/EmailIndexer-v{version}.zip"


def crlf(p: pathlib.Path) -> bytes:
    return "\ufeff".encode() + p.read_text(encoding="utf-8").replace("\r\n", "\n").replace("\n", "\r\n").encode("utf-8")


with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED) as z:
    z.write(exe, "EmailIndexer.exe")
    z.writestr("README.txt", crlf(root / "README.md"))
    for guide in sorted((root / "docs/guide").glob("USER_GUIDE.*.md")):
        z.writestr(f"guide/{guide.stem}.txt", crlf(guide))  # guide/USER_GUIDE.en.txt …
    z.writestr("LICENSE.txt", crlf(root / "LICENSE"))
    z.writestr("THIRD-PARTY-NOTICES.txt", crlf(root / "THIRD-PARTY-NOTICES.md"))


def sha256(p: pathlib.Path) -> str:
    return hashlib.sha256(p.read_bytes()).hexdigest()


zip_sha, exe_sha = sha256(out), sha256(exe)
# sha256sum format, verifiable with: sha256sum -c  /  Get-FileHash
(out.parent / (out.name + ".sha256")).write_text(f"{zip_sha}  {out.name}\n{exe_sha}  EmailIndexer.exe\n", encoding="ascii")
print(f"{out}  ({out.stat().st_size // 1024} KB)")
print(f"zip SHA-256: {zip_sha}")
print(f"exe SHA-256: {exe_sha}")
