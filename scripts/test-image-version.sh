#!/usr/bin/env sh
#
# Tests scripts/image-version.sh in throwaway git repositories: the newest reachable release tag wins
# over older ones, a pre-release tag and a tag that is not reachable are ignored, the build metadata is
# the short commit id, and a repository without a release tag fails instead of printing a version.
#
# Usage: sh scripts/test-image-version.sh

set -u

SCRIPT="$(cd "$(dirname "$0")" && pwd)/image-version.sh"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

FAILURES=0

# Records one check. $1 description, $2 expected output, $3 actual output.
expect() {
  if [ "$2" = "$3" ]; then
    printf 'ok    %s\n' "$1"
  else
    printf 'FAIL  %s (expected "%s", got "%s")\n' "$1" "$2" "$3"
    FAILURES=$((FAILURES + 1))
  fi
}

# Creates a repository with one commit in $1.
new_repo() {
  mkdir "$1"
  git -C "$1" init -q
  git -C "$1" config user.email test@example.invalid
  git -C "$1" config user.name test
  git -C "$1" commit -q --allow-empty -m first
}

repo="$WORK/tagged"
new_repo "$repo"
git -C "$repo" tag v0.4.0
git -C "$repo" tag v0.10.0
git -C "$repo" tag v0.9.0
git -C "$repo" tag v1.0.0-rc.1
git -C "$repo" commit -q --allow-empty -m second
sha="$(git -C "$repo" rev-parse --short=8 HEAD)"
expect "newest reachable release tag, build metadata is the short commit id" "0.10.0+$sha" "$(cd "$repo" && sh "$SCRIPT")"

# A newer tag on a commit HEAD does not contain is not reachable.
git -C "$repo" checkout -q -b other
git -C "$repo" commit -q --allow-empty -m third
git -C "$repo" tag v2.0.0
git -C "$repo" checkout -q -
expect "a tag that is not reachable is ignored" "0.10.0+$sha" "$(cd "$repo" && sh "$SCRIPT")"

only_pre="$WORK/prerelease"
new_repo "$only_pre"
git -C "$only_pre" tag v1.0.0-rc.1
err="$(cd "$only_pre" && sh "$SCRIPT" 2>&1 >/dev/null)"
expect "only a pre-release tag fails with the no tag message" "1" "$(cd "$only_pre" && sh "$SCRIPT" >/dev/null 2>&1; echo $?)"
case "$err" in *"no release tag"*) found=yes ;; *) found=no ;; esac
expect "the pre-release failure says no release tag" "yes" "$found"

none="$WORK/untagged"
new_repo "$none"
out="$(cd "$none" && sh "$SCRIPT" 2>/dev/null)"
status=$?
expect "no tag fails" "1" "$status"
expect "no tag prints no version" "" "$out"

# Not a git repository: git's own failure must not be reported as a missing tag.
plain="$WORK/plain"
mkdir "$plain"
err="$(cd "$plain" && GIT_CEILING_DIRECTORIES="$WORK" sh "$SCRIPT" 2>&1 >/dev/null)"
case "$err" in *"no release tag"*) found=yes ;; *) found=no ;; esac
expect "a git failure is not reported as a missing tag" "no" "$found"

if [ "$FAILURES" -ne 0 ]; then
  printf '%s check(s) failed\n' "$FAILURES"
  exit 1
fi
