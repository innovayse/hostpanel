#!/usr/bin/env sh
#
# Tests the failure handler (after_script) of the deploy:prod job in .gitlab-ci.yml, taken from the file
# itself and run against a fake docker and git: a deploy that never got past fetching the target commit
# leaves the running stack alone, a later failure rolls back, a successful job does nothing, and a failure
# with nothing recorded changes nothing.
#
# Usage: sh scripts/test-deploy-rollback.sh

set -u

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
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

# Prints the commands of the after_script of deploy:prod, without the YAML indentation.
handler() {
  awk '
    /^deploy:prod:/ { job = 1; next }
    job && /^[^ #]/ { job = 0 }
    job && /^  after_script:/ { inside = 1; next }
    inside && /^  [^ #]/ { inside = 0 }
    inside && /^    - \|/ { next }
    inside { sub(/^      /, ""); print }
  ' "$ROOT/.gitlab-ci.yml"
}

handler > "$WORK/handler.sh"
expect "the handler was found in .gitlab-ci.yml" "yes" "$([ -s "$WORK/handler.sh" ] && echo yes || echo no)"

# Fake docker and git that only record what they were asked to do.
mkdir "$WORK/bin"
for tool in docker git; do
  cat > "$WORK/bin/$tool" <<FAKE
#!/usr/bin/env sh
echo "$tool \$*" >> "$WORK/calls"
FAKE
  chmod +x "$WORK/bin/$tool"
done

# Prepares the deploy directory with the recorded state and a fake health check.
# $1 yes when the fetched marker exists, $2 yes when images were recorded.
prepare() {
  rm -rf "$WORK/deploy" "$WORK/calls"
  mkdir -p "$WORK/deploy/.deploy" "$WORK/deploy/scripts"
  : > "$WORK/calls"
  echo "abc123" > "$WORK/deploy/.deploy/previous-commit"
  : > "$WORK/deploy/.deploy/previous-images"
  if [ "$2" = "yes" ]; then
    echo "hostpanel-api hostpanel-rollback/hostpanel-api:previous" > "$WORK/deploy/.deploy/previous-images"
  fi
  if [ "$1" = "yes" ]; then
    : > "$WORK/deploy/.deploy/fetched"
  fi
  echo 'echo "health $*" >> "'"$WORK/calls"'"' > "$WORK/deploy/scripts/wait-for-health.sh"
}

# Runs the handler with the job status $1. Prints its exit status; the output is in $WORK/out.
run_handler() {
  PATH="$WORK/bin:$PATH" DEPLOY_DIR="$WORK/deploy" CI_JOB_STATUS="$1" CI_REGISTRY=registry.example \
    IMAGE_REGISTRY=registry.example/hostpanel IMAGE_TAG=tag HEALTH_URL=http://health.example HEALTH_TIMEOUT_SECONDS=1 \
    sh "$WORK/handler.sh" >"$WORK/out" 2>&1
  echo $?
}

# Counts the recorded calls that match the pattern $1.
calls_matching() {
  grep -c -E "$1" "$WORK/calls" || true
}

prepare no yes
expect "a failure before the fetch ends cleanly" "0" "$(run_handler failed)"
expect "a failure before the fetch recreates nothing" "0" "$(calls_matching 'compose|checkout|tag|health')"
case "$(cat "$WORK/out")" in *"before the target commit was fetched"*) found=yes ;; *) found=no ;; esac
expect "a failure before the fetch says why the stack is left alone" "yes" "$found"

prepare yes yes
expect "a failure after the fetch ends cleanly" "0" "$(run_handler failed)"
expect "a failure after the fetch checks out the previous commit" "1" "$(calls_matching 'git checkout -f abc123')"
expect "a failure after the fetch recreates the recorded service" "1" "$(calls_matching 'compose up -d --no-deps --force-recreate --no-build hostpanel-api')"
expect "a failure after the fetch waits for the site" "1" "$(calls_matching 'health')"

prepare yes no
expect "a failure with nothing recorded ends cleanly" "0" "$(run_handler failed)"
expect "a failure with nothing recorded changes nothing" "0" "$(calls_matching 'compose|checkout|tag|health')"

prepare yes yes
expect "a successful job ends cleanly" "0" "$(run_handler success)"
expect "a successful job rolls nothing back" "0" "$(calls_matching 'compose|checkout|tag|health')"

if [ "$FAILURES" -ne 0 ]; then
  printf '%s check(s) failed\n' "$FAILURES"
  exit 1
fi
