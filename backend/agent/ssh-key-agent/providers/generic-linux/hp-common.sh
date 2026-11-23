#!/usr/bin/env bash
# shellcheck disable=SC2034 # the variables below are read by the helpers that source this file
# hp-common.sh: shared by the generic-linux helpers (detect, capabilities, php-versions, ensure-website, remove-website).
# Sourced, never run. It holds what they all need: the one place that tells which web server and which PHP-FPM layout
# a plain Linux server has, input validation, the root-owned registry of what the agent created, the backup and
# restore of edited files, and the one RESULT line the agent parses.
#
# Test seam: HP_ROOT prefixes every path and tool below (empty on a real server). The agent never forwards it from a
# request; it only reaches a helper through the agent's own fixed environment, so a unit test can point the helper at a
# stand-in tree with fake tools.
#
# Paths written into web server and php-fpm configuration stay logical (no HP_ROOT); only the files the helper
# touches are prefixed.

ROOT="${HP_ROOT:-}"
BIN="$ROOT/usr/bin"
SBIN="$ROOT/usr/sbin"
STATE="$ROOT/var/lib/hostpanel-ssh-key-agent"
AGENT_CONF="$ROOT/etc/hostpanel-ssh-key-agent/agent.conf"
MARKER="# hostpanel-ssh-key-agent managed"
POOL_MARKER="; hostpanel-ssh-key-agent managed"
TAB=$'\t'

ACCOUNT_RE='^[a-z_][a-z0-9_-]{0,31}$'
HOSTNAME_RE='^([a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?\.)+[a-z]{2,63}$'
DOCROOT_RE='^[A-Za-z0-9_-][A-Za-z0-9._-]{0,63}(/[A-Za-z0-9_-][A-Za-z0-9._-]{0,63}){0,5}$'
PHP_RE='^[0-9]\.[0-9]{1,2}$'

# Exit codes the agent maps to its stable error codes.
EXIT_BAD_ARGS=2
EXIT_NOT_OWNED=3
EXIT_CONFLICT=4
EXIT_PHP_UNSUPPORTED=5

ACCOUNT=""
HOME_L=""
HOME_DIR=""
BK=""
BK_N=0
DIRTY=0
WORK=""

# Set by hp_detect_web.
WEB_UNIT=""
WEB_USER=""
VHOST_DIR=""
ENABLED_DIR=""
WEB_TEST=()

# Set by hp_php_select.
PHP_VER=""
FPM_BIN=""
POOL_DIR=""
FPM_SERVICE=""
PHP_CLI=""
RUN_DIR=""

# The php-fpm services whose pool a run removed; they are reloaded once the tests passed.
DROPPED_SERVICES=()

# hp_say <text>: a progress line for whoever runs the helper by hand; the agent ignores everything but RESULT.
hp_say() { echo "[hostpanel] $*"; }

# hp_die <exit code> <text>: refuses with a reason on stderr (the agent discards it, an administrator reads it).
hp_die() { local code="$1"; shift; echo "$*" >&2; exit "$code"; }

# hp_result <json>: the single line the agent parses against its schema. Built only from validated values.
hp_result() { printf 'RESULT %s\n' "$1"; }

# hp_home_root: the directory the account homes are under, from home_root in agent.conf (default /home), as a logical
# path without a trailing slash. Anything but a plain absolute path is refused.
hp_home_root() {
  local root
  root="$(hp_conf_value home_root)"
  root="${root:-/home}"
  root="${root%/}"
  [[ "$root" =~ ^/[A-Za-z0-9._/-]+$ ]] && [[ "$root" != *..* ]] || hp_die 1 "home_root must be a plain absolute path"
  echo "$root"
}

# hp_require_account <name>: sets ACCOUNT, HOME_L (the logical home, under home_root) and HOME_DIR (the file path of it).
hp_require_account() {
  [[ "$1" =~ $ACCOUNT_RE ]] || hp_die "$EXIT_BAD_ARGS" "bad account"
  ACCOUNT="$1"
  HOME_L="$(hp_home_root)" || exit 1
  HOME_L="$HOME_L/$ACCOUNT"
  HOME_DIR="$ROOT$HOME_L"
  [ -d "$HOME_DIR" ] && [ ! -L "$HOME_DIR" ] || hp_die "$EXIT_NOT_OWNED" "no home for $ACCOUNT"
}

