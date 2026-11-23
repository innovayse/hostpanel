#!/usr/bin/env bash
# shellcheck disable=SC2034 # the variables below are read by the helpers that source this file
# hp-common.sh: shared by the CWP ops helpers (ensure-website, remove-website, ensure-database, remove-database).
# Sourced, never run. It holds what all four need: the tool and tree paths, input validation, the root-owned
# registry of what the agent created, the backup/restore of edited files, and the one RESULT line the agent parses.
#
# Test seam: HP_ROOT prefixes every path and tool below (empty on a real server). The agent never forwards it from a
# request; it only reaches a helper through the agent's own fixed environment, so a unit test can point the helper at a
# stand-in tree with fake tools.
#
# Paths written into Apache or php-fpm configuration stay logical (no HP_ROOT); only the files the helper touches
# are prefixed.

ROOT="${HP_ROOT:-}"
BIN="$ROOT/usr/bin"
STATE="$ROOT/var/lib/hostpanel-ssh-key-agent"
VHOSTS="$ROOT/usr/local/apache/conf.d/vhosts"
APACHECTL="$ROOT/usr/local/apache/bin/apachectl"
FPM_PREFIX="/opt/alt/php-fpm"
MARKER="# hostpanel-ssh-key-agent managed"
PHP_BEGIN="# hostpanel-ssh-key-agent php begin"
PHP_END="# hostpanel-ssh-key-agent php end"
WEB_GROUP="nobody"

ACCOUNT_RE='^[a-z][a-z0-9]{1,15}$'
HOSTNAME_RE='^([a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?\.)+[a-z]{2,63}$'
DOCROOT_RE='^[A-Za-z0-9_-][A-Za-z0-9._-]{0,63}(/[A-Za-z0-9_-][A-Za-z0-9._-]{0,63}){0,5}$'
PHP_RE='^[0-9]\.[0-9]{1,2}$'
LABEL_RE='^[a-z][a-z0-9]{0,14}$'

# Exit codes the agent maps to its stable error codes.
EXIT_BAD_ARGS=2
EXIT_NOT_OWNED=3
EXIT_CONFLICT=4
EXIT_PHP_UNSUPPORTED=5
EXIT_LIMIT=6

ACCOUNT=""
HOME_L=""
HOME_DIR=""
BK=""
BK_N=0
DIRTY=0
WORK=""

# hp_say <text>: a progress line for whoever runs the helper by hand; the agent ignores everything but RESULT.
hp_say() { echo "[hostpanel] $*"; }

# hp_die <exit code> <text>: refuses with a reason on stderr (the agent discards it, an administrator reads it).
hp_die() { local code="$1"; shift; echo "$*" >&2; exit "$code"; }

# hp_result <json>: the single line the agent parses against its schema. Built only from validated values.
hp_result() { printf 'RESULT %s\n' "$1"; }

# hp_require_account <name>: sets ACCOUNT, HOME_L (the logical home) and HOME_DIR (the file path of it).
hp_require_account() {
  [[ "$1" =~ $ACCOUNT_RE ]] || hp_die "$EXIT_BAD_ARGS" "bad account"
  ACCOUNT="$1"
  HOME_L="/home/$ACCOUNT"
  HOME_DIR="$ROOT$HOME_L"
  [ -d "$HOME_DIR" ] && [ ! -L "$HOME_DIR" ] || hp_die "$EXIT_NOT_OWNED" "no home for $ACCOUNT"
}

# hp_mysql <sql...>: runs one statement as the MySQL administrator (root's client file, the one CWP itself uses).
# The statement is passed on stdin by callers that carry a secret, so it never shows in the process list.
hp_mysql() {
  HOME=/root "$BIN/mysql" --defaults-file="$ROOT/root/.my.cnf" -N -B "$@"
}

# ----------------------------------------------------------------------------- registry
# One root-owned file per thing the agent created: $STATE/<account>/<kind>.<name> holding key=value lines. A remove
# only touches what has an entry (or carries our marker), never what an administrator or the panel made.

# hp_reg_path <kind> <name>
hp_reg_path() { printf '%s/%s/%s.%s' "$STATE" "$ACCOUNT" "$1" "$2"; }

# hp_reg_has <kind> <name>
hp_reg_has() { [ -f "$(hp_reg_path "$1" "$2")" ]; }

