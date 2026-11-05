#!/usr/bin/env bash
# =============================================================================
# Hostpanel -- one-command installer from the Docker Hub images.
#
#   curl -fsSL https://raw.githubusercontent.com/innovayse/hostpanel/main/install.sh | bash
#
# It downloads docker-compose.hub.yml, the Caddyfile and .env.example (nothing else: no clone, no
# build), writes a .env with freshly generated secrets, pulls the images and starts the stack. The
# bundled caddy edge obtains Let's Encrypt certificates for both hosts, so DNS for them must point
# at this server and ports 80 and 443 must be reachable.
# Run it again to upgrade: an existing .env is never overwritten, the images are pulled
# and the stack is brought up again.
#
# Options (flag, or the environment variable in brackets):
#   --dir <path>       install directory                  [HOSTPANEL_DIR, default /opt/hostpanel]
#   --version <x.y.z>  release to install, e.g. 0.4.0     [HOSTPANEL_VERSION, default: main / latest]
#   --no-tls           serve plain HTTP on 8080 behind your own TLS proxy [HOSTPANEL_TLS=off]
#   --help             print this text
#
# Answers for a non-interactive run (no terminal, or HOSTPANEL_NONINTERACTIVE=1):
#   HOSTPANEL_DOMAIN         public host of the client portal      (required)
#   HOSTPANEL_ADMIN_DOMAIN   public host of the admin panel        (default admin.<domain>)
#   HOSTPANEL_ADMIN_EMAIL    operator contact, sender and certificate e-mail (required)
#
# Needs: docker with the compose v2 plugin, curl, openssl.
# Every command that could read stdin is given /dev/null: under `curl | bash` stdin IS this script.
# =============================================================================
set -euo pipefail

REPO="innovayse/hostpanel"
COMPOSE_FILE="docker-compose.hub.yml"
HEALTH_ATTEMPTS=60
HEALTH_INTERVAL=5

# The installer's own choices, named once. Everything else a new .env holds is the value in .env.example
# (DOCKERHUB_NAMESPACE, the edge ports for TLS mode, the image versions ...), read from the downloaded
# template rather than repeated here.
INSTALL_DIR_DEFAULT="/opt/hostpanel"
# --no-tls: the ports caddy takes so it never collides with the TLS proxy that owns 80 and 443.
PLAIN_HTTP_PORT=8080
PLAIN_HTTPS_PORT=8443
# Database name, and the postgres and rabbitmq user, of a fresh install (the template's are dev values).
SERVICE_ROLE="hostpanel"
# The relay the operator is expected to configure: submission port and its encryption.
SMTP_RELAY_PORT=587
SMTP_RELAY_ENCRYPTION="tls"
# Compose service (and container) name of the API.
API_SERVICE="hostpanel-api"

# ── Output helpers ────────────────────────────────────────────────────────────
if [ -t 1 ]; then
  RED=$'\033[0;31m'; GREEN=$'\033[0;32m'; YELLOW=$'\033[1;33m'
  BLUE=$'\033[0;34m'; BOLD=$'\033[1m'; NC=$'\033[0m'
else
  RED=''; GREEN=''; YELLOW=''; BLUE=''; BOLD=''; NC=''
fi

info()    { printf '%s[info]%s  %s\n' "$BLUE" "$NC" "$*"; }
success() { printf '%s[ok]%s    %s\n' "$GREEN" "$NC" "$*"; }
warn()    { printf '%s[warn]%s  %s\n' "$YELLOW" "$NC" "$*" >&2; }
fail()    { printf '%s[error]%s %s\n' "$RED" "$NC" "$*" >&2; exit 1; }

