#!/usr/bin/env sh
#
# Prints the version a Hostpanel image is built with: the newest release tag reachable from HEAD, without
# its leading v, plus the short commit id as build metadata, for example 0.5.0+1a2b3c4d.
#
# Build metadata does not change semantic version precedence, so a plugin range such as "minHostVersion
# 0.4.0" still admits the host. The host strips the metadata when it reports its version
# (InstalledVersionProvider.ParseVersion).
#
# It fails, and prints why on stderr, when no release tag (vMAJOR.MINOR.PATCH) is reachable. An image
# must never be built with the Dockerfile placeholder 0.0.0-dev, because the host would then report a real
# looking version that is below every plugin's minimum. A git failure (not a repository, an unsafe
# directory) stops the script with git's own message instead of being read as "no tag".
#
# Usage, from a full clone that has its tags: sh scripts/image-version.sh

set -eu

tags="$(git tag --merged HEAD --list 'v*' --sort=-v:refname)"
sha="$(git rev-parse --short=8 HEAD)"
tag="$(printf '%s\n' "$tags" | grep -E '^v[0-9]+\.[0-9]+\.[0-9]+$' | head -n 1 || true)"

if [ -z "$tag" ]; then
  echo "image-version: no release tag vMAJOR.MINOR.PATCH is reachable from HEAD; fetch the tags (full clone) or tag a release." >&2
  exit 1
fi

printf '%s+%s\n' "${tag#v}" "$sha"
