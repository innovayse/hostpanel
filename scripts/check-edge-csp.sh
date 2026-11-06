#!/bin/sh
# Fails when the client portal's Content-Security-Policy differs between the two edges this repository
# ships. Caddy and nginx cannot include one file (different syntax, different variable spelling), so
# the Caddyfile is the definition and this holds the nginx template to it. An operator's own
# edge is outside this repository and is the operator's to keep in step.
#
# Usage: check-edge-csp.sh [Caddyfile] [nginx template]
set -eu

CADDYFILE=${1:-docker/caddy/Caddyfile}
TEMPLATE=${2:-docker/nginx/templates/default.conf.template}

# The client policy starts from 'self'; an uploaded file's starts from 'none' and is not this policy.
CADDY=$(sed -n "s/^[[:space:]]*header Content-Security-Policy \"\(default-src 'self'.*\)\"\$/\1/p" "$CADDYFILE" \
  | sed 's/{\$EDGE_CLIENT_CSP_SOURCES:}/SOURCES/g')
NGINX=$(sed -n "s/^[[:space:]]*add_header Content-Security-Policy \"\(default-src 'self'.*\)\" always;\$/\1/p" "$TEMPLATE" \
  | sed 's/\${EDGE_CLIENT_CSP_SOURCES}/SOURCES/g')

if [ -z "$CADDY" ] || [ -z "$NGINX" ]; then
  echo "No client policy found: an edge changed shape, so this check must change with it." >&2
  exit 1
fi

if [ "$CADDY" != "$NGINX" ]; then
  printf 'The edge policies differ.\nCaddy: %s\nnginx: %s\n' "$CADDY" "$NGINX" >&2
  exit 1
fi

echo "The edges send one client policy."