# Prints the leading comment block of this file (the usage text).
usage() {
  cat <<'USAGE'
Usage: install.sh [--dir <path>] [--version <x.y.z>] [--no-tls]
  --dir      install directory   (HOSTPANEL_DIR, default /opt/hostpanel)
  --version  release to install  (HOSTPANEL_VERSION, default main / latest)
  --no-tls   plain HTTP on 8080 for an install behind your own TLS proxy (HOSTPANEL_TLS=off)
Non-interactive answers: HOSTPANEL_DOMAIN, HOSTPANEL_ADMIN_DOMAIN, HOSTPANEL_ADMIN_EMAIL
(HOSTPANEL_NONINTERACTIVE=1 forces it even with a terminal).
USAGE
}

# ── .env helpers ──────────────────────────────────────────────────────────────

# Sets KEY=value in a dotenv file: replaces the line when the key exists, appends otherwise.
# The value travels through the environment so no character in it is special to awk or sed.
# $1 file, $2 key, $3 value
set_env() {
  local file="$1" key="$2" value="$3" tmp
  tmp="$(mktemp)"
  KEY="$key" VALUE="$value" awk '
    BEGIN { k = ENVIRON["KEY"]; v = ENVIRON["VALUE"]; done = 0 }
    index($0, k "=") == 1 { if (!done) { print k "=" v; done = 1 } ; next }
    { print }
    END { if (!done) { print k "=" v } }
  ' "$file" > "$tmp"
  cat "$tmp" > "$file"
  rm -f "$tmp"
}

# Prints the value of KEY in a dotenv file (empty when absent). $1 file, $2 key
get_env() {
  local line
  line="$(grep -E "^$2=" "$1" | tail -n 1 || true)"
  printf '%s' "${line#*=}"
}

# Prints a fresh hex secret of $1 random bytes (hex is safe in connection strings and URLs).
rand_hex() { openssl rand -hex "$1"; }

# ── Input helpers ─────────────────────────────────────────────────────────────

# True when a terminal can be read: stdin is the pipe under `curl | bash`, so we ask /dev/tty.
have_tty() {
  [ "${HOSTPANEL_NONINTERACTIVE:-0}" != "1" ] && { : < /dev/tty; } 2>/dev/null
}

# Asks a question on the terminal and prints the answer. $1 prompt, $2 default (may be empty)
ask() {
  local prompt="$1" default="$2" answer=''
  if [ -n "$default" ]; then
    printf '  %s [%s]: ' "$prompt" "$default" > /dev/tty
  else
    printf '  %s: ' "$prompt" > /dev/tty
  fi
  IFS= read -r answer < /dev/tty || true
  printf '%s' "${answer:-$default}"
}

# Fails unless $1 looks like a host name. $2 is the label for the message.
check_host() {
  if ! printf '%s' "$1" | grep -Eq '^[A-Za-z0-9]([A-Za-z0-9-]*[A-Za-z0-9])?(\.[A-Za-z0-9]([A-Za-z0-9-]*[A-Za-z0-9])?)+$'; then
    fail "$2 '$1' is not a valid host name (example: panel.example.com)."
  fi
}

# Fails unless $1 looks like an e-mail address.
check_email() {
  if ! printf '%s' "$1" | grep -Eq '^[^[:space:]@;]+@[^[:space:]@;]+\.[^[:space:]@;]+$'; then
    fail "Admin e-mail '$1' is not a valid address."
  fi
}

# ── Steps ─────────────────────────────────────────────────────────────────────

# Exits with a clear message when docker, compose v2, curl or openssl is missing.
check_prerequisites() {
  command -v docker >/dev/null 2>&1 \
    || fail "Docker is not installed. Install it first: https://docs.docker.com/engine/install/"
  docker compose version >/dev/null 2>&1 \
    || fail "Docker Compose v2 is missing ('docker compose' failed). Install the compose plugin: https://docs.docker.com/compose/install/"
  docker info >/dev/null 2>&1 \
    || fail "Cannot talk to the Docker daemon. Is it running, and is this user allowed to use it (try sudo)?"
  command -v curl >/dev/null 2>&1 || fail "curl is required."
  command -v openssl >/dev/null 2>&1 || fail "openssl is required to generate the secrets."
}

