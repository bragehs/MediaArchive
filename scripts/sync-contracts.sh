#!/usr/bin/env zsh
#
# sync-contracts.sh — regenerate the Swift side of the native boundary.
#
# The C# route table (Services/Native) is the source of truth; tools/SwiftGen
# reflects over it and writes native/Sources/Generated/Contracts.swift: the
# Codable structs, the enums and the typed Api. ./ma runs this on every build,
# so a renamed C# property fails the Swift build instead of emptying a screen.
#
# Usage:
#   scripts/sync-contracts.sh            # rewrite Contracts.swift
#   scripts/sync-contracts.sh --check    # exit 1 if it is out of date

set -euo pipefail

REPO="${0:A:h:h}"
cd "$REPO"

dotnet run --project tools/SwiftGen --nologo -v quiet -- \
    native/Sources/Generated/Contracts.swift "$@"
