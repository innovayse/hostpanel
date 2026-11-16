#!/usr/bin/env sh
#
# Tests the marker handling of the deploy:prod job in .gitlab-ci.yml, taken from the file itself and run
# against a fake docker and git:
#   - before_script removes the markers of an earlier deploy before anything else can fail (a missing
#     DEPLOY_DIR, a failed registry login), so a stale marker never makes a later failure roll back;
#   - after_script leaves the running stack alone when the deploy failed before the fetch, only restores the
#     checkout when it failed after the fetch but before any container was recreated, rolls back fully once
#     the containers were being recreated (both restores clear stale submodule folders first), does nothing
#     on a failure with nothing recorded, and removes the markers on success and on failure.
#
# The shared fetch loop (.fetch-target-commit) is tested the same way: the named git locks (shallow.lock, index.lock,
# HEAD.lock) are removed before the first attempt and after a killed one, any other lock name is kept, and a fetch
# that cannot run (status 125 to 127) is not retried.
#
# It also checks the order of the deploy script: the fetch, the fetched marker and the checkout, then the retried
# pull, the recreating marker and the start of the containers.
#
# Usage: sh scripts/test-deploy-rollback.sh

set -u

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
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

# Prints the commands of the one `- |` item of the key $1 (script, before_script or after_script) of the job $2,
# default deploy:prod, without the YAML indentation. A comment line indented by four spaces or less (between the
# items, or before the next key) belongs to no item and never ends the key; one indented deeper is a line of the
# item's own script. Prints nothing and fails when the key does not hold exactly one such item.
# With $3 set to `all` it prints instead every line of the key that is not a comment, indentation kept, whatever
# the number of items, for a key made of several items such as the script of deploy:prod.
block() {
  awk -v key="$1" -v name="${2:-deploy:prod}" -v all="${3:-}" '
    { sub(/[[:cntrl:]]$/, "") }
    $0 == name ":" { job = 1; next }
    job && /^[^ #]/ { job = 0; inside = 0 }
    job && $0 == "  " key ":" { inside = 1; next }
    inside && /^ *#/ { match($0, /^ */); if (RLENGTH <= 4 || all == "all") { next } }
    inside && /^  [^ ]/ { inside = 0 }
    inside && all == "all" { print; next }
    inside && /^    - / { items++; piped = ($0 == "    - |"); next }
    inside && items == 1 && piped { sub(/^      /, ""); body = body $0 "\n"; next }
    END { if (all != "all" && (items != 1 || !piped)) { exit 1 } printf "%s", body }
  ' "$ROOT/.gitlab-ci.yml"
}

# Takes a block of the job into the file $2, then checks it was found.
extract() {
  if block "$1" > "$WORK/$2"; then
    found=yes
  else
    found=no
  fi
  expect "$1 of deploy:prod is exactly one block in .gitlab-ci.yml" "yes" "$found"
  expect "$1 of deploy:prod is not empty" "yes" "$([ -s "$WORK/$2" ] && echo yes || echo no)"
}

extract before_script before.sh
extract after_script handler.sh

# Prints yes when each text given after the file $1 is found, as a fixed string, on a line of that file, the
# second after the line of the first and so on.
in_order() {
  file="$1"
  shift
  position=0
  for text in "$@"; do
    position="$(PATTERN="$text" awk -v start="$position" 'NR > start && index($0, ENVIRON["PATTERN"]) { print NR; exit }' "$file")"
    if [ -z "$position" ]; then
      echo no
      return
    fi
  done
  echo yes
}

# The markers only protect the deploy when they are written at these points, so the order of the script is
# checked: the fetch, then the fetched marker, then the checkout; the pull through the shared retry script, then
# the recreating marker, then the start of the containers.
block script deploy:prod all > "$WORK/script.txt"
expect "the deploy script writes the fetched marker between the fetch and the checkout" "yes" \
  "$(in_order "$WORK/script.txt" '!reference [.fetch-target-commit, script]' 'touch "$DEPLOY_DIR/.deploy/fetched"' 'checkout -f "$CI_COMMIT_SHA"')"
expect "the deploy script writes the recreating marker between the retried pull and the start" "yes" \
  "$(in_order "$WORK/script.txt" 'scripts/retry-bounded.sh docker compose pull' 'touch "$DEPLOY_DIR/.deploy/recreating"' 'compose up -d --no-build')"

# Runs block() on the job text $2 as if it were the .gitlab-ci.yml, with the key $1. Prints yes when it found
# exactly one `- |` item. Called inside $(...), so the changed ROOT never leaks out.
block_of() {
  mkdir -p "$WORK/fake-root"
  printf '%s\n' "$2" > "$WORK/fake-root/.gitlab-ci.yml"
  ROOT="$WORK/fake-root"
  if block "$1" >/dev/null; then echo yes; else echo no; fi
}

# The extraction must fail on a key with two items or with a plain item, or the checks above prove nothing, and a
# comment between the items must not hide the second one.
expect "two items in after_script are refused" "no" "$(block_of after_script 'deploy:prod:
  after_script:
    - |
      echo one
    - echo two')"
expect "a two space comment between two items does not hide the second one" "no" "$(block_of after_script 'deploy:prod:
  after_script:
    - |
      echo one
  # a comment between the items
    - echo two')"
expect "a two space comment after the only item keeps it a single block" "yes" "$(block_of after_script 'deploy:prod:
  after_script:
    - |
      echo one
  # a comment before the next key
  environment: x')"

# Fake docker and git that only record what they were asked to do. The fake docker fails a login when
# $WORK/login-fails exists. The fake git fails a checkout when $WORK/checkout-fails exists, and a checkout drops
# the clear script from the deploy directory when $WORK/checkout-drops-clear exists, the way a commit from before
# that script existed would.
mkdir "$WORK/bin"
cat > "$WORK/bin/docker" <<FAKE
#!/usr/bin/env sh
echo "docker \$*" >> "$WORK/calls"
if [ "\$1" = "login" ] && [ -e "$WORK/login-fails" ]; then
  exit 1
fi
FAKE
cat > "$WORK/bin/git" <<FAKE
#!/usr/bin/env sh
echo "git \$*" >> "$WORK/calls"
if [ "\$1" = "checkout" ]; then
  if [ -e "$WORK/checkout-fails" ]; then
    exit 1
  fi
  if [ -e "$WORK/checkout-drops-clear" ]; then
    rm -f "$WORK/deploy/scripts/clear-stale-submodule-dirs.sh"
  fi
fi
FAKE
chmod +x "$WORK/bin/docker" "$WORK/bin/git"

# Prepares the deploy directory with the recorded state and a fake health check.
# $1 yes when the fetched marker exists, $2 yes when the recreating marker exists, $3 yes when images were recorded.
prepare() {
  rm -rf "$WORK/deploy" "$WORK/calls" "$WORK/login-fails" "$WORK/checkout-fails" "$WORK/checkout-drops-clear"
  mkdir -p "$WORK/deploy/.deploy" "$WORK/deploy/scripts"
  : > "$WORK/calls"
  echo "abc123" > "$WORK/deploy/.deploy/previous-commit"
  : > "$WORK/deploy/.deploy/previous-images"
  if [ "$3" = "yes" ]; then
    echo "hostpanel-api hostpanel-rollback/hostpanel-api:previous" > "$WORK/deploy/.deploy/previous-images"
  fi
  if [ "$1" = "yes" ]; then
    : > "$WORK/deploy/.deploy/fetched"
  fi
  if [ "$2" = "yes" ]; then
    : > "$WORK/deploy/.deploy/recreating"
  fi
  echo 'echo "health $*" >> "'"$WORK/calls"'"' > "$WORK/deploy/scripts/wait-for-health.sh"
  echo 'echo "clear $*" >> "'"$WORK/calls"'"' > "$WORK/deploy/scripts/clear-stale-submodule-dirs.sh"
}

# Prints yes when the recorded calls hold a `clear` call right before a `git submodule update`, as the forward path does.
clear_precedes_update() {
  awk '/^clear / { cleared = 1; next } /^git submodule update/ { if (cleared) { found = 1 } } { cleared = 0 } END { print found ? "yes" : "no" }' "$WORK/calls"
}

# Runs the after_script with the job status $1. Prints its exit status; the output is in $WORK/out.
run_handler() {
  PATH="$WORK/bin:$PATH" DEPLOY_DIR="$WORK/deploy" CI_JOB_STATUS="$1" CI_REGISTRY=registry.example \
    IMAGE_REGISTRY=registry.example/hostpanel IMAGE_TAG=tag HEALTH_URL=http://health.example HEALTH_TIMEOUT_SECONDS=1 \
    sh "$WORK/handler.sh" >"$WORK/out" 2>&1
  echo $?
}

# Runs the before_script with DEPLOY_DIR $1. Prints its exit status.
run_before() {
  PATH="$WORK/bin:$PATH" DEPLOY_DIR="$1" CI_JOB_TOKEN=token CI_REGISTRY=registry.example \
    sh "$WORK/before.sh" >"$WORK/out" 2>&1
  echo $?
}

# Counts the recorded calls that match the pattern $1.
calls_matching() {
  grep -c -E "$1" "$WORK/calls" || true
}

# Prints yes when the file $1 exists, no when it does not.
exists() {
  [ -e "$1" ] && echo yes || echo no
}

# Prints yes when the output of the last run contains the text $1.
output_has() {
  case "$(cat "$WORK/out")" in *"$1"*) echo yes ;; *) echo no ;; esac
}

