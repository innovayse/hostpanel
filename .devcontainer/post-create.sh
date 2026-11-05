#!/usr/bin/env bash
# Prepares a fresh codespace: fetches the plugin SDK submodule and writes docker/.env from the example.
# Safe to run again: an existing docker/.env keeps its values except the two host lists below.
set -euo pipefail

cd "$(dirname "$0")/.."

git submodule update --init --recursive

cd docker
[ -f .env ] || cp .env.example .env

# Sets KEY=value in docker/.env, replacing an existing line.
set_env() {
  if grep -q "^$1=" .env; then
    sed -i "s|^$1=.*|$1=$2|" .env
  else
    echo "$1=$2" >> .env
  fi
}

# Codespaces serves every forwarded port from a name under app.github.dev. The two dev servers refuse names
# they do not know, so they are told to answer to that domain (the leading dot covers every subdomain).
set_env CLIENT_DEV_ALLOWED_HOSTS .app.github.dev
set_env ADMIN_DEV_ALLOWED_HOSTS .app.github.dev
