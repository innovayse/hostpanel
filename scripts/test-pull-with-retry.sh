#!/usr/bin/env sh
#
# Tests scripts/pull-with-retry.sh with fake pull commands: a pull that succeeds is run once, a failed
# or stalled pull is retried and the run continues, a pull that keeps failing ends with a clear message
# after the bounded attempts, and a bad setting or a missing command is refused.
#
# Usage: sh scripts/test-pull-with-retry.sh

set -u

SCRIPT="$(cd "$(dirname "$0")" && pwd)/pull-with-retry.sh"
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

# Writes a fake pull command $1 that counts its runs in $WORK/$1.count and, on run number $2, succeeds.
# Runs before it fail ($3 = fail) or stall ($3 = stall).
fake_pull() {
  cat > "$WORK/$1" <<FAKE
#!/usr/bin/env sh
runs=\$(cat "$WORK/$1.count" 2>/dev/null || echo 0)
runs=\$((runs + 1))
echo "\$runs" > "$WORK/$1.count"
if [ "\$runs" -ge "$2" ]; then
  exit 0
fi
if [ "$3" = "stall" ]; then
  exec sleep 30
fi
exit 1
FAKE
  chmod +x "$WORK/$1"
}

# Runs the script with the environment as it is. Prints the exit status.
run_raw() {
  sh "$SCRIPT" "$@" >"$WORK/out" 2>"$WORK/err"
  echo $?
}

# Runs the script with one second limits and no pause, unless the caller exported its own. Prints the exit status.
run() {
  PULL_TIMEOUT_SECONDS="${PULL_TIMEOUT_SECONDS:-1}"
  PULL_PAUSE_SECONDS=0
  export PULL_TIMEOUT_SECONDS PULL_PAUSE_SECONDS
  run_raw "$@"
}

fake_pull ok 1 fail
expect "a pull that succeeds exits 0" "0" "$(run "$WORK/ok")"
expect "a pull that succeeds is run once" "1" "$(cat "$WORK/ok.count")"

fake_pull flaky 2 fail
expect "a failed pull is retried and the run succeeds" "0" "$(run "$WORK/flaky")"
expect "the failed pull ran twice" "2" "$(cat "$WORK/flaky.count")"

fake_pull stalled 2 stall
expect "a stalled pull is cut off, retried and the run succeeds" "0" "$(run "$WORK/stalled")"
expect "the stalled pull ran twice" "2" "$(cat "$WORK/stalled.count")"

fake_pull broken 99 fail
expect "a pull that keeps failing exits 1" "1" "$(run "$WORK/broken")"
expect "the default is two attempts" "2" "$(cat "$WORK/broken.count")"
case "$(cat "$WORK/err")" in *"failed after 2 attempts"*) found=yes ;; *) found=no ;; esac
expect "the final message says how many attempts failed" "yes" "$found"

rm -f "$WORK/broken.count"
expect "PULL_ATTEMPTS sets the bound" "1" "$(PULL_ATTEMPTS=4; export PULL_ATTEMPTS; run "$WORK/broken")"
expect "four attempts were made" "4" "$(cat "$WORK/broken.count")"

cat > "$WORK/echo-args" <<FAKE
#!/usr/bin/env sh
echo "\$*" > "$WORK/args.seen"
FAKE
chmod +x "$WORK/echo-args"
sh "$SCRIPT" "$WORK/echo-args" compose pull api >/dev/null 2>&1
expect "the arguments reach the pull command unchanged" "compose pull api" "$(cat "$WORK/args.seen")"

expect "no command is a usage error" "2" "$(run)"
expect "a non numeric limit is a usage error" "2" "$(PULL_TIMEOUT_SECONDS=soon; export PULL_TIMEOUT_SECONDS; run "$WORK/ok")"
expect "zero attempts is a usage error" "2" "$(PULL_ATTEMPTS=0; export PULL_ATTEMPTS; run "$WORK/ok")"

if [ "$FAILURES" -ne 0 ]; then
  printf '%s check(s) failed\n' "$FAILURES"
  exit 1
fi