# ── before_script: stale markers never survive the start of a job ─────────────────────────────────
prepare yes yes yes
expect "the before_script succeeds with a good login" "0" "$(run_before "$WORK/deploy")"
expect "the before_script removes a stale fetched marker" "no" "$(exists "$WORK/deploy/.deploy/fetched")"
expect "the before_script removes a stale recreating marker" "no" "$(exists "$WORK/deploy/.deploy/recreating")"
expect "the before_script keeps what was recorded" "yes" "$(exists "$WORK/deploy/.deploy/previous-commit")"

prepare yes yes yes
: > "$WORK/login-fails"
expect "a failed registry login fails the before_script" "1" "$(run_before "$WORK/deploy")"
expect "the stale fetched marker is gone even when the login fails" "no" "$(exists "$WORK/deploy/.deploy/fetched")"
expect "the stale recreating marker is gone even when the login fails" "no" "$(exists "$WORK/deploy/.deploy/recreating")"
expect "the handler after a failed login leaves the stack alone" "0" "$(run_handler failed)"
expect "the handler after a failed login recreates nothing" "0" "$(calls_matching 'compose|checkout|tag|health')"

rm -rf "$WORK/fresh" "$WORK/login-fails"
: > "$WORK/calls"
expect "an unset DEPLOY_DIR fails the before_script" "1" "$(run_before "")"
expect "an unset DEPLOY_DIR is named in the message" "yes" "$(output_has "DEPLOY_DIR must be set")"
expect "an unset DEPLOY_DIR does not reach the registry login" "0" "$(calls_matching 'login')"
expect "a new DEPLOY_DIR gets its .deploy folder" "0" "$(run_before "$WORK/fresh")"
expect "the .deploy folder exists" "yes" "$(exists "$WORK/fresh/.deploy")"

