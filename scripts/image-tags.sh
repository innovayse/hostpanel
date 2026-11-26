#!/usr/bin/env sh
#
# Prints the image references a CI image build is tagged with and pushed under, one per line: the moving tag, then
# the commit tag. This is the one naming rule, so no job names a tag itself.
#
#   prod target:      <image>:latest and <image>:<commit>       (what the production deploy pulls)
#   any other target: <image>:<target> and <image>:<target>-<commit>   (for example :dev and :dev-<commit>)
#
# The target is part of both tags of a non-prod image, so a dev image can never take the tag a prod image of the
# same commit has. Before this rule the dev build pushed <image>:<commit> too, and whichever job finished last
# decided what the production deploy pulled.
#
# Usage: sh scripts/image-tags.sh <image> <target> <commit>
#   sh scripts/image-tags.sh registry.example/hostpanel/admin prod 0ea4d749

set -eu

if [ "$#" -ne 3 ] || [ -z "$1" ] || [ -z "$2" ] || [ -z "$3" ]; then
  echo "Usage: $0 <image> <target> <commit>" >&2
  exit 2
fi

image="$1"
target="$2"
commit="$3"

if [ "$target" = "prod" ]; then
  printf '%s:latest\n%s:%s\n' "$image" "$image" "$commit"
else
  printf '%s:%s\n%s:%s-%s\n' "$image" "$target" "$image" "$target" "$commit"
fi
