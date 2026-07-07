#!/usr/bin/env zsh
#
# new-branch.sh — create a git branch from an Obsidian issue note.
#
# An Obsidian vault is just Markdown on disk, so "branch from an issue" means:
# find the note, slugify its title, and create the branch off the latest main.
#
# Usage:
#   scripts/new-branch.sh "Log and capture"     # fuzzy name -> branch "log-and-capture"
#   scripts/new-branch.sh /abs/path/to/note.md  # full path (used by an Obsidian hotkey)
#
# Exit codes: 0 ok, 1 usage/lookup error, 2 not an issue note.

set -euo pipefail

REPO="/Users/bragehs/Desktop/projects/MediaArchive"
ISSUES_DIR="/Users/bragehs/Desktop/vault/Personal Projects/MediaArchive/Issues"

query="${1:-}"
if [[ -z "$query" ]]; then
  echo "usage: new-branch.sh <issue-name-or-path>" >&2
  exit 1
fi

# --- resolve the note file ---------------------------------------------------
# Either an exact path (an Obsidian hotkey passes the file's full path), or a
# partial name we match case-insensitively against the Issues folder.
if [[ -f "$query" ]]; then
  file="$query"
else
  matches=("${(@f)$(find "$ISSUES_DIR" -type f -iname "*${query}*.md")}")
  if [[ ${#matches[@]} -eq 0 || -z "${matches[1]}" ]]; then
    echo "no issue note matching '$query' in $ISSUES_DIR" >&2
    exit 1
  fi
  if [[ ${#matches[@]} -gt 1 ]]; then
    echo "ambiguous — '$query' matches multiple notes:" >&2
    printf '  %s\n' "${matches[@]##*/}" >&2
    exit 1
  fi
  file="${matches[1]}"
fi

# --- guard: only branch from actual issue notes ------------------------------
# Check ONLY the YAML frontmatter (the block between the first two '---' fences),
# so a "type: issue" line inside a code block in the body can't fool us.
frontmatter="$(awk 'NR==1 && $0!="---"{exit} NR==1{next} /^---[[:space:]]*$/{exit} {print}' "$file")"
if ! print -r -- "$frontmatter" | grep -qiE '^(fileClass|type):[[:space:]]*issue[[:space:]]*$'; then
  echo "not an issue note (no 'fileClass: issue' frontmatter): $file" >&2
  exit 2
fi

# --- slugify the filename ----------------------------------------------------
# "Log and capture.md" -> "log-and-capture"
title="${file##*/}"; title="${title%.md}"
slug="$(print -r -- "$title" \
  | tr '[:upper:]' '[:lower:]' \
  | sed -E 's/[^a-z0-9]+/-/g; s/^-+//; s/-+$//')"

# --- create / switch off the latest main -------------------------------------
cd "$REPO"
git rev-parse --is-inside-work-tree >/dev/null 2>&1 || { echo "not a git repo: $REPO" >&2; exit 1; }

if git show-ref --verify --quiet "refs/heads/$slug"; then
  echo "branch '$slug' already exists — switching to it"
  git switch "$slug"
else
  git switch main
  git pull --ff-only origin main
  git switch -c "$slug"
fi

echo "✓ on branch: $slug"
echo "  from note: $file"