# ── after_script ──────────────────────────────────────────────────────────────────────────────────
prepare no no yes
expect "a failure before the fetch ends cleanly" "0" "$(run_handler failed)"
expect "a failure before the fetch recreates nothing" "0" "$(calls_matching 'compose|checkout|tag|health')"
expect "a failure before the fetch says why the stack is left alone" "yes" "$(output_has "before the target commit was fetched")"

prepare yes no yes
expect "a failure after the fetch only ends cleanly" "0" "$(run_handler failed)"
expect "a failure after the fetch only checks out the previous commit" "1" "$(calls_matching 'git checkout -f abc123')"
expect "a failure after the fetch only syncs the submodules" "1" "$(calls_matching 'git submodule update')"
expect "a failure after the fetch only clears stale submodule folders before the update" "yes" "$(clear_precedes_update)"
expect "a failure after the fetch only recreates nothing" "0" "$(calls_matching 'compose|tag|health')"
expect "a failure after the fetch only says no container was recreated" "yes" "$(output_has "before any container was recreated")"
expect "a failure after the fetch only removes the markers" "no" "$(exists "$WORK/deploy/.deploy/fetched")"

prepare yes no no
expect "a failure after the fetch with nothing recorded ends cleanly" "0" "$(run_handler failed)"
expect "a failure after the fetch with nothing recorded still restores the checkout" "1" "$(calls_matching 'git checkout -f abc123')"

