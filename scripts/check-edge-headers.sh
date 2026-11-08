#!/bin/sh
# Runs the two edges this repository ships, the nginx template and the Caddyfile, and the admin image's nginx
# configuration, as they are, in front of a stub upstream, and checks the Content-Security-Policy headers they answer
# with on the portal, on a plugin page, on an upload and on the admin site (#938). check-edge-csp.sh compares the
# text of the policies; this proves what a browser would receive: one policy where one is meant, and the plugin
# page's narrow frame-src policy arriving beside the edge's, neither stripped nor duplicated.
#
# The plugin page's narrow frame-src policy is sent by the stub upstream here, not by the portal's server middleware, so
# the check proves the edges let a second policy header through and says nothing about the middleware that sends it.
#
# The stub is nginx:alpine with a generated configuration that sends the same headers the portal and the admin
# upstream would. The assertions are curl and a POSIX shell (edge-headers-assert.sh) in a container on the same
# network, so no port is published on the host and nothing but shell runs.
#
# Needs a Docker daemon and the images nginx:alpine and caddy:2-alpine. Starts five short-lived containers with a
# memory cap each and a network of its own, and removes them on exit. Files go into the containers with docker cp,
# never a bind mount, so the check also runs where the daemon is another machine's (a CI job talking to a shared
# socket). The whole run is bounded: the asserting container sleeps for at most RUN_SECONDS, and every wait and every
# request has its own limit.
#
# Usage: check-edge-headers.sh
set -eu

HERE=$(cd "$(dirname "$0")" && pwd)
ROOT=$(dirname "$HERE")
RUN=edge-headers-$$
WORK=$(mktemp -d)
IMAGE_NGINX=nginx:alpine
IMAGE_CADDY=caddy:2-alpine
# Plain HTTP and a fixed pair of names: the check does not depend on a real certificate or domain.
PORTAL=portal.test
ADMIN=admin.test
# The memory each container may use; every one here is a proxy or a sleep.
MEMORY=128m
# The longest the whole check may take; the asserting container is gone after this, whatever else happens.
RUN_SECONDS=240
# The longest an edge may take to start answering.
START_TRIES=30

# shellcheck disable=SC2329 # run by the EXIT trap
cleanup() {
  docker rm -f "$RUN-stub" "$RUN-nginx" "$RUN-caddy" "$RUN-admin" "$RUN-probe" >/dev/null 2>&1 || true
  docker network rm "$RUN" >/dev/null 2>&1 || true
  rm -rf "$WORK"
}
trap cleanup EXIT INT TERM

docker network create "$RUN" >/dev/null

echo "<!doctype html><title>admin</title>" > "$WORK/index.html"

# The stub upstream: one container answering under the three names the edges proxy to. The client portal's answer for
# a plugin page carries the narrow frame-src policy the portal's own server adds in production, so the check can prove
# an edge lets a second Content-Security-Policy header through, neither stripped nor merged. The admin answer carries a
# marker policy so the check can prove an edge does not replace the admin image's own.
cat > "$WORK/stub.conf" <<'EOF'
server {
    listen 8080;
    location / {
        return 200 "ok";
    }
}
server {
    listen 3000;
    location / {
        return 200 "ok";
    }
    location ~ /client/apps/ {
        add_header Content-Security-Policy "frame-src https://preview.example" always;
        return 200 "ok";
    }
}
server {
    listen 80;
    location / {
        add_header Content-Security-Policy "default-src 'self'; script-src 'self'; admin-marker" always;
        return 200 "ok";
    }
}
EOF
docker create --name "$RUN-stub" --network "$RUN" --memory "$MEMORY" \
  --network-alias hostpanel-api --network-alias hostpanel-client --network-alias hostpanel-admin \
  $IMAGE_NGINX >/dev/null
docker cp "$WORK/stub.conf" "$RUN-stub:/etc/nginx/conf.d/default.conf"
docker start "$RUN-stub" >/dev/null

# The nginx edge, from the template, with the container image's own variable substitution.
docker create --name "$RUN-nginx" --network "$RUN" --memory "$MEMORY" \
  -e DOMAIN=$PORTAL -e ADMIN_DOMAIN=$ADMIN -e API_PORT=8080 -e CLIENT_PORT=3000 \
  -e EDGE_MAX_BODY_SIZE=1m -e EDGE_ADMIN_MAX_BODY_SIZE=1m -e EDGE_CLIENT_CSP_SOURCES= \
  $IMAGE_NGINX >/dev/null
