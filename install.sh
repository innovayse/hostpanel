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
# Run it again to upgrade: an existing .env is never overwritten, the database is dumped into
# <dir>/backups (named with the version it belonged to), the images are pulled and the stack is
# brought up again. If that dump fails, is empty or is cut short the upgrade stops before anything is
# changed. Run it with --rollback to put the images and the database back as the newest backup of
# another version left them.
#
# Options (flag, or the environment variable in brackets):
#   --dir <path>       install directory                  [HOSTPANEL_DIR, default /opt/hostpanel]
#   --version <x.y.z>  release to install, e.g. 0.4.0     [HOSTPANEL_VERSION, default: main / latest]
#   --no-tls           serve plain HTTP on 8080 behind your own TLS proxy [HOSTPANEL_TLS=off]
#   --rollback         restore the database and the version recorded with the newest backup of another version
#   --yes              do not ask for confirmation (rollback)
#   --keep <n>         newest backups to keep, older ones are removed, except the oldest backup of
#                      each version [HOSTPANEL_BACKUP_KEEP, default 5]
#   --keep-versions <n>  keep the oldest backup of this many of the newest versions only, so disk use
#                      does not grow with every release [HOSTPANEL_BACKUP_KEEP_VERSIONS, default 10]
#   --skip-backup      upgrade without a backup: the data is NOT protected, use it only when you are
#                      sure there is nothing worth keeping
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
# Compose service of the database. Every other service of the compose file is stopped for a restore.
DB_SERVICE="hostpanel-db"
# The named volume that holds the database files (the "name:" of pgdata in docker-compose.hub.yml).
DB_VOLUME="hostpanel_pgdata"
# How long to wait for a database the installer had to start: attempts times seconds. The environment
# variables exist so the tests do not wait.
DB_WAIT_ATTEMPTS="${HOSTPANEL_DB_WAIT_ATTEMPTS:-30}"
DB_WAIT_INTERVAL="${HOSTPANEL_DB_WAIT_INTERVAL:-2}"
# Backups live in this folder of the install directory; this many are kept unless told otherwise.
BACKUP_DIR_NAME="backups"
BACKUP_KEEP_DEFAULT=5
# The oldest backup of a version is kept for this many of the newest versions; the oldest backups of
# versions further back are trimmed like any other, so the folder does not grow with every release.
BACKUP_KEEP_VERSIONS_DEFAULT=10

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
Usage: install.sh [--dir <path>] [--version <x.y.z>] [--no-tls] [--keep <n>] [--keep-versions <n>] [--skip-backup]
       install.sh --rollback [--yes] [--dir <path>]
  --dir      install directory   (HOSTPANEL_DIR, default /opt/hostpanel)
  --version  release to install  (HOSTPANEL_VERSION, default main / latest)
  --no-tls   plain HTTP on 8080 for an install behind your own TLS proxy (HOSTPANEL_TLS=off)
  --rollback restore the database and the version recorded with the newest backup of another version (asks first)
  --yes      confirm a rollback in advance, for scripts
  --keep     newest backups to keep, plus the oldest of each version (HOSTPANEL_BACKUP_KEEP, default 5)
  --keep-versions  versions whose oldest backup is kept, the newest ones (HOSTPANEL_BACKUP_KEEP_VERSIONS, default 10)
  --skip-backup  upgrade without a database backup; the data is not protected, only when there is nothing to keep
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

# Prints the value of KEY in a dotenv file (empty when absent), without a CR or surrounding quotes. $1 file, $2 key
get_env() {
  local line value
  line="$(grep -E "^$2=" "$1" | tail -n 1 || true)"
  value="${line#*=}"
  # A hand-edited file may carry Windows line ends and quotes around the value; neither belongs to it.
  value="${value%$'\r'}"
  case "$value" in
    \"*\") value="${value#\"}"; value="${value%\"}" ;;
    \'*\') value="${value#\'}"; value="${value%\'}" ;;
  esac
  printf '%s' "$value"
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
  fetch "$1" "$2" || fail "Could not download https://raw.githubusercontent.com/${REPO}/${REF}/$1 (is ${REF} a real branch or release tag?)."
}

# Like download, but returns non-zero instead of stopping the run, for a caller that has to report
# the state it leaves behind. $1 repo path, $2 target name
fetch() {
  local url="https://raw.githubusercontent.com/${REPO}/${REF}/$1" tmp
  tmp="$(mktemp)"
  if ! curl -fsSL "$url" -o "$tmp" < /dev/null; then
    rm -f "$tmp"
    return 1
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

# ── Backup and rollback ───────────────────────────────────────────────────────

# Asks a yes or no question on the terminal; true only for an explicit yes. --yes answers it in advance.
# Without --yes and without a terminal it stops, so a script never rolls back by accident. $1 question
confirm() {
  local answer=''
  if [ "$ASSUME_YES" = "1" ]; then
    return 0
  fi
  have_tty || fail "$1 needs a confirmation and there is no terminal to ask on. Add --yes to confirm in advance."
  printf '  %s [y/N]: ' "$1" > /dev/tty
  IFS= read -r answer < /dev/tty || true
  case "$answer" in
    y|Y|yes|YES|Yes) return 0 ;;
    *) return 1 ;;
  esac
}

# Fails unless $1 is usable inside a backup file name: it comes from .env, which an operator edits. $2 label
check_image_tag() {
  if ! printf '%s' "$1" | grep -Eq '^[A-Za-z0-9][A-Za-z0-9._-]*$'; then
    fail "$2 '$1' is not a usable image tag (letters, digits, dots, dashes and underscores only)."
  fi
}

