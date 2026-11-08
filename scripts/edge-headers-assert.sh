#!/bin/sh
# The assertions of check-edge-headers.sh. It runs inside a container on the check's network, with curl and a POSIX
# shell only, and asks a running edge for the pages whose Content-Security-Policy matters (#938).
#
# The plugin page's narrow frame-src policy comes from the stub upstream, which plays the portal, not from the portal's
# server middleware (client/server/middleware/plugin-frame-csp.ts). So this proves an edge lets a second policy header
# through as its own header, and says nothing about the middleware that sends it.
#
# Usage: edge-headers-assert.sh edge  HOST PORTAL_VHOST ADMIN_VHOST   one of the two public edges
#        edge-headers-assert.sh admin HOST VHOST                      the admin image's own nginx
#
# The "edge" mode prints the portal's policy, whitespace normalised, on stdout so the caller can compare the two edges.
# Every failed expectation goes to stderr and the exit status is 1.
set -u

# The narrow policy the stub's portal adds on a plugin page; keep in step with check-edge-headers.sh.
PLUGIN_FRAME_POLICY="frame-src https://preview.example"
# What the stub's admin upstream sends; the edge must pass it through alone.
ADMIN_POLICY="default-src 'self'; script-src 'self'; admin-marker"
# What an uploaded file is served under.
UPLOAD_POLICY="default-src 'none'; sandbox"

FAILURES=0
HEADERS=
STATUS=

# Records one failed expectation.
# Arguments: the message.
fail() {
  echo "FAIL $HOST: $1" >&2
  FAILURES=$((FAILURES + 1))
}

# Requests one path and keeps the status and the response headers, one per line, carriage returns removed. A repeated
# header stays repeated, which is the point of asking for the raw header block.
# Arguments: the Host header, the path.
fetch() {
  if ! HEADERS=$(curl -sS --max-time 10 -o /dev/null -D - -H "Host: $1" "http://$HOST$2" | tr -d '\r'); then
    fail "$2 did not answer"
    HEADERS=
  fi
  STATUS=$(printf '%s\n' "$HEADERS" | sed -n '1s/^HTTP\/[0-9.]* \([0-9]*\).*/\1/p')
}

# Prints the values of one header of the last response, one per line.
# Arguments: the lower-case header name.
values() {
  printf '%s\n' "$HEADERS" | awk -v name="$1" '
    index(tolower($0), name ":") == 1 { sub(/^[^:]*:[ \t]*/, ""); print }'
}

# Prints how many times one header of the last response was sent.
# Arguments: the lower-case header name.
count() {
  values "$1" | grep -c .
}

# Succeeds when the header was sent exactly once, with exactly this value.
# Arguments: the lower-case header name, the value.
only_value() {
  [ "$(count "$1")" -eq 1 ] && [ "$(values "$1")" = "$2" ]
}

# Checks the pages of one public edge and prints the portal's policy.
# Arguments: the portal's Host header, the admin site's Host header.
check_edge() {
  fetch "$1" /
  portal_policy=$(values content-security-policy)
  [ "$STATUS" = 200 ] || fail "/ answered $STATUS"
  [ "$(count content-security-policy)" -eq 1 ] || fail "/ carries $(count content-security-policy) Content-Security-Policy headers, expected 1"
  case $portal_policy in "default-src 'self'"*) ;; *) fail "/ policy does not start from 'self'" ;; esac
  case $portal_policy in *"frame-ancestors 'none'"*) ;; *) fail "/ policy lets the portal be framed" ;; esac
  only_value x-content-type-options nosniff || fail "/ lacks X-Content-Type-Options: nosniff"
  only_value x-frame-options DENY || fail "/ lacks X-Frame-Options: DENY"
  if values server | grep -qiE '^(nginx|caddy)/'; then
    fail "/ names the proxy's version"
  fi
  echo "$portal_policy" | tr -s ' \t' ' '

  # A plugin page gets a second, narrow policy from the portal; both must reach the browser, as two headers. The
  # second one is the stub's, not the portal middleware's: this proves the edge passes it, not that the portal sends it.
  for path in /client/apps/demo /hy/client/apps/demo/page; do
    fetch "$1" "$path"
    [ "$STATUS" = 200 ] || fail "$path answered $STATUS"
    [ "$(count content-security-policy)" -eq 2 ] || fail "$path carries $(count content-security-policy) Content-Security-Policy headers, expected 2"
    values content-security-policy | grep -qxF "$PLUGIN_FRAME_POLICY" || fail "$path lost the plugin's narrow frame-src policy"
    values content-security-policy | grep -q "^default-src 'self'" || fail "$path lost the edge policy"
  done

  fetch "$1" /uploads/logo.png
  only_value content-security-policy "$UPLOAD_POLICY" || fail "/uploads/ is not served under its own policy"

  fetch "$2" /
  only_value content-security-policy "$ADMIN_POLICY" || fail "admin / does not carry the admin upstream's policy alone: $(values content-security-policy | tr '\n' '|')"
}

# Checks that every location of the admin image's nginx sends exactly one policy, its own. Every location includes the
# snippets by name because a location with an add_header of its own drops the inherited ones. A missing file still
# answers with the policy: the 404 page of an asset is a document too.
# Arguments: the Host header.
check_admin() {
  for path in / /index.html /config.js /theme-boot.js /assets/none.js; do
    fetch "$1" "$path"
    policy=$(values content-security-policy)
    if [ "$(count content-security-policy)" -ne 1 ]; then
      fail "admin image $path carries $(count content-security-policy) policies, expected 1"
      continue
    fi
    case $policy in "default-src 'self'"*) ;; *) fail "admin image $path policy does not start from 'self'" ;; esac
    case $policy in *"script-src 'self';"*) ;; *) fail "admin image $path policy lacks script-src 'self'" ;; esac
    case $policy in *"frame-ancestors 'none'"*) ;; *) fail "admin image $path policy lets the admin be framed" ;; esac
  done
}

MODE=$1
HOST=$2
case $MODE in
  edge) check_edge "$3" "$4" ;;
  admin) check_admin "$3" ;;
  *) echo "unknown mode $MODE" >&2; exit 2 ;;
esac

[ "$FAILURES" -eq 0 ]
