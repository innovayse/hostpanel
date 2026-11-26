#!/usr/bin/env sh
#
# Tests the three image guards: scripts/image-tags.sh (the tags of prod and dev never meet), scripts/check-image.sh
# (a fake docker reports the startup command of an image) and scripts/check-image-tags.sh (two jobs that build the
# same service and target are refused), and that the real .gitlab-ci.yml passes the last one.
#
# Usage: sh scripts/test-image-tags.sh

set -u

SCRIPTS="$(cd "$(dirname "$0")" && pwd)"
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

# Writes a fake docker into $WORK/bin whose `image inspect` prints $1 as the startup command of any image.
fake_docker() {
  mkdir -p "$WORK/bin"
  printf '%s\n' "$1" > "$WORK/startup"
  printf '#!/usr/bin/env sh\ncat "%s"\n' "$WORK/startup" > "$WORK/bin/docker"
  chmod +x "$WORK/bin/docker"
}

# Writes a pipeline file with one image job per line of "service target" given on stdin.
pipeline() {
  while read -r service target; do
    printf 'job:\n  variables: { SERVICE: %s, DOCKERFILE: x, BUILD_CONTEXT: ., IMAGE_TARGET: %s }\n' "$service" "$target"
  done > "$WORK/pipeline.yml"
}

expect "prod image: latest and the commit" "reg/admin:latest reg/admin:0ea4d749" \
  "$(sh "$SCRIPTS/image-tags.sh" reg/admin prod 0ea4d749 | tr '\n' ' ' | sed 's/ $//')"
expect "dev image: the target in both tags" "reg/admin:dev reg/admin:dev-0ea4d749" \
  "$(sh "$SCRIPTS/image-tags.sh" reg/admin dev 0ea4d749 | tr '\n' ' ' | sed 's/ $//')"
expect "missing arguments are a usage error" "2" "$(sh "$SCRIPTS/image-tags.sh" reg/admin prod >/dev/null 2>&1; echo $?)"

fake_docker 'nginx -g daemon off;'
expect "prod image with nginx passes" "0" "$(PATH="$WORK/bin:$PATH" sh "$SCRIPTS/check-image.sh" reg/admin:latest prod >/dev/null 2>&1; echo $?)"
expect "dev target with nginx fails" "1" "$(PATH="$WORK/bin:$PATH" sh "$SCRIPTS/check-image.sh" reg/admin:dev dev >/dev/null 2>&1; echo $?)"

fake_docker ' npm run dev -- --host 0.0.0.0'
expect "prod image with the npm dev server fails" "1" "$(PATH="$WORK/bin:$PATH" sh "$SCRIPTS/check-image.sh" reg/admin:latest prod >/dev/null 2>&1; echo $?)"
expect "dev image with the npm dev server passes" "0" "$(PATH="$WORK/bin:$PATH" sh "$SCRIPTS/check-image.sh" reg/admin:dev dev >/dev/null 2>&1; echo $?)"

fake_docker 'dotnet watch run '
expect "prod image with dotnet watch fails" "1" "$(PATH="$WORK/bin:$PATH" sh "$SCRIPTS/check-image.sh" reg/api:latest prod >/dev/null 2>&1; echo $?)"
expect "an unknown target is a usage error" "2" "$(PATH="$WORK/bin:$PATH" sh "$SCRIPTS/check-image.sh" reg/api:x stage >/dev/null 2>&1; echo $?)"

printf 'api prod\napi dev\nadmin prod\nadmin dev\n' | pipeline
expect "a prod and a dev job of one service do not collide" "0" "$(sh "$SCRIPTS/check-image-tags.sh" "$WORK/pipeline.yml" >/dev/null 2>&1; echo $?)"

printf 'admin prod\nadmin prod\n' | pipeline
expect "two jobs with the same service and target are refused" "1" "$(sh "$SCRIPTS/check-image-tags.sh" "$WORK/pipeline.yml" >/dev/null 2>&1; echo $?)"

printf 'x: 1\n' > "$WORK/pipeline.yml"
expect "a file without an image job is refused" "1" "$(sh "$SCRIPTS/check-image-tags.sh" "$WORK/pipeline.yml" >/dev/null 2>&1; echo $?)"
expect "an unreadable file is a usage error" "2" "$(sh "$SCRIPTS/check-image-tags.sh" "$WORK/missing.yml" >/dev/null 2>&1; echo $?)"

expect "the real pipeline file has no collision" "0" "$(cd "$SCRIPTS/.." && sh "$SCRIPTS/check-image-tags.sh" >/dev/null 2>&1; echo $?)"

if [ "$FAILURES" -ne 0 ]; then
  printf '%s check(s) failed\n' "$FAILURES"
  exit 1
fi