# Creates the backups folder readable by its owner only (dumps hold every record and the stored secrets)
# and removes the half-written dumps (*.sql.part) that an interrupted run left behind.
ensure_backup_dir() {
  local dir="${INSTALL_DIR}/${BACKUP_DIR_NAME}" f
  ( umask 077; mkdir -p "$dir" ) || fail "Cannot create ${dir}."
  chmod 700 "$dir"
  for f in "$dir"/*.sql.part; do
    if [ -f "$f" ]; then
      rm -f -- "$f"
    fi
  done
}

# Prints the file names of the backups with prefix $1, oldest first. The time sits right after the
# prefix in the name, so the shell's sorted glob is also the chronological order.
list_backups() {
  local f
  for f in "${INSTALL_DIR}/${BACKUP_DIR_NAME}/$1"-*.sql; do
    if [ -f "$f" ]; then
      printf '%s\n' "${f##*/}"
    fi
  done
}

# Prints the image tag recorded in a backup file name (<prefix>-<date>-<time>-<tag>.sql, the prefix being
# hostpanel or pre-rollback), or nothing when the name does not have that shape. $1 file name
file_tag() {
  printf '%s' "$1" | sed -nE 's/^(hostpanel|pre-rollback)-[0-9]{8}-[0-9]{6}-(.+)\.sql$/\2/p'
}

# Like file_tag, but stops the run when the name carries no version. $1 file name
backup_tag() {
  local tag
  tag="$(file_tag "$1")"
  if [ -z "$tag" ]; then
    fail "Backup file name '$1' does not carry a version; cannot roll back to it."
  fi
  printf '%s' "$tag"
}

# Removes the old backups with prefix $1. What stays: the newest $KEEP, the oldest backup of each of the
# newest $KEEP_VERSIONS versions (the one taken right before that version was replaced is the way back to
# it, and a re-run of the installer on a bad version must not push it out; versions further back lose
# theirs, or the folder would grow with every release) and the file named in $2, the one just made.
# The names are read into an array, so no pipe can be cut short under pipefail. $1 prefix, $2 file name to keep
prune_backups() {
  local names=() name tag i total first seen=' ' recent=' ' protected=' ' count=0
  while IFS= read -r name; do
    names+=("$name")
  done < <(list_backups "$1")
  total="${#names[@]}"
  first=$((total - KEEP))
  # The newest versions: walk from the newest file back and note each version when it first shows up.
  for ((i = total - 1; i >= 0; i--)); do
    tag="$(file_tag "${names[$i]}")"
    tag="${tag:-${names[$i]}}"
    case "$recent" in
      *" ${tag} "*) ;;
      *)
        recent="${recent}${tag} "
        if [ "$count" -lt "$KEEP_VERSIONS" ]; then
          protected="${protected}${tag} "
        fi
        count=$((count + 1)) ;;
    esac
  done
  for ((i = 0; i < total; i++)); do
    name="${names[$i]}"
    tag="$(file_tag "$name")"
    tag="${tag:-$name}"
    case "$seen" in
      *" ${tag} "*) ;;
      *)
        # The first file of a version in oldest-first order is the oldest of that version.
        seen="${seen}${tag} "
        case "$protected" in
          *" ${tag} "*) continue ;;
        esac ;;
    esac
    if [ "$i" -lt "$first" ] && [ "$name" != "${2:-}" ]; then
      rm -f -- "${INSTALL_DIR}/${BACKUP_DIR_NAME}/${name}"
    fi
  done
}

# True when file $1 holds a finished dump: pg_dump writes the "dump complete" line last, so a dump cut
# short by a crash, a full disk or a lost connection lacks it even when it is not empty. Recent pg_dump
# releases add one more line after it, so the last few lines are searched. $1 file
dump_is_complete() {
  local last
  [ -s "$1" ] || return 1
  last="$(tail -n 5 "$1")"
  grep -qx -- '-- PostgreSQL database dump complete' <<< "$last"
}

# True when dump $1 creates at least one table. A database without tables holds nothing worth a backup,
# and keeping such a file would only push a backup that does out of the retention window. $1 file
dump_has_tables() {
  grep -q '^CREATE TABLE ' "$1"
}

# Dumps the database through the running db container into $1, mode 600. The dump is written beside
# the target and only moved into place once pg_dump succeeded and the file is a finished dump, so a
# failed, empty or truncated dump never looks like a backup. The credentials stay inside the container:
# nothing secret is on this command line or in the output. $1 target file
dump_database() {
  local target="$1" part="$1.part"
  if ! ( umask 077; compose exec -T "$DB_SERVICE" sh -c 'pg_dump -U "$POSTGRES_USER" "$POSTGRES_DB"' > "$part" ); then
    rm -f "$part"
    return 1
  fi
  if ! dump_is_complete "$part"; then
    rm -f "$part"
    return 1
  fi
  chmod 600 "$part"
  mv "$part" "$target"
}

# True when the database container is running.
db_is_running() {
  [ -n "$(compose ps -q "$DB_SERVICE" 2>/dev/null || true)" ]
}

# True when the volume that holds the database files exists, whether or not its container runs.
database_volume_exists() {
  docker volume inspect "$DB_VOLUME" >/dev/null 2>&1
}

# Waits until the database accepts connections. Fails after DB_WAIT_ATTEMPTS tries.
wait_for_database() {
  local attempt
  info "Waiting for the database to accept connections"
  for ((attempt = 1; attempt <= DB_WAIT_ATTEMPTS; attempt++)); do
    if compose exec -T "$DB_SERVICE" sh -c 'pg_isready -q -U "$POSTGRES_USER" -d "$POSTGRES_DB"' >/dev/null 2>&1; then
      return 0
    fi
    sleep "$DB_WAIT_INTERVAL"
  done
  return 1
}

