#!/bin/sh
# Auto-format staged F# files with Fantomas and re-stage them
set -e

if [ $# -eq 0 ]; then
    exit 0
fi

dotnet fantomas "$@"
git add "$@"
