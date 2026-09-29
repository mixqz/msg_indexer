#!/usr/bin/env python3
"""Quick translation check (no .NET needed): python3 tools/i18n_check.py <lang>
Checks key parity with English (plural .one/.other allowed), placeholder sets, empty values, braces."""
import re, sys, pathlib

LANG_DIR = pathlib.Path(__file__).resolve().parent.parent / "src/EmailIndexer.Core/Lang"
PH = re.compile(r"\{(\d+)(?:[,:][^}]*)?\}")


def load(lang):
    d = {}
    for f in sorted(LANG_DIR.glob(f"{lang}*.lang")):
        stem = f.name[:-5]
        if stem != lang and not stem.startswith(lang + "."):
            continue
        for n, raw in enumerate(f.read_text(encoding="utf-8").splitlines(), 1):
            line = raw.strip()
            if not line or line.startswith("#") or "=" not in line:
                continue
            k, v = line.split("=", 1)
            k = k.strip()
            if k in d:
                print(f"DUP {f.name}:{n} {k}")
            d[k] = v.strip()
    return d


def main(lang):
    en, tr = load("en"), load(lang)
    if not tr:
        sys.exit(f"no files for {lang}")
    errors = []
    for k in en:
        if k not in tr:
            errors.append(f"MISSING {k}")
    for k, v in tr.items():
        base = k if k in en else (k.rsplit(".", 1)[0] + ".other" if k.endswith((".one", ".other")) else None)
        if base not in en:
            errors.append(f"UNKNOWN {k}")
            continue
        if not v:
            errors.append(f"EMPTY {k}")
        if set(PH.findall(en[base])) != set(PH.findall(v)):
            errors.append(f"PLACEHOLDER {k}: en={en[base]!r} {lang}={v!r}")
        stripped = PH.sub("", v)
        if "{" in stripped or "}" in stripped:
            errors.append(f"BRACE {k}: {v!r}")
    print("\n".join(errors) if errors else f"OK {lang}: {len(tr)} keys (en {len(en)})")
    sys.exit(1 if errors else 0)


if __name__ == "__main__":
    main(sys.argv[1])
