#!/usr/bin/env bash
# Starts (or restarts) the development stack every time the codespace starts.
#
# When a stopped codespace resumes, Docker-in-Docker can still be coming up while this command already runs, and
# "docker compose up" then fails with "Cannot connect to the Docker daemon". The database, queue and cache come back
# on their own (restart: unless-stopped), but the API, the admin panel, the client portal and the mail catcher have
# no restart policy and stayed down. So wait for the daemon first.
set -euo pipefail

# Up to three minutes, checked every two seconds.
for _ in $(seq 1 90); do
  docker info >/dev/null 2>&1 && break
  sleep 2
done

if ! docker info >/dev/null 2>&1; then
  echo "Docker did not become ready within three minutes; the stack was not started." >&2
  exit 1
fi

docker compose --project-directory docker \
  -f docker/docker-compose.shared.yml -f docker/docker-compose.dev.yml up -d

# The first start builds the images and leaves about 11 GB of build cache that nothing reads again: the sources run from
# bind mounts, and the images are already built. On the 32 GB disk that cache took the codespace to 84 percent full.
# A failure here only costs disk space, so it does not fail the start.
docker builder prune --force >/dev/null 2>&1 || true