# Makes sure the database, and only the database, is up and ready: a stopped db container is started
# alone (no dependencies, no application service), then pg_isready is awaited.
start_database() {
  if ! db_is_running; then
    info "Starting the database (${DB_SERVICE}) on its own; nothing else is started"
    compose up -d --no-deps "$DB_SERVICE" || return 1
  fi
  wait_for_database
}

# Dumps the database before an upgrade into backups/hostpanel-<date>-<time>-<IMAGE_TAG>.sql (UTC), so
# the file names the version whose images it belongs to, then trims old backups. Stops the whole run
# when the dump cannot be taken: the caller has not changed anything yet. A stack whose database never
# existed (the first install stopped before it started) has nothing to back up and goes on. Sets BACKUP_FILE
# and BACKUP_TAG.
backup_before_upgrade() {
  local env="${INSTALL_DIR}/.env" stamp
  BACKUP_FILE=''
  BACKUP_TAG=''
  if [ "$SKIP_BACKUP" = "1" ]; then
    warn "--skip-backup: the database is NOT backed up before this upgrade."
    return 0
  fi
  [ -f "${INSTALL_DIR}/${COMPOSE_FILE}" ] \
    || fail "${INSTALL_DIR}/${COMPOSE_FILE} is missing, so the database cannot be backed up. Nothing was changed. Put the compose file of the installed version back (https://raw.githubusercontent.com/${REPO}/v<version>/docker/${COMPOSE_FILE}) and run the installer again."
  BACKUP_TAG="$(get_env "$env" IMAGE_TAG)"
  BACKUP_TAG="${BACKUP_TAG:-latest}"
  check_image_tag "$BACKUP_TAG" "IMAGE_TAG in ${env}"
  if ! db_is_running && ! database_volume_exists; then
    BACKUP_TAG=''
    info "There is no database yet (an earlier install stopped before it started), so there is nothing to back up."
    return 0
  fi
  start_database \
    || fail "The database (${DB_SERVICE}) could not be started or did not become ready, so it cannot be backed up. Nothing was changed. Look at it with:  cd ${INSTALL_DIR} && docker compose -f ${COMPOSE_FILE} logs ${DB_SERVICE}"

  ensure_backup_dir
  stamp="$(date -u +%Y%m%d-%H%M%S)"
  BACKUP_FILE="${INSTALL_DIR}/${BACKUP_DIR_NAME}/hostpanel-${stamp}-${BACKUP_TAG}.sql"
  info "Backing up the database (version ${BACKUP_TAG}) to ${BACKUP_FILE}"
  dump_database "$BACKUP_FILE" \
    || fail "The database backup failed, came out empty or was cut short. The upgrade was stopped before anything was changed."
  if ! dump_has_tables "$BACKUP_FILE"; then
    rm -f -- "$BACKUP_FILE"
    BACKUP_FILE=''
    BACKUP_TAG=''
    info "The database holds no tables yet, so there is nothing to back up. Existing backups are left as they are."
    return 0
  fi
  success "Database backed up ($(wc -c < "$BACKUP_FILE" | tr -d '[:space:]') bytes, mode 600)"
  prune_backups hostpanel "${BACKUP_FILE##*/}"
  if [ "$BACKUP_TAG" = "latest" ]; then
    warn "This install runs the moving tag 'latest', so a rollback cannot tell which version to return to. Pass --version <x.y.z> on upgrades to make the backups restorable."
  fi
}

# Runs inside the db container, with the dump on its stdin. The dump is restored into a temporary
# database first; only a restore that went through is renamed into the place of the live database,
# so a failed restore leaves the live database exactly as it was. A rename is refused while any
# connection holds the database, and the server keeps background workers (autovacuum above all) that
# connect to a database on their own, also to the temporary one right after the restore wrote into it.
# Autovacuum cannot be switched off for one database, so each rename ends the connections that hold
# the database first and is tried again a few times; a worker that came back in between loses the
# next try. Only a rename the server refuses as "in use" (SQLSTATE 55006) counts as busy and is tried
# again; any other refusal (a missing database, a permission, a full disk) stops at once and the
# server's own message is printed, so it is not mistaken for a busy database. Exit status: 0 restored,
# 1 the backup itself could not be loaded (live database untouched), 3 the swap broke half way and the
# live database is missing (the old data is then in <name>_replaced and the restored data in
# <name>_restore), 4 the rename stayed busy after every try and the live database was put back as it
# was (the backup itself loaded fine), 5 a database command was refused for another reason before the
# swap or during it (the message is printed) and the live database was not changed or was put back.
# Any other status means the script was cut off (killed, docker failed): the caller reads the state.
read -r -d '' RESTORE_SCRIPT <<'SQLSCRIPT' || true
LIVE="$POSTGRES_DB"
NEW="${LIVE}_restore"
OLD="${LIVE}_replaced"
DBUSER="$POSTGRES_USER"
RENAME_TRIES=10
admin() { psql -q -v ON_ERROR_STOP=1 -v VERBOSITY=verbose -U "$DBUSER" -d postgres "$@"; }
drop_temp() { dropdb -U "$DBUSER" --force --if-exists "$1"; }
rename_db() {
  try=0
  while [ "$try" -lt "$RENAME_TRIES" ]; do
    try=$((try + 1))
    admin -c "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '$1' AND pid <> pg_backend_pid()" > /dev/null 2>&1
    if err="$(admin -c "ALTER DATABASE \"$1\" RENAME TO \"$2\"" 2>&1)"; then
      return 0
    fi
    case "$err" in
      *"ERROR:  55006"*|*"is being accessed by other users"*) ;;
      *)
        echo "renaming database $1 to $2 failed: $(printf '%s\n' "$err" | head -n 1)" >&2
        return 2 ;;
    esac
    echo "database $1 is busy, trying the rename again ($try of $RENAME_TRIES)" >&2
    sleep 1
  done
  return 1
}
drop_temp "$NEW" || exit 5
drop_temp "$OLD" || exit 5
createdb -U "$DBUSER" "$NEW" || exit 5
if ! psql -q -v ON_ERROR_STOP=1 --single-transaction -U "$DBUSER" -d "$NEW"; then
  drop_temp "$NEW"
  exit 1
