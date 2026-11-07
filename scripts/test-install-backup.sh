#!/usr/bin/env bash
# Tests the upgrade backup and the --rollback of install.sh without Docker or a network.
#
# install.sh runs as it would for an operator, but with a fake `docker` and a fake `curl` first on PATH.
# The fake docker answers the few compose calls the script makes, records every call, and takes its
# behaviour (a dump that fails, an empty dump, a stopped database) from environment variables. Nothing
# here touches a real stack: every path lives in one temporary directory that is removed at the end.
#
# Usage: bash scripts/test-install-backup.sh [path to install.sh]
set -u

INSTALL_SH="${1:-$(cd "$(dirname "$0")/.." && pwd)/install.sh}"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

FAILURES=0
CHECKS=0

# Records one check. $1 description, remaining arguments: the command that must succeed.
check() {
  local what="$1"
  shift
  CHECKS=$((CHECKS + 1))
  if "$@"; then
    printf 'ok    %s\n' "$what"
  else
    printf 'FAIL  %s\n' "$what"
    FAILURES=$((FAILURES + 1))
  fi
}

# Writes the fake docker and curl into $WORK/bin.
make_fakes() {
  mkdir -p "$WORK/bin"
  cat > "$WORK/bin/docker" <<'FAKE'
#!/usr/bin/env bash
# Fake docker: logs the compose subcommand and answers like a healthy stack unless told otherwise.
if [ "$1" = "info" ]; then exit 0; fi
if [ "$1" = "compose" ] && [ "$2" = "version" ]; then exit 0; fi
if [ "$1" = "compose" ] && [ "$2" = "-f" ]; then
  shift 3
  sub="$1"
  shift
  case "$sub" in
    ps)
      printf 'ps %s\n' "$*" >> "$FAKE_LOG"
      if [ "${FAKE_DB_RUNNING:-1}" = "1" ]; then printf 'abc123\n'; fi
      exit 0 ;;
    exec)
      script="$*"
      case "$script" in
        *pg_dump*)
          printf 'pg_dump\n' >> "$FAKE_LOG"
          case "${FAKE_DUMP:-ok}" in
            ok) printf -- '-- fake dump of %s\nCREATE TABLE t (id int);\n' "${FAKE_DB_STATE:-state}" ; exit 0 ;;
            empty) exit 0 ;;
            fail) printf 'pg_dump: error: connection failed\n' >&2; exit 1 ;;
          esac ;;
        *psql*)
          printf 'restore\n' >> "$FAKE_LOG"
          cat > "$FAKE_RESTORED"
          if [ "${FAKE_RESTORE:-ok}" = "fail" ]; then exit 1; fi
          exit 0 ;;
        *wget*) printf 'health\n' >> "$FAKE_LOG"; exit 0 ;;
      esac
      exit 1 ;;
    stop|pull|up|run|logs)
      printf '%s %s\n' "$sub" "$*" >> "$FAKE_LOG"
      exit 0 ;;
  esac
fi
printf 'unexpected docker call: %s\n' "$*" >&2
exit 1
FAKE
  cat > "$WORK/bin/curl" <<'FAKE'
#!/usr/bin/env bash
# Fake curl: logs the URL and writes a stand-in file to the -o target.
url=''
out=''
while [ "$#" -gt 0 ]; do
  case "$1" in
    -o) out="$2"; shift 2 ;;
    -*) shift ;;
    *) url="$1"; shift ;;
  esac
done
printf '%s\n' "$url" >> "$FAKE_CURL_LOG"
printf '# stand-in for %s\n' "$url" > "$out"
FAKE
  chmod +x "$WORK/bin/docker" "$WORK/bin/curl"
}

# Starts a fresh scenario: an install directory with an old compose file and a .env on version $1.
# Sets DIR, LOG, CURL_LOG and RESTORED.
new_install() {
  DIR="$WORK/install-$CHECKS-$RANDOM"
  mkdir -p "$DIR"
  printf 'old compose\n' > "$DIR/docker-compose.hub.yml"
  printf 'IMAGE_TAG=%s\nPOSTGRES_USER=u\nAPI_PORT=8080\nEMAIL_TO=a@example.com\n' "$1" > "$DIR/.env"
  chmod 600 "$DIR/.env"
  LOG="$DIR.log"
  CURL_LOG="$DIR.curl"
  RESTORED="$DIR.restored"
  : > "$LOG"
  : > "$CURL_LOG"
  unset FAKE_DUMP FAKE_DB_RUNNING FAKE_RESTORE FAKE_DB_STATE
}

