#!/usr/bin/env zsh
#
# sync-colors.sh — push colors.json into the two places that consume it.
#
# The palette lives in colors.json. This rewrites the marked block in
# wwwroot/app.css (CSS custom properties) and in widget/MediaArchiveWidget.swift
# (SwiftUI Colors), so a colour is changed in one file and never drifts between
# the WebView and the native widget.
#
# Run it after editing colors.json. Not wired into ./ma on purpose: the palette
# changes rarely and codegen on every build hides itself.
#
# Usage:
#   scripts/sync-colors.sh            # rewrite both blocks
#   scripts/sync-colors.sh --check    # exit 1 if either is out of date

set -euo pipefail

REPO="${0:A:h:h}"
cd "$REPO"

check=0
[[ "${1:-}" == "--check" ]] && check=1

python3 - "$check" <<'PY'
import json, re, sys

check = sys.argv[1] == "1"
palette = json.load(open("colors.json"))
colors = {k: v for group, entries in palette.items()
          if group != "_" for k, v in entries.items()}

def camel(name):
    head, *rest = name.split("-")
    return head + "".join(part.capitalize() for part in rest)

css = "\n".join(f"    --{name}: {hex};" for name, hex in colors.items())

swift = "\n".join(
    "    static let {} = Color(red: 0x{} / 255, green: 0x{} / 255, blue: 0x{} / 255)".format(
        camel(name), hex[1:3], hex[3:5], hex[5:7])
    for name, hex in colors.items())

targets = [
    ("wwwroot/app.css", r"(/\* colors:start.*?\*/).*?([ \t]*/\* colors:end \*/)", css),
    ("widget/MediaArchiveWidget.swift", r"(// colors:start.*?)\n.*?([ \t]*// colors:end)", swift),
]

stale = []
for path, pattern, block in targets:
    source = open(path).read()
    updated, count = re.subn(
        pattern, lambda m: f"{m[1]}\n{block}\n    {m[2].strip()}", source, flags=re.S)
    if count == 0:
        sys.exit(f"no colors:start/colors:end markers in {path}")
    if updated == source:
        continue
    if check:
        stale.append(path)
    else:
        open(path, "w").write(updated)
        print(f"  updated {path}")

if check and stale:
    sys.exit("out of date with colors.json: " + ", ".join(stale))

print(f"✓ {len(colors)} colours{' up to date' if check else ' synced'}")
PY
