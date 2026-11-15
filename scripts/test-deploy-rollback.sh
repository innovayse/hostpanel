#!/usr/bin/env sh
#
# Tests the marker handling of the deploy:prod job in .gitlab-ci.yml, taken from the file itself and run
# against a fake docker and git:
#   - before_script removes the markers of an earlier deploy before anything else can fail (a missing
#     DEPLOY_DIR, a failed registry login), so a stale marker never makes a later failure roll back;
#   - after_script leaves the running stack alone when the deploy failed before the fetch, only restores the
#     checkout when it failed after the fetch but before any container was recreated, rolls back fully once
#     the containers were being recreated, does nothing on a failure with nothing recorded, and removes the
#     markers on success and on failure.
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

# Prints the commands of the one `- |` item of the key $1 (before_script or after_script) of deploy:prod,
# without the YAML indentation. Prints nothing and fails when the key does not hold exactly one such item.
block() {
  awk -v key="$1" '
    { sub(/[[:cntrl:]]$/, "") }
    /^deploy:prod:/ { job = 1; next }
    job && /^[^ #]/ { job = 0 }
    job && $0 == "  " key ":" { inside = 1; next }
    inside && /^  [^ ]/ { inside = 0 }
    inside && /^    - / { items++; piped = ($0 == "    - |"); next }
    inside && items == 1 && piped { sub(/^      /, ""); body = body $0 "\n"; next }
    END { if (items != 1 || !piped) { exit 1 } printf "%s", body }
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

# The extraction must fail on a key with two items or with a plain item, or the checks above prove nothing.
cat > "$WORK/two-items.yml" <<'YAML'
deploy:prod:
  after_script:
    - |
      echo one
    - echo two
YAML
ROOT_REAL="$ROOT"
mkdir -p "$WORK/fake-root"
cp "$WORK/two-items.yml" "$WORK/fake-root/.gitlab-ci.yml"
ROOT="$WORK/fake-root"
if block after_script >/dev/null; then found=yes; else found=no; fi
expect "two items in after_script are refused" "no" "$found"
ROOT="$ROOT_REAL"

# Fake docker and git that only record what they were asked to do. The fake docker fails a login when
# $WORK/login-fails exists.
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
FAKE
chmod +x "$WORK/bin/docker" "$WORK/bin/git"

# Prepares the deploy directory with the recorded state and a fake health check.
# $1 yes when the fetched marker exists, $2 yes when the recreating marker exists, $3 yes when images were recorded.
prepare() {
  rm -rf "$WORK/deploy" "$WORK/calls" "$WORK/login-fails"
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
expect "an unset DEPLOY_DIR creates nothing" "0" "$(calls_matching 'login')"
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
expect "a failure after the fetch only recreates nothing" "0" "$(calls_matching 'compose|tag|health')"
expect "a failure after the fetch only says no container was recreated" "yes" "$(output_has "before any container was recreated")"
expect "a failure after the fetch only removes the markers" "no" "$(exists "$WORK/deploy/.deploy/fetched")"

prepare yes no no
expect "a failure after the fetch with nothing recorded ends cleanly" "0" "$(run_handler failed)"
expect "a failure after the fetch with nothing recorded still restores the checkout" "1" "$(calls_matching 'git checkout -f abc123')"

prepare yes yes yes
expect "a failure while recreating ends cleanly" "0" "$(run_handler failed)"
expect "a failure while recreating checks out the previous commit" "1" "$(calls_matching 'git checkout -f abc123')"
expect "a failure while recreating recreates the recorded service" "1" "$(calls_matching 'compose up -d --no-deps --force-recreate --no-build hostpanel-api')"
expect "a failure while recreating waits for the site" "1" "$(calls_matching 'health')"
expect "a failure while recreating removes the fetched marker" "no" "$(exists "$WORK/deploy/.deploy/fetched")"
expect "a failure while recreating removes the recreating marker" "no" "$(exists "$WORK/deploy/.deploy/recreating")"

prepare yes yes no
expect "a failure with nothing recorded ends cleanly" "0" "$(run_handler failed)"
expect "a failure with nothing recorded changes nothing" "0" "$(calls_matching 'compose|checkout|tag|health')"

prepare yes yes yes
expect "a successful job ends cleanly" "0" "$(run_handler success)"
expect "a successful job rolls nothing back" "0" "$(calls_matching 'compose|checkout|tag|health')"
expect "a successful job removes the fetched marker" "no" "$(exists "$WORK/deploy/.deploy/fetched")"
expect "a successful job removes the recreating marker" "no" "$(exists "$WORK/deploy/.deploy/recreating")"

if [ "$FAILURES" -ne 0 ]; then
  printf '%s check(s) failed\n' "$FAILURES"
  exit 1
fi
