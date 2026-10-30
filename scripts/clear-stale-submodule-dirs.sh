#!/usr/bin/env sh
#
# Removes, from a long-lived checkout, any directory that sits where .gitmodules now declares a
# submodule but is not a git checkout itself.
#
# A path that was an ordinary folder and became a submodule (backend/src/Innovayse.SDK) is left
# behind by `git checkout` on a deploy host: its build output (bin/, obj/) is untracked, so the folder
# survives, and `git submodule update` then refuses to clone into it ("already exists and is not an
# empty directory"). Only such a folder is removed; a submodule already checked out has a .git entry
# and is kept.
#
# Usage:
#   scripts/clear-stale-submodule-dirs.sh <checkout dir>
#
# Exit status: 0 done (also when there are no submodules), 2 usage.

set -eu

dir="${1:-}"
if [ -z "$dir" ] || [ ! -d "$dir" ]; then
  echo "usage: $0 <checkout dir>" >&2
  exit 2
fi

[ -f "$dir/.gitmodules" ] || exit 0

git config -f "$dir/.gitmodules" --get-regexp '^submodule\..*\.path$' | while read -r _ path; do
  target="$dir/$path"
  if [ -d "$target" ] && [ ! -e "$target/.git" ]; then
    echo "Removing $path: a leftover folder where the submodule now goes."
    rm -rf --one-file-system "$target"
  fi
done