fi
rename_db "$LIVE" "$OLD"
rc=$?
if [ "$rc" -ne 0 ]; then
  drop_temp "$NEW"
  if [ "$rc" -eq 1 ]; then
    exit 4
  fi
  exit 5
fi
rename_db "$NEW" "$LIVE"
rc=$?
if [ "$rc" -ne 0 ]; then
  if rename_db "$OLD" "$LIVE"; then
    drop_temp "$NEW"
    if [ "$rc" -eq 1 ]; then
      exit 4
    fi
    exit 5
  fi
  exit 3
fi
drop_temp "$OLD" || true
exit 0
SQLSCRIPT

# Runs inside the db container and prints which of the live database, <name>_replaced and <name>_restore
# exist, one word per line (live, replaced, restore). Exit status 2 when the list cannot be read.
read -r -d '' STATE_SCRIPT <<'SQLSCRIPT' || true
present() {
  found="$(psql -q -U "$POSTGRES_USER" -d postgres -tA -c "SELECT 1 FROM pg_database WHERE datname = '$1'")" || exit 2
  [ "$found" = "1" ]
}
if present "$POSTGRES_DB"; then echo live; fi
if present "${POSTGRES_DB}_replaced"; then echo replaced; fi
if present "${POSTGRES_DB}_restore"; then echo restore; fi
exit 0
SQLSCRIPT

# Restores the database from dump $1 (see RESTORE_SCRIPT). The dump is read from stdin of the container
# on purpose: this is the one place a file is fed to a command.
restore_dump() {
  ( cd "$INSTALL_DIR" && docker compose -f "$COMPOSE_FILE" exec -T "$DB_SERVICE" sh -c "$RESTORE_SCRIPT" < "$1" )
}

# Prints the command that puts dump $1 back by hand, replacing the live database. $1 dump file
manual_restore_command() {
  printf "      cd %q && docker compose -f %s exec -T %s sh -c '%s' < %q\n" \
    "$INSTALL_DIR" "$COMPOSE_FILE" "$DB_SERVICE" \
    'dropdb -U "$POSTGRES_USER" --force --if-exists "$POSTGRES_DB" && createdb -U "$POSTGRES_USER" "$POSTGRES_DB" && psql -q -v ON_ERROR_STOP=1 --single-transaction -U "$POSTGRES_USER" "$POSTGRES_DB"' \
    "$1"
}

# Prints the command that renames the database <name>$1 (the suffix, _replaced or _restore) into the
# place of the missing live database. $1 suffix
rename_into_place_command() {
  printf "      cd %q && docker compose -f %s exec -T %s sh -c '%s'\n" \
    "$INSTALL_DIR" "$COMPOSE_FILE" "$DB_SERVICE" \
    'psql -q -U "$POSTGRES_USER" -d postgres -c "ALTER DATABASE \"${POSTGRES_DB}'"$1"'\" RENAME TO \"${POSTGRES_DB}\""'
}

# Prints the command that starts the whole stack again.
start_command() {
  printf '      cd %q && docker compose -f %s up -d\n' "$INSTALL_DIR" "$COMPOSE_FILE"
}

# Prints the command that runs the rollback again.
rerun_command() {
  printf '      curl -fsSL https://raw.githubusercontent.com/%s/main/install.sh | bash -s -- --rollback --dir %q\n' "$REPO" "$INSTALL_DIR"
}

# Stops the run after something went wrong during a rollback, and says in what state the install is
# left and what to run. $1 what failed, $2 the state, $3 the commands to run (already formatted)
fail_with_state() {
  printf '%s[error]%s %s\n' "$RED" "$NC" "$1" >&2
  printf '        State: %s\n' "$2" >&2
  printf '        Run:\n%s\n' "$3" >&2
  exit 1
}

# Stops the run, before anything was changed, when an earlier rollback broke half way: the live database
# is then missing, the data from before that rollback is in <name>_replaced and the restored backup in
# <name>_restore. Another rollback would only fail to save the missing database, and its first step
# drops both of them. Expects the database to be running.
check_no_half_swap() {
  local states status=0 name
  name="$(get_env "${INSTALL_DIR}/.env" POSTGRES_DB)"
  states="$(compose exec -T "$DB_SERVICE" sh -c "$STATE_SCRIPT")" || status=$?
  if [ "$status" -ne 0 ]; then
    fail "Could not read the list of databases, so the rollback was not started. Nothing was changed."
  fi
  case "$states" in
    *live*) return 0 ;;
  esac
  fail_with_state "An earlier rollback stopped half way: the live database ${name} is missing, so this one was not started." \
    "nothing was changed by this run. The data from before that earlier rollback is in the database ${name}_replaced and the backup it restored is in ${name}_restore, both inside the database container. The application services are probably still STOPPED. This run would drop both, so it refuses until ${name} exists again." \
    "$(printf '%s\n' '      Put the data from before the earlier rollback back:'; rename_into_place_command _replaced; printf '%s\n' '      or keep the restored backup instead:'; rename_into_place_command _restore; printf '%s\n' '      then start the stack, or run the rollback again:'; start_command)"
}

# Prints the command that lists the databases of the server, to see by hand what a cut off restore left.
list_databases_command() {
  printf "      cd %q && docker compose -f %s exec -T %s sh -c '%s'\n" \
    "$INSTALL_DIR" "$COMPOSE_FILE" "$DB_SERVICE" \
    'psql -q -U "$POSTGRES_USER" -d postgres -c "SELECT datname FROM pg_database ORDER BY datname"'
}

