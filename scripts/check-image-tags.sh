#!/usr/bin/env sh
#
# Fails when two image jobs of a pipeline file would push the same tag. Every job that extends .build-image names its
# SERVICE and IMAGE_TARGET in one `variables:` line; the tags come from scripts/image-tags.sh. Two jobs with the
# same service and target, or any two references that come out equal, are refused, because the last job to push would
# overwrite the other and the production deploy would pull whichever won.
#
# Usage: sh scripts/check-image-tags.sh [pipeline file]     (default .gitlab-ci.yml)
#
# Exit status: 0 no collision, 1 a collision or no image job found, 2 the file cannot be read.

set -eu

here="$(cd "$(dirname "$0")" && pwd)"
file="${1:-.gitlab-ci.yml}"

if [ ! -r "$file" ]; then
  echo "check-image-tags: cannot read $file" >&2
  exit 2
fi

pairs="$(grep -E 'SERVICE: *[a-z]+.*IMAGE_TARGET: *[a-z]+' "$file" \
  | sed -E 's/.*SERVICE: *([a-z]+).*IMAGE_TARGET: *([a-z]+).*/\1 \2/' || true)"

if [ -z "$pairs" ]; then
  echo "check-image-tags: no image job with SERVICE and IMAGE_TARGET found in $file" >&2
  exit 1
fi

duplicated="$(printf '%s\n' "$pairs" | sort | uniq -d)"
if [ -n "$duplicated" ]; then
  printf 'check-image-tags: two jobs build the same service and target:\n%s\n' "$duplicated" >&2
  exit 1
fi

refs="$(printf '%s\n' "$pairs" | while read -r service target; do
  sh "$here/image-tags.sh" "$service" "$target" commit
done)"
equal="$(printf '%s\n' "$refs" | sort | uniq -d)"
if [ -n "$equal" ]; then
  printf 'check-image-tags: two jobs push the same tag:\n%s\n' "$equal" >&2
  exit 1
fi

echo "check-image-tags: every image job has tags of its own."