# hp_reg_get <kind> <name> <key>: the value, empty when the entry or the key is missing.
hp_reg_get() {
  local file; file="$(hp_reg_path "$1" "$2")"
  [ -f "$file" ] || return 0
  sed -n "s/^$3=//p" "$file" | head -n 1
}

# hp_reg_count <kind>: how many entries of that kind the account has.
hp_reg_count() {
  local dir="$STATE/$ACCOUNT"
  [ -d "$dir" ] || { echo 0; return 0; }
  find "$dir" -maxdepth 1 -type f -name "$1.*" | wc -l | tr -d ' '
}

# hp_reg_write <kind> <name> <key=value>...: replaces the entry atomically; the directory is root-only.
hp_reg_write() {
  local kind="$1" name="$2"; shift 2
  local dir="$STATE/$ACCOUNT" file tmp
  (umask 077; mkdir -p "$dir")
  chmod 700 "$STATE" "$dir"
  file="$(hp_reg_path "$kind" "$name")"
  tmp="$file.new"
  (umask 077; printf '%s\n' "$@" > "$tmp")
  mv -f "$tmp" "$file"
}

# hp_reg_remove <kind> <name>
hp_reg_remove() { rm -f -- "$(hp_reg_path "$1" "$2")"; }

# ----------------------------------------------------------------------------- backup and restore
# Every file the helper edits is copied first; a file it creates is noted. If anything fails before the configuration
# tests pass, the EXIT trap puts everything back, so a half-applied change never stays on a server.

# hp_guard: starts the work directory and arms the restore-on-failure trap.
hp_guard() {
  WORK="$(mktemp -d)"
  trap hp_on_exit EXIT
}

hp_on_exit() {
  local code=$?
  if [ "$DIRTY" = 1 ] && [ "$code" -ne 0 ]; then
    hp_restore || true
  fi
  [ -z "$WORK" ] || rm -rf -- "$WORK"
  exit "$code"
}

# hp_stage: creates the backup directory on first use.
hp_stage() {
  if [ -z "$BK" ]; then
    BK="$STATE/backups/$ACCOUNT-$(date +%Y%m%d%H%M%S)-$$"
    (umask 077; mkdir -p "$BK/files")
    chmod 700 "$STATE" "$STATE/backups"
  fi
}

# hp_backup <file>: remembers the file's content (or that it did not exist) once per run.
hp_backup() {
  hp_stage
  DIRTY=1
  if grep -qsxF -- "$1" "$BK/seen"; then
    return 0
  fi
  printf '%s\n' "$1" >> "$BK/seen"
  if [ -e "$1" ]; then
    BK_N=$((BK_N + 1))
    cp -p -- "$1" "$BK/files/$BK_N"
    printf 'file\t%s\t%s\n' "$BK_N" "$1" >> "$BK/index"
  else
    printf 'new\t-\t%s\n' "$1" >> "$BK/index"
  fi
}

# hp_track_dir <dir>: a directory this run created, removed again (when empty) on failure.
hp_track_dir() {
  hp_stage
  DIRTY=1
  printf 'dir\t-\t%s\n' "$1" >> "$BK/index"
}

# hp_restore: undoes the run, newest first.
hp_restore() {
  [ -f "$BK/index" ] || return 0
  local kind arg path
  while IFS=$'\t' read -r kind arg path; do
    case "$kind" in
      file) cat -- "$BK/files/$arg" > "$path" ;;
      new) rm -f -- "$path" ;;
      dir) rmdir -- "$path" 2>/dev/null || true ;;
    esac
  done < <(tac "$BK/index")
  DIRTY=0
}

# hp_replace <new file> <target>: puts the new content in the target when it differs; returns 0 when it did.
hp_replace() {
  if [ -f "$2" ] && cmp -s "$1" "$2"; then
    return 1
  fi
  hp_backup "$2"
  cat -- "$1" > "$2" || hp_die 1 "cannot write $2"
}

# hp_remove_file <file>: removes a file this run is allowed to remove; returns 0 when it existed.
hp_remove_file() {
  [ -e "$1" ] || return 1
  hp_backup "$1"
  rm -f -- "$1"
}

# ----------------------------------------------------------------------------- Apache and php-fpm
# hp_strip_handler <file>: the file without our marked php block.
hp_strip_handler() {
  awk -v b="$PHP_BEGIN" -v e="$PHP_END" '
    { t = $0; gsub(/^[ \t]+|[ \t]+$/, "", t) }
    t == b { skip = 1; next }
    skip && t == e { skip = 0; next }
    !skip { print }' "$1"
}