# Runs install.sh against $DIR with the fakes. Output goes to $DIR.out, the exit code is returned.
run_installer() {
  PATH="$WORK/bin:$PATH" FAKE_LOG="$LOG" FAKE_CURL_LOG="$CURL_LOG" FAKE_RESTORED="$RESTORED" \
    HOSTPANEL_NONINTERACTIVE=1 HOSTPANEL_DIR="$DIR" \
    bash "$INSTALL_SH" "$@" < /dev/null > "$DIR.out" 2>&1
}

# Predicates used by the checks.
log_has()      { grep -q -- "$1" "$LOG"; }
log_lacks()    { ! grep -q -- "$1" "$LOG"; }
out_has()      { grep -q -- "$1" "$DIR.out"; }
env_is()       { [ "$(grep -E "^$1=" "$DIR/.env" | tail -n 1)" = "$1=$2" ]; }
no_curl()      { [ ! -s "$CURL_LOG" ]; }
backups_count() { ls "$DIR/backups" 2>/dev/null | grep -c "^$1-.*\.sql$" || true; }
# True when line $1 of the call log comes before line $2 (both matched by pattern, first hit).
before() {
  local a b
  a="$(grep -n -- "$1" "$LOG" | head -n 1 | cut -d: -f1)"
  b="$(grep -n -- "$2" "$LOG" | head -n 1 | cut -d: -f1)"
  [ -n "$a" ] && [ -n "$b" ] && [ "$a" -lt "$b" ]
}
# True when the file mode is $2 (GNU stat; the CI runner is Linux).
mode_is()      { [ "$(stat -c '%a' "$1" 2>/dev/null)" = "$2" ]; }
one_backup()   { ls "$DIR/backups"/hostpanel-*-"$1".sql 2>/dev/null | head -n 1; }

make_fakes

# ── Upgrade ───────────────────────────────────────────────────────────────────

new_install 0.3.1
run_installer --version 0.4.0
check "upgrade succeeds" test "$?" -eq 0
BACKUP="$(one_backup 0.3.1)"
check "upgrade writes one backup named with the previous version" test -n "$BACKUP"
check "the backup holds the dump" grep -q 'CREATE TABLE' "$BACKUP"
check "the backup file is mode 600" mode_is "$BACKUP" 600
check "the backups folder is mode 700" mode_is "$DIR/backups" 700
check "no partial dump is left behind" test -z "$(ls "$DIR/backups" | grep '\.part$' || true)"
check "the dump is taken before the images are pulled" before pg_dump '^pull'
check "IMAGE_TAG moves to the new version" env_is IMAGE_TAG 0.4.0
check "the summary names the backup and the rollback command" out_has -- '--rollback'

new_install 0.3.1
FAKE_DUMP=fail run_installer --version 0.4.0
check "a failing dump stops the upgrade" test "$?" -ne 0
check "a failing dump: nothing is pulled or started" log_lacks '^pull'
check "a failing dump: nothing is downloaded" no_curl
check "a failing dump: IMAGE_TAG is unchanged" env_is IMAGE_TAG 0.3.1
check "a failing dump: the compose file is unchanged" grep -q 'old compose' "$DIR/docker-compose.hub.yml"
check "a failing dump leaves no backup file" test "$(ls "$DIR/backups" | wc -l | tr -d ' ')" -eq 0

new_install 0.3.1
FAKE_DUMP=empty run_installer --version 0.4.0
check "an empty dump stops the upgrade" test "$?" -ne 0
check "an empty dump: nothing is pulled" log_lacks '^pull'
check "an empty dump: IMAGE_TAG is unchanged" env_is IMAGE_TAG 0.3.1
check "an empty dump leaves no backup file" test "$(ls "$DIR/backups" | wc -l | tr -d ' ')" -eq 0

new_install 0.3.1
FAKE_DB_RUNNING=0 run_installer --version 0.4.0
check "a stopped database stops the upgrade" test "$?" -ne 0
check "a stopped database: nothing is downloaded" no_curl
check "a stopped database: the message names --skip-backup" out_has -- '--skip-backup'

new_install 0.3.1
FAKE_DB_RUNNING=0 run_installer --version 0.4.0 --skip-backup
check "--skip-backup upgrades without a dump" test "$?" -eq 0
check "--skip-backup takes no dump" log_lacks pg_dump

