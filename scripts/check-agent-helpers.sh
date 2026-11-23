#!/bin/sh
# Checks the shell helpers of the SSH key agent (backend/agent/ssh-key-agent/providers/*): `bash -n` on every file and
# shellcheck on each provider's files together (the helpers source their hp-common.sh). Run from anywhere; used by the
# agent job of the GitHub workflow and of .gitlab-ci.yml.
set -eu
root="$(cd "$(dirname "$0")/.." && pwd)"
for dir in "$root"/backend/agent/ssh-key-agent/providers/*/; do
  cd "$dir"
  for file in *; do
    bash -n "$file"
  done
  shellcheck -x -s bash ./*
  echo "ok: ${dir#"$root"/}"
done