prepare yes yes yes
expect "a failure while recreating ends cleanly" "0" "$(run_handler failed)"
expect "a failure while recreating checks out the previous commit" "1" "$(calls_matching 'git checkout -f abc123')"
expect "a failure while recreating clears stale submodule folders before the update" "yes" "$(clear_precedes_update)"
expect "a failure while recreating recreates the recorded service" "1" "$(calls_matching 'compose up -d --no-deps --force-recreate --no-build hostpanel-api')"
expect "a failure while recreating waits for the site" "1" "$(calls_matching 'health')"
expect "a failure while recreating removes the fetched marker" "no" "$(exists "$WORK/deploy/.deploy/fetched")"
expect "a failure while recreating removes the recreating marker" "no" "$(exists "$WORK/deploy/.deploy/recreating")"

prepare yes no yes
: > "$WORK/checkout-drops-clear"
expect "a failure after the fetch, with no clear script in the previous commit, ends cleanly" "0" "$(run_handler failed)"
expect "a failure after the fetch, with no clear script in the previous commit, still updates the submodules" "1" "$(calls_matching 'git submodule update')"
expect "a failure after the fetch, with no clear script in the previous commit, says the clearing is skipped" "yes" "$(output_has "clearing stale submodule folders is skipped")"
expect "a failure after the fetch, with no clear script in the previous commit, warns of nothing" "no" "$(output_has "WARNING")"

prepare yes yes yes
: > "$WORK/checkout-drops-clear"
expect "a rollback to a commit without the clear script ends cleanly" "0" "$(run_handler failed)"
expect "a rollback to a commit without the clear script says the clearing is skipped" "yes" "$(output_has "clearing stale submodule folders is skipped")"
expect "a rollback to a commit without the clear script still updates the submodules" "1" "$(calls_matching 'git submodule update')"
expect "a rollback to a commit without the clear script still recreates the recorded service" "1" "$(calls_matching 'compose up -d --no-deps --force-recreate --no-build hostpanel-api')"
expect "a rollback to a commit without the clear script still waits for the site" "1" "$(calls_matching 'health')"

prepare yes no yes
: > "$WORK/checkout-fails"
expect "a failed checkout restore ends cleanly" "0" "$(run_handler failed)"
expect "a failed checkout restore warns clearly" "yes" "$(output_has "WARNING: the checkout could not be restored to abc123")"
expect "a failed checkout restore stops before the submodules" "0" "$(calls_matching 'git submodule')"

prepare yes yes yes
: > "$WORK/checkout-fails"
expect "a failed checkout restore during a rollback ends cleanly" "0" "$(run_handler failed)"
expect "a failed checkout restore during a rollback warns clearly" "yes" "$(output_has "WARNING: the checkout could not be restored to abc123")"
expect "a failed checkout restore during a rollback still recreates the recorded service" "1" "$(calls_matching 'compose up -d --no-deps --force-recreate --no-build hostpanel-api')"

prepare yes yes no
expect "a failure with nothing recorded ends cleanly" "0" "$(run_handler failed)"
expect "a failure with nothing recorded changes nothing" "0" "$(calls_matching 'compose|checkout|tag|health')"

prepare yes yes yes
expect "a successful job ends cleanly" "0" "$(run_handler success)"
expect "a successful job rolls nothing back" "0" "$(calls_matching 'compose|checkout|tag|health')"
expect "a successful job removes the fetched marker" "no" "$(exists "$WORK/deploy/.deploy/fetched")"
expect "a successful job removes the recreating marker" "no" "$(exists "$WORK/deploy/.deploy/recreating")"

# ── .fetch-target-commit: the bounded fetch loop ──────────────────────────────────────────────────
# A fake timeout stands in for `timeout ... git fetch`: it answers with the next status of $WORK/fetch-statuses
# (one per line, 0 when the list is used up), records whether the shallow lock existed when it was called, and
# leaves the shallow and index locks behind when it fails, the way a killed or failed git does. A fake sleep keeps
# the pauses out of the test.
if block script .fetch-target-commit > "$WORK/fetch.sh"; then found=yes; else found=no; fi
expect ".fetch-target-commit script is exactly one block in .gitlab-ci.yml" "yes" "$found"
cat > "$WORK/bin/timeout" <<FAKE
#!/usr/bin/env sh
count=\$(( \$(cat "$WORK/fetch-count" 2>/dev/null || echo 0) + 1 ))
echo "\$count" > "$WORK/fetch-count"
echo "fetch \$count lock=\$([ -e "$WORK/deploy/.git/shallow.lock" ] && echo yes || echo no)" >> "$WORK/calls"
status=\$(sed -n "\${count}p" "$WORK/fetch-statuses")
if [ "\${status:-0}" -ne 0 ]; then
  : > "$WORK/deploy/.git/shallow.lock"
  : > "$WORK/deploy/.git/index.lock"