# ----------------------------------------------------------------------------- web server detection
# hp_conf_value <key>: the value of a key in agent.conf, empty when absent.
hp_conf_value() {
  [ -f "$AGENT_CONF" ] || return 0
  sed -n "s/^[[:space:]]*$1[[:space:]]*=[[:space:]]*//p" "$AGENT_CONF" | head -n 1 | tr -d '\r' | sed 's/[[:space:]]*$//'
}

# hp_unit_active <unit>: true when systemd says the unit is running.
hp_unit_active() { "$BIN/systemctl" is-active --quiet "$1" 2>/dev/null; }

# hp_nginx_user: the user nginx workers run as, from its main configuration, else the distribution's default.
hp_nginx_user() {
  local user
  user="$(sed -n 's/^[[:space:]]*user[[:space:]]\{1,\}\([a-z_][a-z0-9_-]*\)[;[:space:]].*$/\1/p' "$ROOT/etc/nginx/nginx.conf" 2>/dev/null | head -n 1)"
  if [ -n "$user" ]; then
    echo "$user"
  elif [ -e "$ROOT/etc/debian_version" ]; then
    echo www-data
  else
    echo nginx
  fi
}

# hp_detect_web: the one place that knows the server's web server. Sets WEB_UNIT (apache2, httpd or nginx), WEB_USER,
# VHOST_DIR, ENABLED_DIR (Debian Apache only) and WEB_TEST (the configuration test command). The running one wins; two
# running or none is refused unless agent.conf names it with `web_server = apache|nginx`. Refuses (exit 1) when a tool
# the layout needs is missing.
hp_detect_web() {
  local configured active=() unit
  configured="$(hp_conf_value web_server)"
  case "$configured" in
    apache) if [ -d "$ROOT/etc/apache2" ]; then WEB_UNIT=apache2; else WEB_UNIT=httpd; fi ;;
    nginx) WEB_UNIT=nginx ;;
    "")
      for unit in apache2 httpd nginx; do
        if hp_unit_active "$unit"; then
          active+=("$unit")
        fi
      done
      [ "${#active[@]}" -eq 1 ] || hp_die 1 "cannot tell the web server (running: ${active[*]:-none}); set web_server in agent.conf"
      WEB_UNIT="${active[0]}"
      ;;
    *) hp_die 1 "web_server must be apache or nginx" ;;
  esac
  case "$WEB_UNIT" in
    apache2)
      VHOST_DIR="$ROOT/etc/apache2/sites-available"
      ENABLED_DIR="$ROOT/etc/apache2/sites-enabled"
      WEB_TEST=("$SBIN/apache2ctl" -t)
      WEB_USER=www-data
      [ -x "$SBIN/a2ensite" ] && [ -x "$SBIN/a2dissite" ] && [ -x "$SBIN/a2enmod" ] || hp_die 1 "a2ensite, a2dissite or a2enmod missing"
      ;;
    httpd)
      VHOST_DIR="$ROOT/etc/httpd/conf.d"
      WEB_TEST=("$SBIN/httpd" -t)
      WEB_USER=apache
      ;;
    nginx)
      VHOST_DIR="$ROOT/etc/nginx/conf.d"
      WEB_TEST=("$SBIN/nginx" -t)
      WEB_USER="$(hp_nginx_user)"
      ;;
  esac
  [ -x "${WEB_TEST[0]}" ] && [ -x "$BIN/systemctl" ] || hp_die 1 "the web server test command or systemctl is missing"
  [ -d "$VHOST_DIR" ] || hp_die 1 "$WEB_UNIT configuration directory missing"
}

# hp_is_apache: true when the detected web server is Apache.
hp_is_apache() { [ "$WEB_UNIT" != nginx ]; }

# hp_web_test: the web server configuration test every change must pass before a reload.
hp_web_test() { "${WEB_TEST[@]}" >/dev/null 2>&1; }