new_install 0.3.1
mkdir -p "$DIR/backups"
for n in 1 2 3 4 5 6; do
  printf 'old %s\n' "$n" > "$DIR/backups/hostpanel-2026010${n}-100000-0.2.${n}.sql"
done
run_installer --version 0.4.0 --keep 3
check "retention: only --keep backups remain" test "$(backups_count hostpanel)" -eq 3
check "retention: the newest old backup is kept" test -f "$DIR/backups/hostpanel-20260106-100000-0.2.6.sql"
check "retention: the oldest is removed" test ! -f "$DIR/backups/hostpanel-20260101-100000-0.2.1.sql"
check "retention: the new backup is kept" test -n "$(one_backup 0.3.1)"

new_install latest
run_installer --version 0.4.0
check "a backup on the moving tag still succeeds" test "$?" -eq 0
check "a backup on the moving tag warns about pinning" out_has 'moving tag'

new_install 0.3.1
run_installer --keep 0
check "--keep 0 is refused" test "$?" -ne 0

# ── Rollback ──────────────────────────────────────────────────────────────────

new_install 0.4.0
mkdir -p "$DIR/backups"
printf -- '-- older dump\n' > "$DIR/backups/hostpanel-20260101-100000-0.2.0.sql"
printf -- '-- dump of 0.3.1\nCREATE TABLE t (id int);\n' > "$DIR/backups/hostpanel-20260201-100000-0.3.1.sql"
run_installer --rollback --yes
check "rollback with --yes succeeds" test "$?" -eq 0
check "rollback restores the latest backup" grep -q 'dump of 0.3.1' "$RESTORED"
check "rollback sets IMAGE_TAG to the recorded version" env_is IMAGE_TAG 0.3.1
check "rollback downloads the files of the recorded release" grep -q '/v0.3.1/' "$CURL_LOG"
check "rollback saves the current database first" test -n "$(ls "$DIR/backups"/pre-rollback-*-0.4.0.sql 2>/dev/null | head -n 1)"
check "the safety copy is mode 600" mode_is "$(ls "$DIR/backups"/pre-rollback-*-0.4.0.sql | head -n 1)" 600
check "rollback stops the application first" before '^stop' pg_dump
check "rollback saves before it restores" before pg_dump restore
check "rollback restores before it starts" before restore '^up'

new_install 0.4.0
printf -- '-- dump\n' > /dev/null
mkdir -p "$DIR/backups"
printf -- '-- dump of 0.3.1\n' > "$DIR/backups/hostpanel-20260201-100000-0.3.1.sql"
run_installer --rollback
check "rollback without --yes and without a terminal refuses" test "$?" -ne 0
check "a refused rollback changes nothing" log_lacks restore
check "a refused rollback keeps IMAGE_TAG" env_is IMAGE_TAG 0.4.0
check "a refused rollback stops no service" log_lacks '^stop'

new_install 0.4.0
mkdir -p "$DIR/backups"
printf -- '-- dump of 0.3.1\n' > "$DIR/backups/hostpanel-20260201-100000-0.3.1.sql"
FAKE_DUMP=fail run_installer --rollback --yes
check "a rollback that cannot save the current database stops" test "$?" -ne 0
check "that rollback restores nothing" log_lacks restore
check "that rollback keeps IMAGE_TAG" env_is IMAGE_TAG 0.4.0

new_install 0.4.0
run_installer --rollback --yes
check "rollback with no backup fails" test "$?" -ne 0
check "rollback with no backup touches nothing" log_lacks '^stop'

new_install 0.4.0
mkdir -p "$DIR/backups"
printf -- '-- dump\n' > "$DIR/backups/hostpanel-20260201-100000-latest.sql"
run_installer --rollback --yes
check "a backup taken on the moving tag is not rolled back automatically" test "$?" -ne 0
check "that refusal restores nothing" log_lacks restore

new_install 0.4.0
mkdir -p "$DIR/backups"
: > "$DIR/backups/hostpanel-20260201-100000-0.3.1.sql"
run_installer --rollback --yes
check "an empty backup is refused" test "$?" -ne 0
check "an empty backup restores nothing" log_lacks restore

new_install 0.4.0
mkdir -p "$DIR/backups"
printf -- '-- dump\n' > "$DIR/backups/hostpanel-20260201-100000-0.3.1.sql"
run_installer --rollback --yes --version 0.2.0
check "--rollback with --version is refused" test "$?" -ne 0

printf '\n%s checks, %s failed\n' "$CHECKS" "$FAILURES"
[ "$FAILURES" -eq 0 ]
