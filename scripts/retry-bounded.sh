#!/usr/bin/env sh
#
# Runs a command with a time limit per attempt and a bounded number of attempts.
#
# A transfer over a slow link (an image pull or push) can stall or end half way (EOF, "bad record MAC",
# "context canceled"), and the bare command then either holds the job until its limit or fails it on the
# first error. Layers that already arrived are skipped on a retry, so a second attempt is cheap.
#
# The numbers live here and nowhere else. A call that needs other ones sets them for that call only:
#   RETRY_ATTEMPTS=4 scripts/retry-bounded.sh <command>
# The worst case of a call is ATTEMPTS * (TIMEOUT + KILL_GRACE) + (ATTEMPTS - 1) * PAUSE seconds, which is
# 2 * (900 + 10) + 10 = 1830 seconds (about 31 minutes) with the defaults.
#
# Usage:
#   scripts/retry-bounded.sh <command> [arguments]
#   scripts/retry-bounded.sh docker compose pull hostpanel-api hostpanel-client
#
# Environment (all optional, whole numbers):
#   RETRY_ATTEMPTS          attempts before giving up, at least 1, default 2
#   RETRY_TIMEOUT_SECONDS   limit of one attempt, at least 1, default 900
#   RETRY_PAUSE_SECONDS     pause between attempts, 0 or more, default 10
#
# Exit status: 0 the command succeeded, 1 every attempt failed or timed out, or the command cannot run, 2 usage.
# A command that cannot run (status 125 to 127: the timeout itself failed, the command is not executable or was
# not found) is not retried, a second attempt would end the same way. The last message on a failure names how
# the final attempt ended: "timed out or was killed" (status 124 or 137), "cannot run" or "exit status N".

set -eu

# Seconds a command that ignores the first signal gets before it is killed.
KILL_GRACE_SECONDS=10

attempts="${RETRY_ATTEMPTS:-2}"
limit="${RETRY_TIMEOUT_SECONDS:-900}"
pause="${RETRY_PAUSE_SECONDS:-10}"

if [ "$#" -eq 0 ]; then
  echo "Usage: $0 <command> [arguments]" >&2
  exit 2
fi

for setting in "$attempts" "$limit" "$pause"; do
  case "$setting" in
    ''|*[!0-9]*)
      echo "RETRY_ATTEMPTS, RETRY_TIMEOUT_SECONDS and RETRY_PAUSE_SECONDS must be whole numbers, got '$setting'." >&2
      exit 2
      ;;
  esac
done

# A limit of 0 would switch the timeout off, and 0 attempts would never run the command.
if [ "$attempts" -lt 1 ] || [ "$limit" -lt 1 ]; then
  echo "RETRY_ATTEMPTS and RETRY_TIMEOUT_SECONDS must be at least 1, got $attempts and $limit." >&2
  exit 2
fi

attempt=1
while :; do
  status=0
  timeout -k "$KILL_GRACE_SECONDS" "$limit" "$@" || status=$?
  if [ "$status" -eq 0 ]; then
    exit 0
  fi
  case "$status" in
    125|126|127)
      echo "ERROR: '$*' cannot run (status $status), not retried." >&2
      exit 1
      ;;
    124|137) how="timed out or was killed (status $status)" ;;
    *) how="exit status $status" ;;
  esac
  if [ "$attempt" -ge "$attempts" ]; then
    echo "ERROR: '$*' failed after $attempt attempts, the last one with $how." >&2
    exit 1
  fi
  attempt=$((attempt + 1))
  echo "Attempt failed ($how), attempt $attempt of $attempts in ${pause} seconds"
  sleep "$pause"
done
