#!/bin/sh
# Auto-format staged F# files with Fantomas and re-stage them
#
# WARNING: If a developer stages only specific hunks of a file (git add -p),
# Fantomas formats the full working-tree version, then git add "$@" stages all
# of it — silently committing hunks that were intentionally left unstaged.
set -e

if [ $# -eq 0 ]; then
    exit 0
fi

# Warn about partially-staged files whose unstaged hunks will be pulled in
for file in "$@"; do
    if git diff --quiet -- "$file" 2>/dev/null; then
        : # no unstaged changes, safe
    else
        echo "WARNING: '$file' has unstaged changes that will now be included in the commit (Fantomas formats the full working-tree version)." >&2
    fi
done

dotnet fantomas "$@"
git add "$@"