# Downloads one file from the pinned ref into the install dir, atomically. $1 repo path, $2 target name
download() {
  local url="https://raw.githubusercontent.com/${REPO}/${REF}/$1" tmp
  tmp="$(mktemp)"
  if ! curl -fsSL "$url" -o "$tmp" < /dev/null; then
    rm -f "$tmp"
    fail "Could not download $url (is ${REF} a real branch or release tag?)."
  fi
  mv "$tmp" "${INSTALL_DIR}/$2"
  chmod 644 "${INSTALL_DIR}/$2"
}

# Asks (or reads from the environment) for the domain, admin domain and admin e-mail.
collect_answers() {
  DOMAIN="${HOSTPANEL_DOMAIN:-}"
  ADMIN_DOMAIN="${HOSTPANEL_ADMIN_DOMAIN:-}"
  ADMIN_EMAIL="${HOSTPANEL_ADMIN_EMAIL:-}"

  if have_tty; then
    printf '\n%sConfiguration%s\n' "$BOLD" "$NC" > /dev/tty
    [ -n "$DOMAIN" ] || DOMAIN="$(ask 'Domain of the client portal (e.g. panel.example.com)' '')"
    [ -n "$ADMIN_DOMAIN" ] || ADMIN_DOMAIN="$(ask 'Domain of the admin panel' "admin.${DOMAIN}")"
    [ -n "$ADMIN_EMAIL" ] || ADMIN_EMAIL="$(ask 'Admin e-mail (contact form target and mail sender)' '')"
  fi

  [ -n "$DOMAIN" ] || fail "Domain is required: answer the prompt, or set HOSTPANEL_DOMAIN for a non-interactive run."
  [ -n "$ADMIN_EMAIL" ] || fail "Admin e-mail is required: answer the prompt, or set HOSTPANEL_ADMIN_EMAIL."
  [ -n "$ADMIN_DOMAIN" ] || ADMIN_DOMAIN="admin.${DOMAIN}"

  check_host "$DOMAIN" "Domain"
  check_host "$ADMIN_DOMAIN" "Admin domain"
  check_email "$ADMIN_EMAIL"
  if [ "$DOMAIN" = "$ADMIN_DOMAIN" ]; then
    fail "The admin panel is built for the root path and needs a host of its own: domain and admin domain must differ."
  fi
}

# Writes the edge settings for the chosen TLS mode. $1 file
# on : caddy owns the template's ports (80/443) and gets the certificates. off: caddy serves plain HTTP
# on PLAIN_HTTP_PORT (443 is parked on PLAIN_HTTPS_PORT) so it never collides with the proxy that
# already owns 80 and 443 on this host.
set_env_edge() {
  local template="${INSTALL_DIR}/.env.example"
  if [ "$TLS" = "off" ]; then
    set_env "$1" EDGE_SCHEME "http://"
    set_env "$1" EDGE_HTTP_PORT "$PLAIN_HTTP_PORT"
    set_env "$1" EDGE_HTTPS_PORT "$PLAIN_HTTPS_PORT"
  else
    set_env "$1" EDGE_SCHEME "$(get_env "$template" EDGE_SCHEME)"
    set_env "$1" EDGE_HTTP_PORT "$(get_env "$template" EDGE_HTTP_PORT)"
    set_env "$1" EDGE_HTTPS_PORT "$(get_env "$template" EDGE_HTTPS_PORT)"
  fi
}