# hp_insert_handler <socket>: stdin with our marked php block added before the first </VirtualHost>.
hp_insert_handler() {
  awk -v sock="$1" -v b="$PHP_BEGIN" -v e="$PHP_END" '
    !done && /^<\/VirtualHost>/ {
      print "\t" b
      print "\t<IfModule proxy_fcgi_module>"
      print "\t\t<FilesMatch \\.php$>"
      print "\t\t\tSetHandler \"proxy:unix:" sock "|fcgi://localhost\""
      print "\t\t</FilesMatch>"
      print "\t</IfModule>"
      print "\t" e
      done = 1
    }
    { print }'
}

# hp_apache_test / hp_fpm_test <nn>: the configuration tests every change must pass before a reload.
hp_apache_test() { "$APACHECTL" -t >/dev/null 2>&1; }
hp_fpm_test() { "$ROOT$FPM_PREFIX$1/usr/sbin/php-fpm" -t -y "$ROOT$FPM_PREFIX$1/usr/etc/php-fpm.conf" >/dev/null 2>&1; }

# hp_php_versions: the installed php-fpm versions as "N.N", one per line, sorted (/opt/alt/php-fpm83 is 8.3). The one
# place that knows which versions this server offers: the php-versions helper prints it and ensure-website accepts
# nothing else.
hp_php_versions() {
  local dir nn
  for dir in "$ROOT$FPM_PREFIX"[0-9][0-9]*; do
    [ -x "$dir/usr/sbin/php-fpm" ] || continue
    nn="${dir##*/php-fpm}"
    [[ "$nn" =~ ^[0-9]{2,3}$ ]] || continue
    printf '%s.%s\n' "${nn:0:1}" "${nn:1}"
  done | sort -t. -k1,1n -k2,2n
}

# hp_vhost_files <hostname>: the http and https vhost files that exist for the hostname.
hp_vhost_files() {
  local f
  for f in "$VHOSTS/$1.conf" "$VHOSTS/$1.ssl.conf"; do
    if [ -f "$f" ]; then
      echo "$f"
    fi
  done
}

# hp_vhost_account <file>: the account named by SuexecUserGroup in a vhost file, empty when absent.
hp_vhost_account() {
  sed -n 's/^[[:space:]]*SuexecUserGroup[[:space:]]\{1,\}\([^[:space:]]\{1,\}\)[[:space:]].*$/\1/p' "$1" | head -n 1
}

# hp_vhost_docroot <file>: the first DocumentRoot value, without a trailing slash.
hp_vhost_docroot() {
  sed -n 's/^[[:space:]]*DocumentRoot[[:space:]]\{1,\}"\{0,1\}\([^"[:space:]]\{1,\}\)"\{0,1\}[[:space:]]*$/\1/p' "$1" | head -n 1 | sed 's:/*$::'
}

# hp_is_managed <file>: true when the file starts with our marker, so the helper made it.
hp_is_managed() { [ "$(head -n 1 "$1")" = "$MARKER" ]; }

# ----------------------------------------------------------------------------- databases
MAX_DATABASES=10

# hp_db_count <sql>: runs a COUNT(*) query into HP_COUNT; a MySQL failure aborts instead of reading as "none".
# (A variable, not command substitution: an exit inside $(...) would only leave the subshell.)
HP_COUNT=0
hp_db_count() { HP_COUNT="$(hp_mysql -e "$1" </dev/null)" || hp_die 1 "mysql query failed"; }

# hp_db_exists <name>: true when a schema of that exact name exists (validated names only, so no quoting problem).
hp_db_exists() { hp_db_count "SELECT COUNT(*) FROM information_schema.SCHEMATA WHERE SCHEMA_NAME='$1'"; [ "$HP_COUNT" != 0 ]; }

# hp_db_user_exists <name>: true when a MySQL user of that name exists.
hp_db_user_exists() { hp_db_count "SELECT COUNT(*) FROM mysql.user WHERE User='$1'"; [ "$HP_COUNT" != 0 ]; }

# hp_db_drop <name>: drops the schema and both users (localhost and 127.0.0.1) the helper creates for it.
hp_db_drop() {
  hp_mysql <<SQL
DROP DATABASE IF EXISTS \`$1\`;
DROP USER IF EXISTS '$1'@'localhost';
DROP USER IF EXISTS '$1'@'127.0.0.1';
SQL
}