# hp_vhost_file <hostname>: the file the helper writes the website's virtual host to.
hp_vhost_file() { printf '%s/hp-%s.conf' "$VHOST_DIR" "$1"; }

# ----------------------------------------------------------------------------- PHP-FPM layouts
# Every layout prints one line per installed version: version, php-fpm binary, pool directory, systemd unit, CLI binary
# and socket directory (all logical paths), separated by tabs. Debian first, then Remi, then the distribution's own.

# hp_php_layouts: every PHP-FPM install found on the server, unsorted.
hp_php_layouts() {
  local file version dir nn
  # Debian and Ubuntu (sury): /usr/sbin/php-fpmN.N, /etc/php/N.N/fpm/pool.d, phpN.N-fpm, /usr/bin/phpN.N, /run/php.
  for file in "$SBIN"/php-fpm[0-9].[0-9]*; do
    [ -x "$file" ] || continue
    version="${file##*/php-fpm}"
    [[ "$version" =~ $PHP_RE ]] || continue
    [ -d "$ROOT/etc/php/$version/fpm/pool.d" ] && [ -x "$BIN/php$version" ] || continue
    printf '%s\t/usr/sbin/php-fpm%s\t/etc/php/%s/fpm/pool.d\tphp%s-fpm\t/usr/bin/php%s\t/run/php\n' \
      "$version" "$version" "$version" "$version" "$version"
  done
  # RHEL with Remi: /opt/remi/phpNN/root/..., /etc/opt/remi/phpNN/php-fpm.d, phpNN-php-fpm.
  for dir in "$ROOT"/opt/remi/php[0-9][0-9]*; do
    nn="${dir##*/php}"
    [[ "$nn" =~ ^[0-9]{2,3}$ ]] || continue
    [ -x "$dir/root/usr/sbin/php-fpm" ] && [ -x "$dir/root/usr/bin/php" ] && [ -d "$ROOT/etc/opt/remi/php$nn/php-fpm.d" ] || continue
    printf '%s.%s\t/opt/remi/php%s/root/usr/sbin/php-fpm\t/etc/opt/remi/php%s/php-fpm.d\tphp%s-php-fpm\t/opt/remi/php%s/root/usr/bin/php\t/var/opt/remi/php%s/run/php-fpm\n' \
      "${nn:0:1}" "${nn:1}" "$nn" "$nn" "$nn" "$nn" "$nn"
  done
  # RHEL AppStream: /usr/sbin/php-fpm, /etc/php-fpm.d, php-fpm, /usr/bin/php, /run/php-fpm; the version comes from the CLI.
  if [ -x "$SBIN/php-fpm" ] && [ -x "$BIN/php" ] && [ -d "$ROOT/etc/php-fpm.d" ]; then
    version="$("$BIN/php" -r 'echo PHP_MAJOR_VERSION . "." . PHP_MINOR_VERSION;' </dev/null 2>/dev/null || true)"
    if [[ "$version" =~ $PHP_RE ]]; then
      printf '%s\t/usr/sbin/php-fpm\t/etc/php-fpm.d\tphp-fpm\t/usr/bin/php\t/run/php-fpm\n' "$version"
    fi
  fi
}

# hp_php_table: the installed versions, oldest first, one line each (the first layout found for a version wins).
hp_php_table() {
  hp_php_layouts | sort -s -t "$TAB" -k1,1V | awk -F '\t' '!seen[$1]++'
}

# hp_php_versions: "N.N<TAB>/abs/cli" per installed version. The one place that knows which versions this server
# offers: the php-versions helper prints it and ensure-website accepts nothing else.
hp_php_versions() {
  hp_php_table | cut -f 1,5
}

# hp_php_select <N.N>: sets PHP_VER, FPM_BIN, POOL_DIR, FPM_SERVICE, PHP_CLI and RUN_DIR; false when not installed.
hp_php_select() {
  local line
  line="$(hp_php_table | awk -F '\t' -v v="$1" '$1 == v { print }')"
  [ -n "$line" ] || return 1
  IFS=$'\t' read -r PHP_VER FPM_BIN POOL_DIR FPM_SERVICE PHP_CLI RUN_DIR <<< "$line"
}

