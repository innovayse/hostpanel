#!/usr/bin/env sh
#
# Runs an image pull with a time limit per attempt and a bounded number of attempts.
#
# A registry download can stall or end half way (EOF, "bad record MAC") on a slow link, and a bare
# `docker compose pull` then either holds the deploy until the job limit or fails it on the first
# error. Layers that already arrived are skipped on a retry, so a second attempt is cheap.
#
# The numbers match the push loop of the image build jobs in .gitlab-ci.yml (two attempts, 900 s each,
# a 10 s pause), so a pull and a push have the same worst case: two attempts of 900 s, a 10 s kill
# grace each and one 10 s pause, about 31 minutes. Change both places together.
#
# Usage:
#   scripts/pull-with-retry.sh <pull command> [arguments]
#   scripts/pull-with-retry.sh docker compose pull hostpanel-api hostpanel-client
#
# Environment (all optional, whole numbers of seconds except the attempts):
#   PULL_ATTEMPTS          attempts before giving up, default 2
#   PULL_TIMEOUT_SECONDS   limit of one attempt, default 900
#   PULL_PAUSE_SECONDS     pause between attempts, default 10
#
# Exit status: 0 the pull succeeded, 1 every attempt failed or timed out, 2 usage.

set -eu

# Seconds a command that ignores the first signal gets before it is killed.
KILL_GRACE_SECONDS=10

attempts="${PULL_ATTEMPTS:-2}"
limit="${PULL_TIMEOUT_SECONDS:-900}"
pause="${PULL_PAUSE_SECONDS:-10}"

if [ "$#" -eq 0 ]; then
  echo "Usage: $0 <pull command> [arguments]" >&2
  exit 2
fi

for setting in "$attempts" "$limit" "$pause"; do
  case "$setting" in
    ''|*[!0-9]*)
      echo "PULL_ATTEMPTS, PULL_TIMEOUT_SECONDS and PULL_PAUSE_SECONDS must be whole numbers, got '$setting'." >&2
      exit 2
      ;;
  esac
done

if [ "$attempts" -lt 1 ]; then
  echo "PULL_ATTEMPTS must be at least 1." >&2
  exit 2
fi

attempt=1
until timeout -k "$KILL_GRACE_SECONDS" "$limit" "$@"; do
  if [ "$attempt" -ge "$attempts" ]; then
    echo "ERROR: '$*' failed after $attempt attempts (each limited to ${limit} seconds)." >&2
    exit 1
  fi
  attempt=$((attempt + 1))
  echo "Pull failed or timed out, attempt $attempt of $attempts in ${pause} seconds"
  sleep "$pause"
done