# The image has no templates folder until one is given; copying a folder creates it.
mkdir "$WORK/templates"
cp "$ROOT/docker/nginx/templates/default.conf.template" "$WORK/templates/"
docker cp "$WORK/templates" "$RUN-nginx:/etc/nginx/templates"
docker cp "$ROOT/docker/nginx/snippets" "$RUN-nginx:/etc/nginx/snippets"
docker start "$RUN-nginx" >/dev/null

# The Caddy edge, from the Caddyfile, in plain HTTP so no certificate is asked for.
docker create --name "$RUN-caddy" --network "$RUN" --memory "$MEMORY" \
  -e DOMAIN=$PORTAL -e ADMIN_DOMAIN=$ADMIN -e API_PORT=8080 -e CLIENT_PORT=3000 \
  -e EDGE_MAX_BODY_SIZE=1MB -e EDGE_ADMIN_MAX_BODY_SIZE=1MB -e EDGE_SCHEME=http:// -e ACME_EMAIL=ops@example.test \
  $IMAGE_CADDY >/dev/null
docker cp "$ROOT/docker/caddy/Caddyfile" "$RUN-caddy:/etc/caddy/Caddyfile"
docker start "$RUN-caddy" >/dev/null

# The admin image's nginx: its own configuration and snippets serving a one-file site.
docker create --name "$RUN-admin" --network "$RUN" --memory "$MEMORY" $IMAGE_NGINX >/dev/null
docker cp "$ROOT/docker/nginx/admin.conf" "$RUN-admin:/etc/nginx/conf.d/default.conf"
docker cp "$ROOT/docker/nginx/snippets" "$RUN-admin:/etc/nginx/snippets"
docker cp "$WORK/index.html" "$RUN-admin:/usr/share/nginx/html/index.html"
docker start "$RUN-admin" >/dev/null

# The asserting container: curl and a POSIX shell on the same network, asked to run the assertions below. It sleeps
# instead of running them at once so the edges can be waited for, and its sleep is the hard bound of the whole run.
docker create --name "$RUN-probe" --network "$RUN" --memory "$MEMORY" --entrypoint sleep $IMAGE_NGINX "$RUN_SECONDS" >/dev/null
docker cp "$HERE/edge-headers-assert.sh" "$RUN-probe:/assert.sh"
docker start "$RUN-probe" >/dev/null

# Waits for an address to answer before it is asked anything; a container needs a moment to start listening.
# Arguments: the container's name, the Host header to ask with.
wait_for() {
  tries=0
  until docker exec "$RUN-probe" curl -s -o /dev/null --max-time 2 -H "Host: $2" "http://$1/" >/dev/null 2>&1; do
    tries=$((tries + 1))
    if [ "$tries" -ge "$START_TRIES" ]; then
      echo "$1 did not start answering" >&2
      docker logs "$1" 2>&1 | tail -n 20 >&2 || true
      exit 1
    fi
    sleep 1
  done
}

wait_for "$RUN-nginx" $PORTAL
wait_for "$RUN-caddy" $PORTAL
wait_for "$RUN-admin" $ADMIN

status=0
# The check prints the portal's policy on stdout; the two edges must print the same one.
nginx_policy=$(docker exec "$RUN-probe" sh /assert.sh edge "$RUN-nginx" $PORTAL $ADMIN) || status=1
caddy_policy=$(docker exec "$RUN-probe" sh /assert.sh edge "$RUN-caddy" $PORTAL $ADMIN) || status=1

# The admin image on its own: one policy, from its own snippet, on the shell, a config file and a missing asset.
docker exec "$RUN-probe" sh /assert.sh admin "$RUN-admin" $ADMIN || status=1

if [ "$nginx_policy" != "$caddy_policy" ]; then
  echo "The two edges answer the portal with different policies." >&2
  printf '%s\n%s\n' "$nginx_policy" "$caddy_policy" >&2
  status=1
fi

if [ "$status" -eq 0 ]; then
  echo "Both edges and the admin image answer with the expected Content-Security-Policy headers."
  printf 'portal policy: %s\n' "$nginx_policy"
fi
exit $status
