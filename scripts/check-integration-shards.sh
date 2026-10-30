#!/usr/bin/env sh
#
# Fails when the Integration test shards of the CI pipelines do not cover the test project exactly.
#
# The Integration suite is split by top-level test folder into shard filters
# (FullyQualifiedName~Integration.Tests.<Folder>.) that live in the pipeline files. A folder too
# heavy for one shard is split by the first letter of its test class names instead
# (FullyQualifiedName~Integration.Tests.<Folder>.<Letter>, one entry per letter A to Z; class names
# are PascalCase, so every class of the folder, sub-namespaces included, starts with one of them).
# A folder named in no filter runs nowhere, a folder or letter named twice runs twice, a split
# folder missing a letter loses those classes, and a name with no folder is a stale entry. Every
# file given on the command line must cover each folder exactly once and name nothing else, so the
# GitHub and the GitLab pipelines also stay equal to each other.
#
# Usage, from the repository root:
#   scripts/check-integration-shards.sh .github/workflows/ci.yml .gitlab-ci.yml

set -eu

tests_dir="backend/tests/Innovayse.Hostpanel.Integration.Tests"
letters="A B C D E F G H I J K L M N O P Q R S T U V W X Y Z"
status=0

if [ "$#" -eq 0 ]; then
  echo "Usage: $0 <pipeline file> [<pipeline file> ...]" >&2
  exit 2
fi

# Folders that hold no tests and so belong to no shard (Fixtures holds fakes only).
folders=$(
  for dir in "$tests_dir"/*/; do
    name=$(basename "$dir")
    case "$name" in
      bin|obj|Fixtures) ;;
      *) echo "$name" ;;
    esac
  done | sort
)

for file in "$@"; do
  # Each entry as "<Folder>" (whole folder) or "<Folder>.<Letter>" (one letter of a split folder).
  entries=$(grep -oE 'Integration\.Tests\.[A-Za-z0-9_]+\.[A-Z]?' "$file" | sed 's/^Integration\.Tests\.//; s/\.$//' | sort)
  whole=$(printf '%s\n' "$entries" | grep -v '\.' || true)
  split=$(printf '%s\n' "$entries" | grep '\.' || true)

  for folder in $folders; do
    count=$(printf '%s\n' "$whole" | grep -cx "$folder" || true)
    parts=$(printf '%s\n' "$split" | grep -c "^$folder\." || true)
    if [ "$count" -gt 0 ] && [ "$parts" -gt 0 ]; then
      echo "ERROR: $file: test folder '$folder' is named whole and by letter, so its tests run more than once." >&2
      status=1
    elif [ "$count" -gt 1 ]; then
      echo "ERROR: $file: test folder '$folder' is in $count shards, so its tests run more than once." >&2
      status=1
    elif [ "$count" -eq 0 ] && [ "$parts" -eq 0 ]; then
      echo "ERROR: $file: test folder '$folder' is in no shard, so its tests run nowhere." >&2
      status=1
    elif [ "$parts" -gt 0 ]; then
      for letter in $letters; do
        n=$(printf '%s\n' "$split" | grep -cx "$folder\.$letter" || true)
        if [ "$n" -eq 0 ]; then
          echo "ERROR: $file: split test folder '$folder' has no shard for letter '$letter', so those classes run nowhere." >&2
          status=1
        elif [ "$n" -gt 1 ]; then
          echo "ERROR: $file: split test folder '$folder' names letter '$letter' in $n shards, so those classes run more than once." >&2
          status=1
        fi
      done
    fi
  done

  for name in $(printf '%s\n' "$entries" | sed 's/\..*$//' | sort -u); do
    if ! printf '%s\n' "$folders" | grep -qx "$name"; then
      echo "ERROR: $file: shard entry '$name' has no matching test folder." >&2
      status=1
    fi
  done
done

if [ "$status" -eq 0 ]; then
  echo "Every Integration test folder is in exactly one shard in: $*"
fi
exit "$status"