# Creates .env from .env.example with generated secrets and the answers. Never called when .env exists.
create_env() {
  local env="${INSTALL_DIR}/.env"
  info "Creating ${env} with generated secrets"
  collect_answers

  # Written under a restrictive umask so the secrets are never world-readable, not even briefly.
  ( umask 077; cp "${INSTALL_DIR}/.env.example" "$env" )

  set_env "$env" ASPNETCORE_ENVIRONMENT Production
  set_env "$env" AUTH_MODE local
  set_env "$env" IMAGE_TAG "$IMAGE_TAG"

  set_env "$env" DOMAIN "$DOMAIN"
  set_env "$env" ADMIN_DOMAIN "$ADMIN_DOMAIN"
  set_env "$env" ACME_EMAIL "$ADMIN_EMAIL"
  set_env_edge "$env"
  set_env "$env" NUXT_PUBLIC_BASE_URL "https://${DOMAIN}"
  set_env "$env" ADMIN_BASE_URL "https://${ADMIN_DOMAIN}"
  set_env "$env" CLIENT_BASE_URL "https://${DOMAIN}"
  set_env "$env" ALLOWED_HOSTS "${DOMAIN};${ADMIN_DOMAIN};${API_SERVICE};localhost"

  set_env "$env" POSTGRES_USER "$SERVICE_ROLE"
  set_env "$env" POSTGRES_DB "$SERVICE_ROLE"
  set_env "$env" POSTGRES_PASSWORD "$(rand_hex 24)"
  set_env "$env" RABBITMQ_USER "$SERVICE_ROLE"
  set_env "$env" RABBITMQ_PASSWORD "$(rand_hex 24)"

  set_env "$env" JWT_SECRET "$(rand_hex 32)"
  # The API decodes this as base64 and requires exactly 32 bytes (AesEncryptionService).
  set_env "$env" ENCRYPTION_KEY "$(openssl rand -base64 32)"
  set_env "$env" HOSTPANEL_BACKCHANNEL_LOGOUT_KEY "$(rand_hex 24)"
  # The client portal encrypts its stored sessions with this key: 32 random bytes, base64.
  set_env "$env" SESSION_ENCRYPTION_KEY "$(openssl rand -base64 32)"

  # Mail: the bundled example points at a dev catcher that does not exist in this stack.
  # The operator fills in a real relay afterwards; until then the API starts but cannot send.
  set_env "$env" SMTP_HOST ""
  set_env "$env" SMTP_PORT "$SMTP_RELAY_PORT"
  set_env "$env" SMTP_ENCRYPTION "$SMTP_RELAY_ENCRYPTION"
  set_env "$env" SMTP_FROM "$ADMIN_EMAIL"
  set_env "$env" SMTP_FROM_NAME "$DOMAIN"
  set_env "$env" EMAIL_TO "$ADMIN_EMAIL"

  chmod 600 "$env"
  success ".env created (mode 600)"
}

# Keeps an existing .env: moves IMAGE_TAG when a version was asked for, and adds keys the new
# .env.example introduced. Nothing already set is changed.
update_env() {
  local env="${INSTALL_DIR}/.env" key
  info "Keeping the existing ${env}"

  if [ -n "${HOSTPANEL_VERSION:-}" ]; then
    set_env "$env" IMAGE_TAG "$IMAGE_TAG"
    info "IMAGE_TAG set to ${IMAGE_TAG}"
  fi

  while IFS= read -r key; do
    if ! grep -Eq "^${key}=" "$env"; then
      # A secret is generated, never copied from the template: a template value is public.
      if [ "$key" = "SESSION_ENCRYPTION_KEY" ]; then
        set_env "$env" "$key" "$(openssl rand -base64 32)"
        warn "New secret ${key} generated and added to .env."
      else
        set_env "$env" "$key" "$(get_env "${INSTALL_DIR}/.env.example" "$key")"
        warn "New setting ${key} added to .env with its example value; review it."
      fi
    fi
  done < <(grep -Eo '^[A-Z][A-Z0-9_]*=' "${INSTALL_DIR}/.env.example" | tr -d '=')

  # The certificate contact defaults to the operator address the install already holds.
  if [ "$(get_env "$env" ACME_EMAIL)" = "$(get_env "${INSTALL_DIR}/.env.example" ACME_EMAIL)" ]; then
    set_env "$env" ACME_EMAIL "$(get_env "$env" EMAIL_TO)"
  fi
  # An explicit --no-tls / HOSTPANEL_TLS on a re-run switches the mode; otherwise the .env decides.
  if [ -n "$TLS_ASKED" ]; then
    set_env_edge "$env"
  fi
}