# Reports a restore that ended with a status RESTORE_SCRIPT never returns (it was killed, or docker itself
# failed), and stops the run. Nothing is assumed: the databases that exist now are read and the message says
# what that means. $1 status, $2 backup file, $3 safety copy text, $4 database name
report_cut_off_restore() {
  local status="$1" file="$2" saved="$3" name="$4" states list='' has_live=0 has_replaced=0 has_restore=0 word
  if ! states="$(compose exec -T "$DB_SERVICE" sh -c "$STATE_SCRIPT" 2>/dev/null)"; then
    fail_with_state "The restore was cut off (exit status ${status}) and the list of databases could not be read afterwards." \
      "UNKNOWN. The application services are STOPPED. It is not known whether the live database ${name} is intact, missing or already replaced by the backup ${file}. ${saved} Look at what exists first." \
      "$(printf '%s\n' '      List the databases (the database container may need to be started first):'; list_databases_command; printf '%s\n' '      If the live database is missing, rename the old data (or the restored backup) into place:'; rename_into_place_command _replaced; rename_into_place_command _restore; printf '%s\n' '      then start the stack:'; start_command)"
  fi
  for word in $states; do
    list="${list:+${list}, }${word}"
    case "$word" in
      live) has_live=1 ;;
      replaced) has_replaced=1 ;;
      restore) has_restore=1 ;;
    esac
  done
  if [ "$has_live" = "0" ]; then
    fail_with_state "The restore was cut off (exit status ${status}) while the databases were being swapped." \
      "the application services are STOPPED and the live database ${name} is MISSING. Of the databases ${name}_replaced (the data from before the rollback) and ${name}_restore (the backup ${file}) inside the database container, these exist: ${list:-neither}. ${saved} Running the rollback again is refused until ${name} exists." \
      "$(printf '%s\n' '      Put the data from before the rollback back (if it is listed above):'; rename_into_place_command _replaced; printf '%s\n' '      or use the restored backup (if it is listed above):'; rename_into_place_command _restore; printf '%s\n' '      then start the stack:'; start_command)"
  fi
  if [ "$has_replaced" = "1" ]; then
    fail_with_state "The restore was cut off (exit status ${status}) right after the swap." \
      "the application services are STOPPED. The live database ${name} exists and, because ${name}_replaced still exists, it most likely holds the restored backup ${file}; the data from before the rollback is in ${name}_replaced. Check the data before you start the stack. ${saved}" \
      "$(printf '%s\n' '      If the data is the restored backup, start the stack:'; start_command; printf '%s\n' '      To see all databases:'; list_databases_command)"
  fi
  if [ "$has_restore" = "1" ]; then
    fail_with_state "The restore was cut off (exit status ${status}) before the swap." \
      "the application services are STOPPED. The live database ${name} exists and was not swapped; ${name}_restore is a leftover of the cut off restore (the next rollback removes it). ${saved}" \
      "$(printf '%s\n' '      Start the stack again, or run the rollback again:'; start_command; rerun_command)"
  fi
  fail_with_state "The restore was cut off (exit status ${status}) and left no temporary database." \
    "the application services are STOPPED. The live database ${name} exists, but it cannot be told from here whether it holds the data from before the rollback or the restored backup ${file}. Look at the data before you start the stack. ${saved}" \
    "$(printf '%s\n' '      If the data is right, start the stack:'; start_command; printf '%s\n' '      If it is not, run the rollback again:'; rerun_command)"
}

# Reports a restore that failed, by the exit status of RESTORE_SCRIPT, and stops the run. Every message says
# which data is where. $1 status, $2 backup file, $3 safety copy (empty when the database had no tables)
restore_failed() {
  local status="$1" file="$2" safety="$3" name saved
  name="$(get_env "${INSTALL_DIR}/.env" POSTGRES_DB)"
  if [ -n "$safety" ]; then
    saved="The database as it was before the rollback is saved in ${safety}."
  else
    saved="The database had no tables, so no copy was made and there is nothing to save."
  fi
  if [ "$status" = "3" ]; then
    fail_with_state "The restore failed half way: the databases were being swapped and the swap broke." \
      "the application services are STOPPED and the live database ${name} is MISSING. The data from before the rollback is still in the database ${name}_replaced, and the backup ${file} is loaded in the database ${name}_restore (both inside the database container). ${saved} Running the rollback again is refused until ${name} exists." \
      "$(printf '%s\n' '      Put the data from before the rollback back:'; rename_into_place_command _replaced; printf '%s\n' '      or use the restored backup:'; rename_into_place_command _restore; printf '%s\n' '      then start the stack:'; start_command)"
  fi
  if [ "$status" = "4" ]; then
    fail_with_state "The backup was restored into a temporary database, but it could not be put in place: the database stayed busy (a background database worker kept connecting) through every try. The backup ${file} is most likely fine." \
      "the application services are STOPPED. The live database ${name} was put back and was not changed. ${saved}" \
      "$(printf '%s\n' '      Run the rollback again (it is safe to repeat):'; rerun_command; printf '%s\n' '      or start the stack as it is:'; start_command)"
  fi
  if [ "$status" = "5" ]; then
    fail_with_state "A database command was refused for a reason other than the backup: the server's message is printed above this error. This is not a busy database, and it says nothing against the backup ${file}." \
      "the application services are STOPPED. The live database ${name} was not changed (or was put back). ${saved}" \
      "$(printf '%s\n' '      Fix the cause named above, then run the rollback again (it is safe to repeat):'; rerun_command; printf '%s\n' '      or start the stack as it is:'; start_command)"
  fi
  if [ "$status" != "1" ]; then
    report_cut_off_restore "$status" "$file" "$saved" "$name"
  fi
  if [ -n "$safety" ]; then
    fail_with_state "The restore failed: the backup ${file} could not be loaded, so it may be damaged." \
      "the application services are STOPPED. The live database ${name} was not changed: the restore ran in a temporary database that was removed again. ${saved}" \
      "$(printf '%s\n' '      Start the stack again:'; start_command; printf '%s\n' '      Only if the database looks wrong, put the saved data back first:'; manual_restore_command "$safety")"
  fi
  fail_with_state "The restore failed: the backup ${file} could not be loaded, so it may be damaged." \
    "the application services are STOPPED. The live database ${name} was not changed: the restore ran in a temporary database that was removed again. ${saved}" \
    "$(printf '%s\n' '      Start the stack again:'; start_command)"
}

