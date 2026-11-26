#!/usr/bin/env sh
#
# Fails when a freshly built image does not start what its target says it is: a prod image whose entrypoint or
# command is the development server (npm run dev, yarn dev, dotnet watch), or a dev image that is not. It runs in
# the image jobs after the build and before the push, so a wrong image never reaches the registry, whatever tag
# it would have been given.
#
# Usage: sh scripts/check-image.sh <image> <target>
#   sh scripts/check-image.sh registry.example/hostpanel/admin:latest prod
#
# Exit status: 0 the image matches its target, 1 it does not, 2 usage or an unknown target.

set -eu

# What every development stage of the three Dockerfiles starts with. A new dev command is added here, once.
DEV_COMMAND_PATTERN='npm run dev|yarn dev|dotnet watch'

if [ "$#" -ne 2 ] || [ -z "$1" ] || [ -z "$2" ]; then
  echo "Usage: $0 <image> <target>" >&2
  exit 2
fi

image="$1"
target="$2"

startup="$(docker image inspect --format '{{join .Config.Entrypoint " "}} {{join .Config.Cmd " "}}' "$image")"

if printf '%s' "$startup" | grep -Eq "$DEV_COMMAND_PATTERN"; then
  runs_dev=yes
else
  runs_dev=no
fi

case "$target" in
  prod)
    if [ "$runs_dev" = yes ]; then
      echo "check-image: $image is a prod image but starts the development server: $startup" >&2
      exit 1
    fi
    ;;
  dev)
    if [ "$runs_dev" = no ]; then
      echo "check-image: $image is a dev image but does not start the development server: $startup" >&2
      exit 1
    fi
    ;;
  *)
    echo "check-image: unknown target '$target', expected prod or dev" >&2
    exit 2
    ;;
esac

echo "check-image: $image matches its $target target."
