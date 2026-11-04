#!/bin/sh
# Runs from /docker-entrypoint.d before nginx starts: turns ADMIN_BASE_URL_PATH into the SPA's
# runtime config.js and the <base href> of index.html, so one image serves any base path.
set -eu

: "${ADMIN_BASE_URL_PATH:?ADMIN_BASE_URL_PATH must be set}"

if ! printf '%s' "$ADMIN_BASE_URL_PATH" | grep -Eq '^/[A-Za-z0-9._~/-]*$'; then
  echo "ADMIN_BASE_URL_PATH must start with / and contain only letters, digits and ._~/-" >&2
  exit 1
fi

# Optional: the storefront's public origin, so image paths the storefront stores as root-relative
# (/demo/...) resolve against it instead of the admin's own origin. Empty leaves them relative.
CLIENT_BASE_URL=$(printf '%s' "${CLIENT_BASE_URL:-}" | sed 's|/*$||')
if ! printf '%s' "$CLIENT_BASE_URL" | grep -Eq '^(https?://[A-Za-z0-9._~:/-]+)?$'; then
  echo "CLIENT_BASE_URL must be empty or an http(s) origin such as https://host.example.com" >&2
  exit 1
fi

# Optional: the documentation website address. Empty hides all documentation links.
DOCS_URL=$(printf '%s' "${DOCS_URL:-}" | sed 's|/*$||')
if ! printf '%s' "$DOCS_URL" | grep -Eq '^(https?://[A-Za-z0-9._~:/-]+)?$'; then
  echo "DOCS_URL must be empty or an http(s) address such as https://innovayse.github.io/hostpanel" >&2
  exit 1
fi

root=/usr/share/nginx/html
base=$(printf '%s' "$ADMIN_BASE_URL_PATH" | sed 's|/*$||')
if [ -z "$base" ]; then
  base=/
  href=/
else
  href="$base/"
fi

ADMIN_BASE_URL_PATH="$base" CLIENT_BASE_URL="$CLIENT_BASE_URL" DOCS_URL="$DOCS_URL" envsubst '${ADMIN_BASE_URL_PATH} ${CLIENT_BASE_URL} ${DOCS_URL}' < /etc/admin/config.js.template > "$root/config.js"
sed -i "s|<base href=\"[^\"]*\">|<base href=\"$href\">|" "$root/index.html"