# Orders two whole numbers written in decimal, of any length, without turning them into numbers (a part
# of a version may be longer than the shell's integer). Sets ORDER to -1, 0 or 1. $1 number, $2 number
compare_numbers() {
  local x="${1#"${1%%[!0]*}"}" y="${2#"${2%%[!0]*}"}"
  if [ "${#x}" -ne "${#y}" ]; then
    if [ "${#x}" -gt "${#y}" ]; then
      ORDER=1
    else
      ORDER=-1
    fi
    return 0
  fi
  compare_text "$x" "$y"
}

# Orders two words by the byte value of their characters, so the result does not depend on the locale
# (a locale may sort rc.a before rc.B, semver does not). Sets ORDER to -1, 0 or 1. $1 word, $2 word
compare_text() {
  local x="$1" y="$2" i n a b
  n="${#x}"
  if [ "${#y}" -lt "$n" ]; then
    n="${#y}"
  fi
  for ((i = 0; i < n; i++)); do
    printf -v a '%d' "'${x:i:1}"
    printf -v b '%d' "'${y:i:1}"
    if [ "$a" -ne "$b" ]; then
      if [ "$a" -gt "$b" ]; then
        ORDER=1
      else
        ORDER=-1
      fi
      return 0
    fi
  done
  if [ "${#x}" -gt "${#y}" ]; then
    ORDER=1
  elif [ "${#x}" -lt "${#y}" ]; then
    ORDER=-1
  else
    ORDER=0
  fi
}

# True when version $1 is newer than version $2, by the precedence rules of semver.org: a number list
# (0.4.0; a missing part counts as 0, so 0.4 is 0.4.0), then an optional pre-release after a dash. A
# version with a pre-release is older than the same numbers without one; two pre-releases are compared
# identifier by identifier at the dots, numbers as numbers (rc.10 is after rc.2), a number before any word,
# words by byte value, and the one with more identifiers after the one it starts with. Build metadata after
# a plus is ignored. False when either does not read like that, so a tag nobody can order is never called
# newer. $1 version, $2 version
version_is_newer() {
  local re='^([0-9]+(\.[0-9]+)*)(-([0-9A-Za-z-]+(\.[0-9A-Za-z-]+)*))?(\+[0-9A-Za-z-]+(\.[0-9A-Za-z-]+)*)?$'
  local a=() b=() pa pb i n x y ORDER=0
  [[ "$1" =~ $re ]] || return 1
  IFS=. read -r -a a <<< "${BASH_REMATCH[1]}"
  pa="${BASH_REMATCH[4]:-}"
  [[ "$2" =~ $re ]] || return 1
  IFS=. read -r -a b <<< "${BASH_REMATCH[1]}"
  pb="${BASH_REMATCH[4]:-}"
  n="${#a[@]}"
  if [ "${#b[@]}" -gt "$n" ]; then
    n="${#b[@]}"
  fi
  for ((i = 0; i < n; i++)); do
    compare_numbers "${a[$i]:-0}" "${b[$i]:-0}"
    if [ "$ORDER" -ne 0 ]; then
      [ "$ORDER" -gt 0 ]
      return
    fi
  done
  if [ -z "$pa" ] && [ -z "$pb" ]; then
    return 1
  fi
  if [ -z "$pa" ]; then
    return 0
  fi
  if [ -z "$pb" ]; then
    return 1
  fi
  IFS=. read -r -a a <<< "$pa"
  IFS=. read -r -a b <<< "$pb"
  n="${#a[@]}"
  if [ "${#b[@]}" -lt "$n" ]; then
    n="${#b[@]}"
  fi
  for ((i = 0; i < n; i++)); do
    x="${a[$i]}"
    y="${b[$i]}"
    if [[ "$x" =~ ^[0-9]+$ ]] && [[ "$y" =~ ^[0-9]+$ ]]; then
      compare_numbers "$x" "$y"
    elif [[ "$x" =~ ^[0-9]+$ ]]; then
      ORDER=-1
    elif [[ "$y" =~ ^[0-9]+$ ]]; then
      ORDER=1
    else
      compare_text "$x" "$y"
    fi
    if [ "$ORDER" -ne 0 ]; then
      [ "$ORDER" -gt 0 ]
      return
    fi
  done
  [ "${#a[@]}" -gt "${#b[@]}" ]
}