# Runs compose in the install dir. Every call gets /dev/null as stdin (see the header).
compose() {
  ( cd "$INSTALL_DIR" && docker compose -f "$COMPOSE_FILE" "$@" < /dev/null )
}

# The API runs as an unprivileged user. A data volume created by an earlier release belongs to root,
# so it is handed over once before the stack starts (a no-op on a volume that already belongs to it).
fix_volume_ownership() {
  info "Making sure the API can write to its data volumes"
  compose run -T --rm --no-deps --user 0 --cap-add CHOWN --entrypoint sh "$API_SERVICE" \
    -c 'chown -R "$APP_UID" /app/wwwroot/uploads /app/plugins-installed' \
    || warn "Could not set the owner of the API data volumes; uploads may fail until you do."
}

# Waits until the API answers /health from inside its own container (its port is not published).
wait_for_api() {
  local port
  port="$(get_env "${INSTALL_DIR}/.env" API_PORT)"
  info "Waiting for the API to become healthy (first start applies the database migrations)"
  for _ in $(seq 1 "$HEALTH_ATTEMPTS"); do
    if compose exec -T "$API_SERVICE" wget -q -O /dev/null "http://127.0.0.1:${port}/health" >/dev/null 2>&1; then
      success "API is healthy"
      return 0
    fi
    sleep "$HEALTH_INTERVAL"
  done
  warn "The API did not become healthy in $((HEALTH_ATTEMPTS * HEALTH_INTERVAL))s."
  warn "Look at it with:  cd ${INSTALL_DIR} && docker compose -f ${COMPOSE_FILE} logs ${API_SERVICE}"
  return 1
}

# Prints the one-time first-admin setup token the API logs while nobody holds the Admin role.
read_setup_token() {
  compose logs --no-color "$API_SERVICE" 2>/dev/null \
    | sed -n 's/.*paste this setup token when it asks for one: //p' \
    | tail -n 1 | tr -d '"\r' | sed 's/[[:space:]]*$//' || true
}

# Prints the URLs and the exact first-admin step.
print_summary() {
  local domain admin_domain token http_port
  domain="$(get_env "${INSTALL_DIR}/.env" DOMAIN)"
  admin_domain="$(get_env "${INSTALL_DIR}/.env" ADMIN_DOMAIN)"
  http_port="$(get_env "${INSTALL_DIR}/.env" EDGE_HTTP_PORT)"
  token="$(read_setup_token)"

  printf '\n%sHostpanel is running.%s\n\n' "$BOLD" "$NC"
  printf '  Client portal : https://%s\n' "$domain"
  printf '  Admin panel   : https://%s\n' "$admin_domain"
  printf '  Install dir   : %s  (secrets are in .env, mode 600)\n\n' "$INSTALL_DIR"
  if [ "$(get_env "${INSTALL_DIR}/.env" EDGE_SCHEME)" = "http://" ]; then
    printf '  TLS is off: the bundled caddy serves plain HTTP on port %s. Forward both hosts to it from\n' "$http_port"
    printf '  your TLS proxy, keeping the Host header and sending X-Forwarded-Proto: https (sign-in\n'
    printf '  cookies need a secure origin).\n\n'
  else
    printf '  Certificates are issued by Let'"'"'s Encrypt on first request. DNS for both hosts must point at\n'
    printf '  this server and ports 80 and 443 must be open; watch it with:\n'
    printf '       cd %s && docker compose -f %s logs hostpanel-caddy\n\n' "$INSTALL_DIR" "$COMPOSE_FILE"
  fi

  printf '%sFirst admin account%s\n' "$BOLD" "$NC"
  if [ -n "$token" ]; then
    printf '  1. Open https://%s and create your account.\n' "$admin_domain"
    printf '  2. Paste this one-time setup token when asked (it proves you operate this server\n'
    printf '     and stops working once setup is done):\n\n'
    printf '       %s%s%s\n\n' "$BOLD" "$token" "$NC"
    printf '  Lost it? It is repeated in the API log until setup completes:\n'
    printf '       cd %s && docker compose -f %s logs %s | grep "setup token"\n\n' "$INSTALL_DIR" "$COMPOSE_FILE" "$API_SERVICE"
  else
    printf '  No setup token is outstanding: an administrator already exists, so sign in at\n'
    printf '  https://%s. If this is a fresh install, read the log for the token:\n' "$admin_domain"
    printf '       cd %s && docker compose -f %s logs %s | grep "setup token"\n\n' "$INSTALL_DIR" "$COMPOSE_FILE" "$API_SERVICE"
  fi

  printf '%sNext%s: set a real SMTP relay in %s/.env (SMTP_HOST, SMTP_USERNAME, SMTP_PASSWORD), then\n' "$BOLD" "$NC" "$INSTALL_DIR"
  printf '  cd %s && docker compose -f %s up -d\n' "$INSTALL_DIR" "$COMPOSE_FILE"
  printf 'Upgrade any time by running the install command again.\n'
}

