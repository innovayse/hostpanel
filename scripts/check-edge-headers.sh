#!/bin/sh
# Runs the two edges this repository ships, the nginx template and the Caddyfile, and the admin image's nginx
# configuration, as they are, in front of a stub upstream, and checks the Content-Security-Policy headers they answer
# with on the portal, on a plugin page, on an upload and on the admin site (#938). check-edge-csp.sh compares the
# text of the policies; this proves what a browser would receive: one policy where one is meant, and the plugin
# page's narrow frame-src policy arriving beside the edge's, neither stripped nor duplicated.
#
# Needs a Docker daemon and the images nginx:alpine, caddy:2-alpine and python:3.12-slim. Starts four short-lived
# containers and a network of its own, and removes them on exit. Files go into the containers with docker cp, never a
# bind mount, so the check also runs where the daemon is another machine's (a CI job talking to a shared socket).
#
# Usage: check-edge-headers.sh
set -eu

HERE=$(cd "$(dirname "$0")" && pwd)
ROOT=$(dirname "$HERE")
RUN=edge-headers-$$
WORK=$(mktemp -d)
IMAGE_PY=python:3.12-slim
# Plain HTTP and a fixed pair of names: the check does not depend on a real certificate or domain.
PORTAL=portal.test
ADMIN=admin.test

cleanup() {
  docker rm -f "$RUN-stub" "$RUN-nginx" "$RUN-caddy" "$RUN-admin" >/dev/null 2>&1 || true
  docker network rm "$RUN" >/dev/null 2>&1 || true
  rm -rf "$WORK"
}
trap cleanup EXIT INT TERM

docker network create "$RUN" >/dev/null

echo "<!doctype html><title>admin</title>" > "$WORK/index.html"

# The upstreams: one container answering under the three names the edges proxy to.
docker create --name "$RUN-stub" --network "$RUN" \
  --network-alias hostpanel-api --network-alias hostpanel-client --network-alias hostpanel-admin \
  $IMAGE_PY python /stub.py >/dev/null
docker cp "$HERE/edge_headers_stub.py" "$RUN-stub:/stub.py"
docker start "$RUN-stub" >/dev/null

# The nginx edge, from the template, with the container image's own variable substitution.
docker create --name "$RUN-nginx" --network "$RUN" \
  -e DOMAIN=$PORTAL -e ADMIN_DOMAIN=$ADMIN -e API_PORT=8080 -e CLIENT_PORT=3000 \
  -e EDGE_MAX_BODY_SIZE=1m -e EDGE_ADMIN_MAX_BODY_SIZE=1m -e EDGE_CLIENT_CSP_SOURCES= \
  nginx:alpine >/dev/null
# The image has no templates folder until one is given; copying a folder creates it.
mkdir "$WORK/templates"
cp "$ROOT/docker/nginx/templates/default.conf.template" "$WORK/templates/"
docker cp "$WORK/templates" "$RUN-nginx:/etc/nginx/templates"
docker cp "$ROOT/docker/nginx/snippets" "$RUN-nginx:/etc/nginx/snippets"
docker start "$RUN-nginx" >/dev/null

# The Caddy edge, from the Caddyfile, in plain HTTP so no certificate is asked for.
docker create --name "$RUN-caddy" --network "$RUN" \
  -e DOMAIN=$PORTAL -e ADMIN_DOMAIN=$ADMIN -e API_PORT=8080 -e CLIENT_PORT=3000 \
  -e EDGE_MAX_BODY_SIZE=1MB -e EDGE_ADMIN_MAX_BODY_SIZE=1MB -e EDGE_SCHEME=http:// -e ACME_EMAIL=ops@example.test \
  caddy:2-alpine >/dev/null
docker cp "$ROOT/docker/caddy/Caddyfile" "$RUN-caddy:/etc/caddy/Caddyfile"
docker start "$RUN-caddy" >/dev/null

# The admin image's nginx: its own configuration and snippets serving a one-file site.
docker create --name "$RUN-admin" --network "$RUN" nginx:alpine >/dev/null
docker cp "$ROOT/docker/nginx/admin.conf" "$RUN-admin:/etc/nginx/conf.d/default.conf"
docker cp "$ROOT/docker/nginx/snippets" "$RUN-admin:/etc/nginx/snippets"
docker cp "$WORK/index.html" "$RUN-admin:/usr/share/nginx/html/index.html"
docker start "$RUN-admin" >/dev/null

# run_py runs a Python script from inside the network, so no port is published on the host. The script and its
# arguments follow; the script's own output is this function's output.
run_py() {
  script=$1
  shift
  name=$RUN-py-$$-$(date +%s%N)
  docker create --name "$name" --network "$RUN" $IMAGE_PY python /script.py "$@" >/dev/null
  docker cp "$script" "$name:/script.py"
  code=0
  docker start -a "$name" || code=$?
  docker rm -f "$name" >/dev/null 2>&1 || true
  return $code
}

# waits for an address to answer before it is asked anything; a container needs a moment to start listening.
wait_for() {
  tries=0
  printf '%s\n' "import http.client, sys" \
    "c = http.client.HTTPConnection(sys.argv[1], int(sys.argv[2]), timeout=2)" \
    "c.request('GET', '/', headers={'Host': sys.argv[3]})" \
    "c.getresponse()" > "$WORK/probe.py"
  until run_py "$WORK/probe.py" "$1" 80 "$2" >/dev/null 2>&1; do
    tries=$((tries + 1))
    if [ "$tries" -ge 30 ]; then
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
nginx_policy=$(run_py "$HERE/edge_headers_check.py" "$RUN-nginx" 80 $PORTAL $ADMIN) || status=1
caddy_policy=$(run_py "$HERE/edge_headers_check.py" "$RUN-caddy" 80 $PORTAL $ADMIN) || status=1

# The admin image on its own: one policy, from its own snippet, on the shell, a config file and a missing asset.
run_py "$HERE/edge_headers_admin_check.py" "$RUN-admin" 80 admin.test || status=1

if [ "$nginx_policy" != "$caddy_policy" ]; then
  echo "The two edges answer the portal with different policies." >&2
  printf '%s\n%s\n' "$nginx_policy" "$caddy_policy" >&2
  status=1
fi

if [ "$status" -eq 0 ]; then
  echo "Both edges and the admin image answer with the expected Content-Security-Policy headers."
fi
exit $status
