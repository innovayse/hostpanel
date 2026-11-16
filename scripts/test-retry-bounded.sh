#!/usr/bin/env sh
#
# Tests scripts/retry-bounded.sh with fake commands: a command that succeeds is run once, a failed or
# stalled one is retried and the run continues, one that keeps failing ends with a message naming how the
# last attempt ended after the bounded attempts, a command that ignores the first signal is killed, the
# pause is kept, arguments with spaces arrive intact, a command that cannot run is not retried, and a bad
# setting or a missing command is refused.
#
# Usage: sh scripts/test-retry-bounded.sh

set -u

# The script reads these; a value left over in the caller's environment must not change a check.
unset RETRY_ATTEMPTS RETRY_TIMEOUT_SECONDS RETRY_PAUSE_SECONDS

SCRIPT="$(cd "$(dirname "$0")" && pwd)/retry-bounded.sh"
WORK="$(mktemp -d)"
trap 'rm -rf "${WORK:?}"' EXIT

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

# Writes a fake command $1 that counts its runs in $WORK/$1.count and, from run number $2 on, succeeds.
# Runs before it fail ($3 = fail), stall ($3 = stall) or stall and ignore the first signal ($3 = deaf).
fake_command() {
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
if [ "$3" = "deaf" ]; then
  trap '' TERM
  sleep 30
  sleep 30
fi
exit 1
FAKE
  chmod +x "$WORK/$1"
}

# Runs the script with the environment as it is. Prints the exit status; the output is in $WORK/out and $WORK/err.
run_raw() {
  sh "$SCRIPT" "$@" >"$WORK/out" 2>"$WORK/err"
  echo $?
}

# Runs the script with one second limits and no pause, unless the call sets its own. Prints the exit status.
run() {
  (
    RETRY_TIMEOUT_SECONDS="${RETRY_TIMEOUT_SECONDS:-1}"
    RETRY_PAUSE_SECONDS="${RETRY_PAUSE_SECONDS:-0}"
    export RETRY_ATTEMPTS RETRY_TIMEOUT_SECONDS RETRY_PAUSE_SECONDS
    run_raw "$@"
  )
}

# Prints yes when the file $1 contains the text $2.
file_has() {
  case "$(cat "$1")" in *"$2"*) echo yes ;; *) echo no ;; esac
}

fake_command ok 1 fail
expect "a command that succeeds exits 0" "0" "$(run "$WORK/ok")"
expect "a command that succeeds is run once" "1" "$(cat "$WORK/ok.count")"

fake_command flaky 2 fail
expect "a failed command is retried and the run succeeds" "0" "$(run "$WORK/flaky")"
expect "the failed command ran twice" "2" "$(cat "$WORK/flaky.count")"

fake_command stalled 2 stall
expect "a stalled command is cut off, retried and the run succeeds" "0" "$(run "$WORK/stalled")"
expect "the stalled command ran twice" "2" "$(cat "$WORK/stalled.count")"

fake_command broken 99 fail
expect "a command that keeps failing exits 1" "1" "$(run "$WORK/broken")"
expect "the default is two attempts" "2" "$(cat "$WORK/broken.count")"
expect "the final message says how many attempts failed" "yes" "$(file_has "$WORK/err" "failed after 2 attempts")"
expect "the final message names the exit status of a failure" "yes" "$(file_has "$WORK/err" "exit status 1")"

fake_command hung 99 stall
expect "a command that always stalls exits 1" "1" "$(RETRY_ATTEMPTS=1 run "$WORK/hung")"
expect "the final message says the last attempt timed out" "yes" "$(file_has "$WORK/err" "timed out or was killed (status 124)")"

# A command that ignores SIGTERM is only stopped by the kill that follows the grace period. The grace is
# ten seconds, so this check takes about eleven, and a missing -k would leave it running for the full minute.
fake_command deaf 99 deaf
started="$(date +%s)"
expect "a command that ignores the first signal is killed and exits 1" "1" "$(RETRY_ATTEMPTS=1 run "$WORK/deaf")"
elapsed=$(($(date +%s) - started))
expect "the kill came within the grace period" "yes" "$([ "$elapsed" -lt 30 ] && echo yes || echo no)"
expect "the kill is reported as a timeout" "yes" "$(file_has "$WORK/err" "timed out or was killed (status 137)")"

# A command that cannot run is not retried: not found (127) and not executable (126) end the run at once.
expect "a missing command exits 1" "1" "$(RETRY_ATTEMPTS=3 run "$WORK/no-such-command")"
expect "a missing command is reported as one that cannot run" "yes" "$(file_has "$WORK/err" "cannot run (status 127)")"
expect "a missing command is not retried" "no" "$(file_has "$WORK/out" "Attempt failed")"
printf '#!/usr/bin/env sh\n' > "$WORK/not-executable"
expect "a command that is not executable exits 1" "1" "$(RETRY_ATTEMPTS=3 run "$WORK/not-executable")"
expect "a command that is not executable is reported as one that cannot run" "yes" "$(file_has "$WORK/err" "cannot run (status 126)")"

rm -f "$WORK/broken.count"
expect "RETRY_ATTEMPTS sets the bound" "1" "$(RETRY_ATTEMPTS=4 run "$WORK/broken")"
expect "four attempts were made" "4" "$(cat "$WORK/broken.count")"

# The pause between attempts is kept: two attempts with a two second pause take at least two seconds.
rm -f "$WORK/broken.count"
started="$(date +%s)"
RETRY_PAUSE_SECONDS=2 run "$WORK/broken" >/dev/null
elapsed=$(($(date +%s) - started))
expect "the pause between attempts is kept" "yes" "$([ "$elapsed" -ge 2 ] && echo yes || echo no)"

# The arguments reach the command unchanged, one with spaces included.
cat > "$WORK/echo-args" <<FAKE
#!/usr/bin/env sh
printf '%s|' "\$@" > "$WORK/args.seen"
FAKE
chmod +x "$WORK/echo-args"
sh "$SCRIPT" "$WORK/echo-args" compose "a b  c" pull >/dev/null 2>&1
expect "the arguments reach the command unchanged" "compose|a b  c|pull|" "$(cat "$WORK/args.seen")"

expect "no command is a usage error" "2" "$(run)"
expect "a non numeric limit is a usage error" "2" "$(RETRY_TIMEOUT_SECONDS=soon run "$WORK/ok")"
expect "a zero limit is a usage error" "2" "$(RETRY_TIMEOUT_SECONDS=0 run "$WORK/ok")"
expect "a zero limit is not run" "yes" "$([ "$(cat "$WORK/ok.count")" = "1" ] && echo yes || echo no)"
expect "zero attempts is a usage error" "2" "$(RETRY_ATTEMPTS=0 run "$WORK/ok")"
expect "a non numeric pause is a usage error" "2" "$(RETRY_PAUSE_SECONDS=-1 run "$WORK/ok")"

if [ "$FAILURES" -ne 0 ]; then
  printf '%s check(s) failed\n' "$FAILURES"
  exit 1
fi