# ── Main ──────────────────────────────────────────────────────────────────────

# Entry point. The whole script is a function called on the last line, so under `curl | bash`
# bash has read it completely before anything runs and a truncated download runs nothing.
main() {
  INSTALL_DIR="${HOSTPANEL_DIR:-$INSTALL_DIR_DEFAULT}"
  HOSTPANEL_VERSION="${HOSTPANEL_VERSION:-}"
  TLS_ASKED="${HOSTPANEL_TLS:-}"

  while [ "$#" -gt 0 ]; do
    case "$1" in
      --dir)      [ "$#" -ge 2 ] || fail "--dir needs a path"; INSTALL_DIR="$2"; shift 2 ;;
      --version)  [ "$#" -ge 2 ] || fail "--version needs a value"; HOSTPANEL_VERSION="$2"; shift 2 ;;
      --no-tls)   TLS_ASKED="off"; shift ;;
      -h|--help)  usage; exit 0 ;;
      *)          fail "Unknown option: $1 (see --help)" ;;
    esac
  done

  TLS="${TLS_ASKED:-on}"
  case "$TLS" in
    on|off) ;;
    *) fail "HOSTPANEL_TLS must be 'on' or 'off' (got '$TLS')." ;;
  esac

  # A release is the git tag vX.Y.Z; the images are innovayse/hostpanel:{api,client,admin}-X.Y.Z, so
  # IMAGE_TAG is X.Y.Z (or latest) and the compose file adds the component prefix. Accept either spelling.
  if [ -n "$HOSTPANEL_VERSION" ]; then
    IMAGE_TAG="${HOSTPANEL_VERSION#v}"
    REF="v${IMAGE_TAG}"
  else
    IMAGE_TAG="latest"
    REF="main"
  fi

  printf '%sHostpanel installer%s  (%s, images :%s)\n' "$BOLD" "$NC" "$REF" "$IMAGE_TAG"
  check_prerequisites

  mkdir -p "$INSTALL_DIR" 2>/dev/null \
    || fail "Cannot create ${INSTALL_DIR}. Re-run with sudo, or choose another place with --dir / HOSTPANEL_DIR."
  [ -w "$INSTALL_DIR" ] || fail "${INSTALL_DIR} is not writable. Re-run with sudo, or use --dir / HOSTPANEL_DIR."
  INSTALL_DIR="$(cd "$INSTALL_DIR" && pwd)"

  info "Downloading ${COMPOSE_FILE}, Caddyfile and .env.example from ${REF} into ${INSTALL_DIR}"
  download "docker/${COMPOSE_FILE}" "$COMPOSE_FILE"
  download "docker/caddy/Caddyfile" "Caddyfile"
  download "docker/.env.example" ".env.example"

  if [ -f "${INSTALL_DIR}/.env" ]; then
    update_env
  else
    create_env
  fi

  info "Pulling the images"
  compose pull
  fix_volume_ownership
  info "Starting the stack"
  compose up -d

  if wait_for_api; then
    print_summary
  else
    exit 1
  fi
}

main "$@"
