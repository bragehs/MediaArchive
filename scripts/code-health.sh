#!/usr/bin/env zsh
set -euo pipefail

REPO="${0:A:h:h}"
cd "$REPO"
export CS_DISABLE_VERSION_CHECK=1

# The MCP server unpacks its own copy of the CLI under a versioned folder.
cs_bin() {
    command -v cs && return
    local bundled=(~/Library/Caches/codehealth-mcp/MCP-*/cs(N-.om))
    (( ${#bundled} )) && print -- "${bundled[1]}" && return
    return 1
}

CS="$(cs_bin)" || {
    print -u2 "code-health: no CodeScene CLI found."
    print -u2 "  brew install cs-mcp, sign in with cs-mcp auth, then run one Code Health check through the MCP server."
    exit 1
}

scored() { [[ "$1" == *.(cs|swift) && "$1" != Migrations/* && "$1" != native/Sources/Generated/* ]] }

staged=false
if [[ "${1:-}" == --staged ]]; then
    staged=true
    files=("${(@f)$(git diff --cached --name-only --diff-filter=ACMR)}")
elif (( $# )); then
    files=("$@")
else
    files=("${(@f)$(git ls-files)}")
fi

failed=0
for file in $files; do
    scored "$file" || continue
    if $staged; then
        report="$(git show ":$file" | "$CS" check --file-name "$file")"
    else
        report="$("$CS" check "$file")"
    fi
    score="$(print -r -- "$report" | sed -nE 's/.*Code health score: ([0-9.]+|N\/A).*/\1/p' | head -1)"
    [[ "$score" == N/A || "$score" == 10 || "$score" == 10.0* ]] && continue
    (( failed++ )) || true
    print -u2 "✗ $file — ${score:-no score}"
    print -r -- "$report" | grep -E '^(warn|error):' | sed -E "s|^[a-z]+: [^:]+:([0-9]+): |    line \1: |; s| \(null\)$||" >&2 || true
done

if (( failed )); then
    print -u2 "code-health: $failed file(s) below 10. Fix them before committing."
    exit 1
fi
print "code-health: every checked file scores 10."
