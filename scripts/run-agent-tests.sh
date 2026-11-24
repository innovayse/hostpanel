#!/bin/sh
# Runs the unit tests of the SSH key agent (backend/agent/ssh-key-agent/tests). The agent refuses uid 0 and the tests
# take the user running them as the stand-in account, so they must not run as root. A runner that runs jobs as root
# (a container image, a self-hosted runner) gets `nobody` through runuser; an ordinary user runs them as itself.
# Run from anywhere; used by the agent job of the GitHub workflow and of .gitlab-ci.yml.
set -eu
root="$(cd "$(dirname "$0")/.." && pwd)"
tests="$root/backend/agent/ssh-key-agent/tests"
if [ "$(id -u)" -eq 0 ]; then
  exec runuser -u nobody -- env PYTHONDONTWRITEBYTECODE=1 python3 -m unittest discover -s "$tests"
fi
exec env PYTHONDONTWRITEBYTECODE=1 python3 -m unittest discover -s "$tests"
