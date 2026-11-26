#!/bin/sh
# Runs from /docker-entrypoint.d before nginx starts: turns the environment into the SPA's runtime
# config.js, so one image serves any domain. The admin is always served at the root of its own host.
set -eu

# The two optional addresses below are checked only when set: printf of an empty string writes no line, so
# grep would see no input and report no match, and the check would refuse the very value it documents as allowed.
# Optional: the storefront's public origin, so image paths the storefront stores as root-relative
# (/demo/...) resolve against it instead of the admin's own origin. Empty leaves them relative.
CLIENT_BASE_URL=$(printf '%s' "${CLIENT_BASE_URL:-}" | sed 's|/*$||')
if [ -n "$CLIENT_BASE_URL" ] && ! printf '%s' "$CLIENT_BASE_URL" | grep -Eq '^(https?://[A-Za-z0-9._~:/-]+)?$'; then
  echo "CLIENT_BASE_URL must be empty or an http(s) origin such as https://host.example.com" >&2
  exit 1
fi

# Optional: the documentation website address. Empty hides all documentation links.
DOCS_URL=$(printf '%s' "${DOCS_URL:-}" | sed 's|/*$||')
if [ -n "$DOCS_URL" ] && ! printf '%s' "$DOCS_URL" | grep -Eq '^(https?://[A-Za-z0-9._~:/-]+)?$'; then
  echo "DOCS_URL must be empty or an http(s) address such as https://hostpanel.innovayse.com/docs" >&2
  exit 1
fi

root=/usr/share/nginx/html
CLIENT_BASE_URL="$CLIENT_BASE_URL" DOCS_URL="$DOCS_URL" envsubst '${CLIENT_BASE_URL} ${DOCS_URL}' < /etc/admin/config.js.template > "$root/config.js"
