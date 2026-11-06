#!/usr/bin/env bash
# Prepares a fresh codespace: fetches the plugin SDK submodule and writes docker/.env from the example, then lets the
# stack answer on the names Codespaces forwards its ports from. Safe to run again: a value a person set in docker/.env is
# kept, and what this script needs is added next to it.
set -euo pipefail

cd "$(dirname "$0")/.."

git submodule update --init --recursive

cd docker
[ -f .env ] || cp .env.example .env

# Current value of KEY in docker/.env, empty when the line is missing.
get_env() {
  grep "^$1=" .env | head -n 1 | cut -d= -f2- || true
}

# Writes KEY=value, replacing the line when there is one.
put_env() {
  local value=${2//&/\\&}
  if grep -q "^$1=" .env; then
    sed -i "s|^$1=.*|$1=$value|" .env
  else
    echo "$1=$2" >> .env
  fi
}

# Adds a value to the list KEY (items split by $3) unless it is already in it; an empty list just gets the value.
add_to_list() {
  local key=$1 value=$2 sep=$3 current
  current=$(get_env "$key")
  case "$sep$current$sep" in
    *"$sep$value$sep"*) return ;;
  esac
  if [ -z "$current" ]; then
    put_env "$key" "$value"
  else
    put_env "$key" "$current$sep$value"
  fi
}

# Replaces the value of KEY by $3 only while it still is the one the example ships ($2): a value set by hand stays.
replace_default() {
  if [ "$(get_env "$1")" = "$2" ]; then
    put_env "$1" "$3"
  fi
}

# Codespaces serves every forwarded port from a name under app.github.dev. The two dev servers and the API refuse names
# they do not know, so each is told to answer to that domain (the dot or the star covers every subdomain).
add_to_list CLIENT_DEV_ALLOWED_HOSTS .app.github.dev ,
add_to_list ADMIN_DEV_ALLOWED_HOSTS .app.github.dev ,
add_to_list ALLOWED_HOSTS '*.app.github.dev' ';'

# The API builds the links of its mails and its allowed browser origins from these two. They point at localhost in the
# example, which a browser outside the codespace cannot reach; the public name of a forwarded port is
# https://<codespace>-<port>.<forwarding domain>.
if [ -n "${CODESPACE_NAME:-}" ] && [ -n "${GITHUB_CODESPACES_PORT_FORWARDING_DOMAIN:-}" ]; then
  replace_default CORS_CLIENT_ORIGIN http://localhost:3001 "https://${CODESPACE_NAME}-3001.${GITHUB_CODESPACES_PORT_FORWARDING_DOMAIN}"
  replace_default CORS_ADMIN_ORIGIN http://localhost:5174 "https://${CODESPACE_NAME}-5174.${GITHUB_CODESPACES_PORT_FORWARDING_DOMAIN}"
fi
