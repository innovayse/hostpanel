#!/usr/bin/env bash
# Prints what is needed to settle the open points of the devcontainer: how the disk is used, whether the editor's
# restore leaves its package paths in the backend, and whether the stack came back after a Stop and Start.
#
# Run it in a codespace right after the first start, again once the stack is built, and after every Stop and Start,
# then paste the output into the issue. It only reads: nothing is changed or removed.
set -uo pipefail

cd "$(dirname "$0")/.."

echo "## $(date -u +%Y-%m-%dT%H:%M:%SZ)  uptime: $(uptime -p)"

echo
echo "### Disk"
df -h /workspaces / /var/lib/docker 2>/dev/null | sort -u

echo
echo "### Docker"
if docker info >/dev/null 2>&1; then
  docker system df
  echo
  docker system df -v 2>/dev/null | sed -n '/^Images space usage/,/^Containers space usage/p' | head -n 30
  echo
  echo "Containers not running:"
  docker ps -a --filter status=exited --filter status=created --format '  {{.Names}}  {{.Status}}'
  echo "Containers running: $(docker ps -q | wc -l)"
else
  echo "The Docker daemon does not answer."
fi

echo
echo "### Largest folders in the workspace"
du -xh --max-depth=3 . 2>/dev/null | sort -rh | head -n 15

echo
echo "### Largest folders on the whole disk (the workspace is only a small part of it)"
sudo du -xh --max-depth=2 / 2>/dev/null | sort -rh | head -n 25

echo
echo "### Restore leftovers (the editor's package folder in the backend)"
# NETSDK1064 comes from project.assets.json files that name /home/vscode/.nuget instead of the container's folder.
leftovers=$(grep -rl --include=project.assets.json '/home/vscode/.nuget' backend 2>/dev/null | head -n 5)
if [ -n "$leftovers" ]; then
  echo "FOUND:"
  echo "$leftovers"
else
  echo "none"
fi
grep -c 'NETSDK1064' <(docker compose --project-directory docker -f docker/docker-compose.shared.yml -f docker/docker-compose.dev.yml logs api 2>&1) 2>/dev/null | sed 's/^/API log lines with NETSDK1064: /'

echo
echo "### Stack"
docker compose --project-directory docker -f docker/docker-compose.shared.yml -f docker/docker-compose.dev.yml ps 2>&1 | head -n 20