# hp_fpm_test: the php-fpm configuration test of the selected version.
hp_fpm_test() { "$ROOT$FPM_BIN" -t >/dev/null 2>&1; }

# hp_pool_file: the file the account's pool of the selected version is written to.
hp_pool_file() { printf '%s%s/hp-%s.conf' "$ROOT" "$POOL_DIR" "$ACCOUNT"; }

# ----------------------------------------------------------------------------- registry
# One root-owned file per thing the agent created: $STATE/<account>/<kind>.<name> holding key=value lines. A remove
# only touches what has an entry, never what an administrator made.

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

# hp_site_owner <hostname>: the account the hostname is registered to, empty when it is registered to nobody.
hp_site_owner() {
  local file
  for file in "$STATE"/*/website."$1"; do
    if [ -f "$file" ]; then
      basename "$(dirname "$file")"
      return 0
    fi
  done
}

# hp_sites_using <excluded hostname> <php N.N> [<link_root 0|1>]: how many other registered websites of the account use
# that PHP version (and, when given, have that link_root).
hp_sites_using() {
  local file name count=0
  for file in "$STATE/$ACCOUNT"/website.*; do
    [ -f "$file" ] || continue
    name="${file##*/website.}"
    [ "$name" != "$1" ] || continue
    [ "$(hp_reg_get website "$name" php)" = "$2" ] || continue
    if [ -n "${3:-}" ] && [ "$(hp_reg_get website "$name" link_root)" != "$3" ]; then
      continue
    fi
    count=$((count + 1))
  done
  echo "$count"
}

# ----------------------------------------------------------------------------- backup and restore
# Every file the helper edits is copied first; a file it creates is noted. If anything fails before the configuration
# tests pass, the EXIT trap puts everything back, so a half-applied change never stays on a server.

# hp_guard: starts the work directory and arms the restore-on-failure trap.
hp_guard() {
  WORK="$(mktemp -d)"
  trap hp_on_exit EXIT
}

# hp_on_exit: the EXIT trap; puts everything back when the run failed after it started editing.
hp_on_exit() {
  local code=$?
  if [ "$DIRTY" = 1 ] && [ "$code" -ne 0 ]; then
    hp_restore || true
  fi
  [ -z "$WORK" ] || rm -rf -- "$WORK"
  # The backup is only needed until the run ends; it stays when a restore left the edits half done (DIRTY still set).
  if [ -n "$BK" ] && [ "$DIRTY" = 0 ]; then
    rm -rf -- "$BK"
  fi
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

# hp_backup <path>: remembers a file's content, a symlink's target, or that nothing existed, once per run.
hp_backup() {
  hp_stage
  DIRTY=1
  if grep -qsxF -- "$1" "$BK/seen"; then
    return 0
  fi
  printf '%s\n' "$1" >> "$BK/seen"
  if [ -L "$1" ]; then
    printf 'link\t%s\t%s\n' "$(readlink -- "$1")" "$1" >> "$BK/index"
  elif [ -e "$1" ]; then
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
      link) ln -sfn -- "$arg" "$path" ;;
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

# hp_is_managed <file>: true when the file starts with one of our markers, so the helper made it.
hp_is_managed() {
  local first
  first="$(head -n 1 "$1")"
  [ "$first" = "$MARKER" ] || [ "$first" = "$POOL_MARKER" ]
}

# hp_drop_pool <php N.N> <excluded hostname>: removes the account's pool of that version when the agent wrote it and no
# other registered website of the account uses the version. Selects that version's layout, so callers select theirs
# again afterwards. The removed pool's service is added to DROPPED_SERVICES; a failing php-fpm test refuses (restored).
hp_drop_pool() {
  local pool
  [ -n "$1" ] || return 0
  hp_php_select "$1" || return 0
  pool="$(hp_pool_file)"
  [ "$(hp_sites_using "$2" "$1")" = 0 ] && [ -f "$pool" ] && hp_is_managed "$pool" || return 0
  hp_remove_file "$pool" || true
  hp_fpm_test || hp_die 1 "php-fpm $1 test failed, restored"
  DROPPED_SERVICES+=("$FPM_SERVICE")
  hp_say "pool of php $1 removed"
}
