#!/usr/bin/env python3
"""배포 패키지: dist/EmailIndexer-v<버전>.zip (exe + 사용안내.txt + 테스트 체크리스트.txt).
Windows 탐색기에서 한글 파일명이 깨지지 않도록 zipfile(UTF-8 플래그)로 만든다."""
import hashlib, pathlib, re, zipfile

root = pathlib.Path(__file__).resolve().parent
version = re.search(r'Version = "([^"]+)"', (root / "src/EmailIndexer.Core/AppInfo.cs").read_text(encoding="utf-8")).group(1)
exe = root / "dist/EmailIndexer.exe"
out = root / f"dist/EmailIndexer-v{version}.zip"

def crlf(p: pathlib.Path) -> bytes:
    # 메모장에서 보기 좋게 CRLF + UTF-8 BOM
    return "\ufeff".encode() + p.read_text(encoding="utf-8").replace("\r\n", "\n").replace("\n", "\r\n").encode("utf-8")

with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED) as z:
    z.write(exe, "EmailIndexer.exe")
    z.writestr("사용안내.txt", crlf(root / "docs/사용안내.md"))
    z.writestr("테스트 체크리스트.txt", crlf(root / "docs/windows-test-checklist.md"))

sha = hashlib.sha256(exe.read_bytes()).hexdigest()
print(f"{out}  ({out.stat().st_size // 1024} KB)")
print(f"EmailIndexer.exe SHA-256: {sha}")