fi
exit "\${status:-0}"
FAKE
printf '#!/usr/bin/env sh\nexit 0\n' > "$WORK/bin/sleep"
chmod +x "$WORK/bin/timeout" "$WORK/bin/sleep"

# Runs the fetch loop with the fetch statuses $1 (space separated), the stale locks of an earlier job (the three
# named ones and packed-refs.lock, which is not named), and a config file in the .git of the deploy directory.
# Prints the exit status of the loop.
run_fetch() {
  prepare no no no
  rm -f "$WORK/fetch-count"
  mkdir -p "$WORK/deploy/.git"
  : > "$WORK/deploy/.git/shallow.lock"
  : > "$WORK/deploy/.git/index.lock"
  : > "$WORK/deploy/.git/HEAD.lock"
  : > "$WORK/deploy/.git/packed-refs.lock"
  : > "$WORK/deploy/.git/config"
  printf '%s\n' $1 > "$WORK/fetch-statuses"
  PATH="$WORK/bin:$PATH" DEPLOY_DIR="$WORK/deploy" CI_REPOSITORY_URL=https://git.example/repo CI_COMMIT_SHA=abc123 \
    sh "$WORK/fetch.sh" >"$WORK/out" 2>&1
  echo $?
}

expect "a fetch that succeeds ends cleanly" "0" "$(run_fetch "0")"
expect "a fetch that succeeds runs once" "1" "$(calls_matching '^fetch ')"
expect "the first attempt starts without the stale shallow lock of an earlier job" "1" "$(calls_matching '^fetch 1 lock=no')"
expect "the stale index lock of an earlier job is removed before the first attempt" "no" "$(exists "$WORK/deploy/.git/index.lock")"
expect "the stale HEAD lock of an earlier job is removed before the first attempt" "no" "$(exists "$WORK/deploy/.git/HEAD.lock")"
expect "a lock with another name stays before the first attempt" "yes" "$(exists "$WORK/deploy/.git/packed-refs.lock")"
expect "a file of .git that is not a lock stays before the first attempt" "yes" "$(exists "$WORK/deploy/.git/config")"

expect "a fetch killed once is retried and ends cleanly" "0" "$(run_fetch "124 0")"
expect "a fetch killed once runs twice" "2" "$(calls_matching '^fetch ')"
expect "the second attempt after a kill starts without the stale lock" "1" "$(calls_matching '^fetch 2 lock=no')"
expect "the stale index lock is removed after a kill" "no" "$(exists "$WORK/deploy/.git/index.lock")"
expect "a file of .git that is not a lock stays" "yes" "$(exists "$WORK/deploy/.git/config")"
expect "a lock with another name stays after a kill" "yes" "$(exists "$WORK/deploy/.git/packed-refs.lock")"

expect "a fetch killed with status 137 is retried" "0" "$(run_fetch "137 0")"
expect "a fetch killed with status 137 removes the stale lock" "1" "$(calls_matching '^fetch 2 lock=no')"

expect "a fetch that fails on its own is retried" "0" "$(run_fetch "1 0")"
expect "a fetch that fails on its own keeps the lock files alone" "1" "$(calls_matching '^fetch 2 lock=yes')"

expect "a fetch that cannot run (125) stops at once" "1" "$(run_fetch "125 0")"
expect "a fetch that cannot run (125) runs once" "1" "$(calls_matching '^fetch ')"
expect "a fetch that cannot run (127) stops at once" "1" "$(run_fetch "127 0")"
expect "a fetch that cannot run (127) runs once" "1" "$(calls_matching '^fetch ')"
expect "a fetch that cannot run names its status" "yes" "$(output_has "cannot run (status 127)")"

expect "a fetch that always fails ends after four attempts" "1" "$(run_fetch "1 1 1 1 1")"
expect "a fetch that always fails runs four times" "4" "$(calls_matching '^fetch ')"

if [ "$FAILURES" -ne 0 ]; then
  printf '%s check(s) failed\n' "$FAILURES"
  exit 1
fi