# --rollback: puts the images and the database back to the state of the newest backup that belongs to
# another version than the one installed now (a re-run of the installer on a bad version makes newer
# backups of that same version; they are skipped). The order is: confirm, stop the application, save the
# current database (pre-rollback-*), restore, and only then replace the compose files, so a failure
# early on leaves the current files in place. Old pre-rollback files are trimmed only after a restore went through.
rollback() {
  local env="${INSTALL_DIR}/.env" name='' tag current stamp file safety='' status=0 i candidate svc
  local forward=0 question names=() services=()
  [ -f "$env" ] || fail "No ${env}: there is no install here to roll back."
  [ -f "${INSTALL_DIR}/${COMPOSE_FILE}" ] || fail "${INSTALL_DIR}/${COMPOSE_FILE} is missing; cannot roll back."
  current="$(get_env "$env" IMAGE_TAG)"
  current="${current:-unknown}"
  check_image_tag "$current" "IMAGE_TAG in ${env}"
  # The restore swaps databases by renaming them from the maintenance database, which is named postgres,
  # so it cannot replace a live database of that name. Said before anything is touched.
  if [ "$(get_env "$env" POSTGRES_DB)" = "postgres" ]; then
    fail "POSTGRES_DB in ${env} is 'postgres', the server's own maintenance database. The rollback swaps databases by renaming them from that one, so it cannot replace it. Nothing was changed. Restore the backup by hand, connected to the template1 database to drop and create it (dropping the database you are connected to fails): see 'A database named postgres' in the rollback guide, docs/en/operators/rollback.md."
  fi

  while IFS= read -r candidate; do
    names+=("$candidate")
  done < <(list_backups hostpanel)
  [ "${#names[@]}" -gt 0 ] || fail "No backup found in ${INSTALL_DIR}/${BACKUP_DIR_NAME}; there is nothing to roll back to."
  for ((i = ${#names[@]} - 1; i >= 0; i--)); do
    tag="$(file_tag "${names[$i]}")"
    if [ -n "$tag" ] && [ "$tag" != "$current" ]; then
      name="${names[$i]}"
      break
    fi
  done
  [ -n "$name" ] || fail "Every backup in ${INSTALL_DIR}/${BACKUP_DIR_NAME} was taken on the version now installed (${current}), so none of them leads to another version. Nothing was changed."
  file="${INSTALL_DIR}/${BACKUP_DIR_NAME}/${name}"
  if [ "$tag" = "latest" ]; then
    fail "The newest backup of another version (${name}) was taken on the moving tag 'latest', so it does not say which version to return to. Restore it by hand: see the rollback guide."
  fi
  check_image_tag "$tag" "Version recorded in ${name}"
  dump_is_complete "$file" || fail "The backup ${file} is empty or was cut short (it does not end with the pg_dump completion line); refusing to roll back to it."

  printf '\n%sRollback%s\n' "$BOLD" "$NC"
  printf '  Backup   : %s\n' "$file"
  printf '  Version  : %s (now %s)\n' "$tag" "$current"
  if version_is_newer "$tag" "$current"; then
    forward=1
    printf '\n  %sThis moves FORWARD, not back:%s the backup was taken on %s, which is newer than the\n' "$BOLD" "$NC" "$tag"
    printf '  installed %s. That happens when a rollback is repeated: the backups of the version you\n' "$current"
    printf '  left are the newest ones now. If you meant to go further back, answer no and move the\n'
    printf '  newer files out of %s/%s first.\n\n' "$INSTALL_DIR" "$BACKUP_DIR_NAME"
  fi
  printf '  This replaces the database with the backup. Everything written since it was taken\n'
  printf '  (orders, payments, tickets) is lost, after a copy of the current database is saved.\n\n'
  if [ "$forward" = "1" ]; then
    question="Move FORWARD to ${tag}?"
  else
    question="Roll back to ${tag}?"
  fi
  if ! confirm "$question"; then
    info "Rollback cancelled. Nothing was changed."
    return 0
  fi

  # Everything that can still fail without harm comes first: the service list, then the database itself.
  while IFS= read -r svc; do
    if [ -n "$svc" ] && [ "$svc" != "$DB_SERVICE" ]; then
      services+=("$svc")
    fi
  done < <(compose config --services)
  [ "${#services[@]}" -gt 0 ] || fail "Could not read the services of ${COMPOSE_FILE} ('docker compose config --services' failed). Nothing was changed."
  start_database \
    || fail "The database (${DB_SERVICE}) could not be started or did not become ready. Nothing was changed. Look at it with:  cd ${INSTALL_DIR} && docker compose -f ${COMPOSE_FILE} logs ${DB_SERVICE}"
  check_no_half_swap

  info "Stopping every service except the database: ${services[*]}"
  compose stop "${services[@]}" \
    || fail_with_state "Could not stop the application services." \
      "some of them may be stopped and some still running; the database was not touched." \
      "$(printf '%s\n' '      Start the stack again, or run the rollback again:'; start_command)"

  ensure_backup_dir
  stamp="$(date -u +%Y%m%d-%H%M%S)"
  safety="${INSTALL_DIR}/${BACKUP_DIR_NAME}/pre-rollback-${stamp}-${current}.sql"
  info "Saving the current database to ${safety}"
  dump_database "$safety" \
    || fail_with_state "Could not save the current database, so the rollback was stopped before replacing it." \
      "the application services are STOPPED; the database was not changed." \
      "$(printf '%s\n' '      Start the stack again:'; start_command)"
  if ! dump_has_tables "$safety"; then
    rm -f -- "$safety"
    safety=''
    info "The current database holds no tables, so no safety copy was kept."
  fi

  info "Restoring the database from ${name}"
  restore_dump "$file" || status=$?
  if [ "$status" -ne 0 ]; then
    restore_failed "$status" "$file" "$safety"
  fi
  success "Database restored"
  # Only now that the restore went through may old safety copies make room for the new one.
  prune_backups pre-rollback "${safety##*/}"

  REF="v${tag}"
  info "Downloading the ${tag} compose file and Caddyfile"
  if ! fetch "docker/${COMPOSE_FILE}" "$COMPOSE_FILE" || ! fetch "docker/caddy/Caddyfile" "Caddyfile"; then
    fail_with_state "Could not download the files of version ${tag} (is ${REF} a real release?)." \
      "the database is restored from ${name}; the compose file and Caddyfile may be partly replaced, IMAGE_TAG is still ${current} and the application services are STOPPED." \
      "$(printf '%s\n' '      When the download works again, run the rollback once more (it saves the current database first), or start the version that is installed:'; start_command)"
  fi

  set_env "$env" IMAGE_TAG "$tag"
  IMAGE_TAG="$tag"
  info "IMAGE_TAG set to ${tag}"
  info "Pulling the images"
  compose pull \
    || fail_with_state "Could not pull the images of version ${tag}." \
      "the database is restored, the files and IMAGE_TAG are those of ${tag}, the application services are STOPPED." \
      "$(printf '      cd %q && docker compose -f %s pull && docker compose -f %s up -d\n' "$INSTALL_DIR" "$COMPOSE_FILE" "$COMPOSE_FILE")"
  fix_volume_ownership
  info "Starting the stack"
  compose up -d \
    || fail_with_state "Could not start the stack of version ${tag}." \
      "the database is restored, the files and IMAGE_TAG are those of ${tag}, some services may not be running." \
      "$(printf '      cd %q && docker compose -f %s up -d\n      cd %q && docker compose -f %s logs\n' "$INSTALL_DIR" "$COMPOSE_FILE" "$INSTALL_DIR" "$COMPOSE_FILE")"

  if wait_for_api; then
    printf '\n%sRolled back to %s.%s\n' "$BOLD" "$tag" "$NC"
    if [ -n "$safety" ]; then
      printf '  The database before the rollback is saved in %s\n' "$safety"
    fi
  else
    exit 1
  fi
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
  if [ -n "$BACKUP_FILE" ]; then
    printf '\n%sBackup%s\n' "$BOLD" "$NC"
    printf '  The database was backed up before this upgrade (it belongs to version %s):\n' "$BACKUP_TAG"
    printf '       %s\n' "$BACKUP_FILE"
    printf '  If the new version misbehaves, go back to that version and database with:\n'
    printf '       curl -fsSL https://raw.githubusercontent.com/%s/main/install.sh | bash -s -- --rollback --dir %s\n' "$REPO" "$INSTALL_DIR"
    printf '  It asks before it replaces anything.\n'
  fi
  printf '\nUpgrade any time by running the install command again.\n'
}

# ── Main ──────────────────────────────────────────────────────────────────────

# Entry point. The whole script is a function called on the last line, so under `curl | bash`
# bash has read it completely before anything runs and a truncated download runs nothing.
main() {
  INSTALL_DIR="${HOSTPANEL_DIR:-$INSTALL_DIR_DEFAULT}"
  HOSTPANEL_VERSION="${HOSTPANEL_VERSION:-}"
  TLS_ASKED="${HOSTPANEL_TLS:-}"
  KEEP="${HOSTPANEL_BACKUP_KEEP:-$BACKUP_KEEP_DEFAULT}"
  KEEP_VERSIONS="${HOSTPANEL_BACKUP_KEEP_VERSIONS:-$BACKUP_KEEP_VERSIONS_DEFAULT}"
  ROLLBACK=0
  ASSUME_YES=0
  SKIP_BACKUP=0
  BACKUP_FILE=''
  BACKUP_TAG=''

  while [ "$#" -gt 0 ]; do
    case "$1" in
      --dir)      [ "$#" -ge 2 ] || fail "--dir needs a path"; INSTALL_DIR="$2"; shift 2 ;;
      --version)  [ "$#" -ge 2 ] || fail "--version needs a value"; HOSTPANEL_VERSION="$2"; shift 2 ;;
      --no-tls)   TLS_ASKED="off"; shift ;;
      --rollback) ROLLBACK=1; shift ;;
      --yes|-y)   ASSUME_YES=1; shift ;;
      --keep)     [ "$#" -ge 2 ] || fail "--keep needs a number"; KEEP="$2"; shift 2 ;;
      --keep-versions) [ "$#" -ge 2 ] || fail "--keep-versions needs a number"; KEEP_VERSIONS="$2"; shift 2 ;;
      --skip-backup) SKIP_BACKUP=1; shift ;;
      -h|--help)  usage; exit 0 ;;
      *)          fail "Unknown option: $1 (see --help)" ;;
    esac
  done

  TLS="${TLS_ASKED:-on}"
  case "$TLS" in
    on|off) ;;
    *) fail "HOSTPANEL_TLS must be 'on' or 'off' (got '$TLS')." ;;
  esac

  case "$KEEP" in
    ''|*[!0-9]*|0) fail "--keep / HOSTPANEL_BACKUP_KEEP must be a whole number of at least 1 (got '$KEEP')." ;;
  esac
  case "$KEEP_VERSIONS" in
    ''|*[!0-9]*|0) fail "--keep-versions / HOSTPANEL_BACKUP_KEEP_VERSIONS must be a whole number of at least 1 (got '$KEEP_VERSIONS')." ;;
  esac

  if [ "$ROLLBACK" = "1" ]; then
    # The version to return to is the one recorded with the backup, never one typed here.
    [ -z "$HOSTPANEL_VERSION" ] || fail "--rollback returns to the version recorded with the latest backup; do not combine it with --version."
    printf '%sHostpanel rollback%s\n' "$BOLD" "$NC"
    check_prerequisites
    INSTALL_DIR="$(cd "$INSTALL_DIR" 2>/dev/null && pwd)" || fail "${INSTALL_DIR} does not exist: there is no install here to roll back."
    rollback
    return 0
  fi

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

  # An existing install is backed up before anything below changes it (the downloads replace the
  # compose file and Caddyfile). A failed backup ends the run here.
  if [ -f "${INSTALL_DIR}/.env" ]; then
    backup_before_upgrade
  fi

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

# HOSTPANEL_SOURCE_ONLY=1 loads the functions without running anything, for the tests in scripts/.
if [ "${HOSTPANEL_SOURCE_ONLY:-0}" != "1" ]; then
  main "$@"
fi
