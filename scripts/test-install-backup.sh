#!/usr/bin/env bash
# Tests the upgrade backup and the --rollback of install.sh without Docker or a network.
#
# install.sh runs as it would for an operator, but with a fake `docker` and a fake `curl` first on PATH.
# The fake docker answers the few compose calls the script makes, records every call, and takes its
# behaviour (a dump that fails, is empty or cut short, a stopped database, a restore that fails and so on)
# from FAKE_* environment variables. Nothing here touches a real stack: every path lives in one
# temporary directory that is removed at the end.
#
# With HOSTPANEL_TEST_POSTGRES=1 the script also runs the dump and the restore of install.sh against a
# real postgres container (needs a working docker; HOSTPANEL_TEST_POSTGRES_IMAGE picks the image).
#
# Usage: bash scripts/test-install-backup.sh [path to install.sh]
set -u

INSTALL_SH="${1:-$(cd "$(dirname "$0")/.." && pwd)/install.sh}"
WORK="$(mktemp -d)"
PGDIR=''
PG_STARTED=0
PG_PROJECT="hptest$$"

# Removes everything the run created: the postgres test stack (volumes included), then the work folder.
cleanup() {
  if [ "$PG_STARTED" = "1" ]; then
    ( cd "$PGDIR" && COMPOSE_PROJECT_NAME="$PG_PROJECT" docker compose -f docker-compose.hub.yml down -v >/dev/null 2>&1 )
  fi
  rm -rf "$WORK"
}
trap cleanup EXIT

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
if [ "$1" = "volume" ] && [ "$2" = "inspect" ]; then
  [ "${FAKE_VOLUME:-1}" = "1" ]
  exit $?
fi
if [ "$1" = "compose" ] && [ "$2" = "version" ]; then exit 0; fi
if [ "$1" = "compose" ] && [ "$2" = "-f" ]; then
  shift 3
  sub="$1"
  shift
  case "$sub" in
    ps)
      printf 'ps %s\n' "$*" >> "$FAKE_LOG"
      if [ "${FAKE_DB_RUNNING:-1}" = "1" ] || [ -f "$FAKE_LOG.dbup" ]; then printf 'abc123\n'; fi
      exit 0 ;;
    config)
      printf 'config %s\n' "$*" >> "$FAKE_LOG"
      if [ "${FAKE_CONFIG:-ok}" = "fail" ]; then exit 1; fi
      for s in ${FAKE_SERVICES:-hostpanel-db hostpanel-rabbitmq hostpanel-redis hostpanel-api hostpanel-client hostpanel-admin hostpanel-caddy}; do
        printf '%s\n' "$s"
      done
      exit 0 ;;
    exec)
      script="$*"
      case "$script" in
        *pg_dump*)
          printf 'pg_dump\n' >> "$FAKE_LOG"
          case "${FAKE_DUMP:-ok}" in
            ok) printf -- '-- fake dump of %s\nCREATE TABLE t (id int);\n--\n-- PostgreSQL database dump complete\n--\n\\unrestrict abc\n' "${FAKE_DB_STATE:-state}" ; exit 0 ;;
            notables) printf -- '-- fake empty database\n--\n-- PostgreSQL database dump complete\n--\n' ; exit 0 ;;
            truncated) printf -- '-- fake dump of %s\nCREATE TABLE t (id int);\nCOPY t (id) FROM stdin;\n1\n' "${FAKE_DB_STATE:-state}" ; exit 0 ;;
            empty) exit 0 ;;
            fail) printf 'pg_dump: error: connection failed\n' >&2; exit 1 ;;
          esac ;;
        *pg_isready*)
          printf 'ready\n' >> "$FAKE_LOG"
          [ "${FAKE_DB_READY:-1}" = "1" ]
          exit $? ;;
        *createdb*)
          printf 'restore\n' >> "$FAKE_LOG"
          cat > "$FAKE_RESTORED"
          case "${FAKE_RESTORE:-ok}" in
            fail) exit 1 ;;
            swap) exit 3 ;;
          esac
          exit 0 ;;
        *wget*) printf 'health\n' >> "$FAKE_LOG"; exit 0 ;;
      esac
      exit 1 ;;
    stop|pull|up|run|logs)
      printf '%s %s\n' "$sub" "$*" >> "$FAKE_LOG"
      case "$sub" in
        up)
          case "$*" in
            *--no-deps*) : > "$FAKE_LOG.dbup" ;;
            *) if [ "${FAKE_UP:-ok}" = "fail" ]; then exit 1; fi ;;
          esac ;;
        stop) if [ "${FAKE_STOP:-ok}" = "fail" ]; then exit 1; fi ;;
        pull) if [ "${FAKE_PULL:-ok}" = "fail" ]; then exit 1; fi ;;
      esac
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
printf 'curl\n' >> "$FAKE_LOG"
if [ "${FAKE_CURL:-ok}" = "fail" ]; then exit 22; fi
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
  unset FAKE_DUMP FAKE_DB_RUNNING FAKE_RESTORE FAKE_DB_STATE FAKE_VOLUME FAKE_DB_READY FAKE_STOP \
    FAKE_PULL FAKE_UP FAKE_CURL FAKE_SERVICES FAKE_CONFIG
}

# Runs install.sh against $DIR with the fakes. Output goes to $DIR.out, the exit code is returned.
run_installer() {
  PATH="$WORK/bin:$PATH" FAKE_LOG="$LOG" FAKE_CURL_LOG="$CURL_LOG" FAKE_RESTORED="$RESTORED" \
    HOSTPANEL_NONINTERACTIVE=1 HOSTPANEL_DIR="$DIR" HOSTPANEL_DB_WAIT_ATTEMPTS=3 HOSTPANEL_DB_WAIT_INTERVAL=0 \
    bash "$INSTALL_SH" "$@" < /dev/null > "$DIR.out" 2>&1
}

# Writes a finished dump of the given state to file $1 (what pg_dump leaves). $2 label
write_dump() {
  printf -- '-- dump of %s\nCREATE TABLE t (id int);\n--\n-- PostgreSQL database dump complete\n--\n' "$2" > "$1"
}

# Predicates used by the checks.
log_has()      { grep -q -- "$1" "$LOG"; }
log_lacks()    { ! grep -q -- "$1" "$LOG"; }
out_has()      { grep -q -- "$1" "$DIR.out"; }
out_lacks()    { ! grep -q -- "$1" "$DIR.out"; }
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
has_file()     { [ -f "$DIR/backups/$1" ]; }
lacks_file()   { [ ! -f "$DIR/backups/$1" ]; }
old_compose()  { grep -q 'old compose' "$DIR/docker-compose.hub.yml"; }

make_fakes

# ── Upgrade ───────────────────────────────────────────────────────────────────

new_install 0.3.1
mkdir -p "$DIR/backups"
printf 'half a dump\n' > "$DIR/backups/hostpanel-20260101-100000-0.2.0.sql.part"
run_installer --version 0.4.0
check "upgrade succeeds" test "$?" -eq 0
BACKUP="$(one_backup 0.3.1)"
check "upgrade writes one backup named with the previous version" test -n "$BACKUP"
check "the backup holds the dump" grep -q 'CREATE TABLE' "$BACKUP"
check "the backup file is mode 600" mode_is "$BACKUP" 600
check "the backups folder is mode 700" mode_is "$DIR/backups" 700
check "no partial dump is left behind, a stale one is cleaned" test -z "$(ls "$DIR/backups" | grep '\.part$' || true)"
check "the dump is taken before the images are pulled" before pg_dump '^pull'
check "IMAGE_TAG moves to the new version" env_is IMAGE_TAG 0.4.0
check "the summary names the backup and the rollback command" out_has -- '--rollback'

new_install 0.3.1
FAKE_DUMP=fail run_installer --version 0.4.0
check "a failing dump stops the upgrade" test "$?" -ne 0
check "a failing dump: nothing is pulled or started" log_lacks '^pull'
check "a failing dump: nothing is downloaded" no_curl
check "a failing dump: IMAGE_TAG is unchanged" env_is IMAGE_TAG 0.3.1
check "a failing dump: the compose file is unchanged" old_compose
check "a failing dump leaves no backup file" test "$(ls "$DIR/backups" | wc -l | tr -d ' ')" -eq 0

new_install 0.3.1
FAKE_DUMP=empty run_installer --version 0.4.0
check "an empty dump stops the upgrade" test "$?" -ne 0
check "an empty dump: nothing is pulled" log_lacks '^pull'
check "an empty dump: IMAGE_TAG is unchanged" env_is IMAGE_TAG 0.3.1
check "an empty dump leaves no backup file" test "$(ls "$DIR/backups" | wc -l | tr -d ' ')" -eq 0

new_install 0.3.1
FAKE_DUMP=truncated run_installer --version 0.4.0
check "a dump without the completion line stops the upgrade" test "$?" -ne 0
check "a truncated dump: nothing is downloaded" no_curl
check "a truncated dump leaves no backup file" test "$(ls "$DIR/backups" | wc -l | tr -d ' ')" -eq 0

new_install 0.3.1
mkdir -p "$DIR/backups"
write_dump "$DIR/backups/hostpanel-20260101-100000-0.3.1.sql" older
FAKE_DUMP=notables run_installer --version 0.4.0 --keep 1
check "a database without tables does not stop the upgrade" test "$?" -eq 0
check "a database without tables is not kept as a backup" test "$(backups_count hostpanel)" -eq 1
check "a database without tables does not push out the older backup" has_file hostpanel-20260101-100000-0.3.1.sql

new_install 0.3.1
printf 'IMAGE_TAG="0.3.1"\r\nPOSTGRES_USER=u\r\nAPI_PORT=8080\r\nEMAIL_TO=a@example.com\r\n' > "$DIR/.env"
run_installer --version 0.4.0
check "quotes and a CR around IMAGE_TAG are ignored" test "$?" -eq 0
check "the backup is named with the clean tag" test -n "$(one_backup 0.3.1)"

new_install 0.3.1
printf 'IMAGE_TAG=0.3.1/../x\n' > "$DIR/.env"
run_installer --version 0.4.0
check "an IMAGE_TAG that is not a tag is refused" test "$?" -ne 0
check "that refusal changes nothing" no_curl

# A database container that is stopped but whose volume exists: only the db is started, awaited, dumped.
new_install 0.3.1
FAKE_DB_RUNNING=0 run_installer --version 0.4.0
check "a stopped database with its volume is backed up" test "$?" -eq 0
check "the stopped database is started on its own" log_has '^up -d --no-deps hostpanel-db'
check "the database is awaited before it is dumped" before '^ready' pg_dump
check "the database is started before it is awaited" before '^up -d --no-deps' '^ready'
check "that upgrade still leaves a backup" test -n "$(one_backup 0.3.1)"
check "that upgrade never suggests --skip-backup" out_lacks '--skip-backup'

new_install 0.3.1
FAKE_DB_RUNNING=0 FAKE_DB_READY=0 run_installer --version 0.4.0
check "a database that never gets ready stops the upgrade" test "$?" -ne 0
check "a database that never gets ready: nothing is downloaded" no_curl
check "a database that never gets ready: no --skip-backup advice" out_lacks '--skip-backup'
check "a database that never gets ready: the compose file is unchanged" old_compose

# A first install that stopped before the database ever started: no container, no volume, no data.
new_install 0.3.1
FAKE_DB_RUNNING=0 FAKE_VOLUME=0 run_installer --version 0.4.0
check "a rerun after a first install that never started the database succeeds" test "$?" -eq 0
check "that rerun takes no dump" log_lacks pg_dump
check "that rerun starts no database on its own" log_lacks '^up -d --no-deps'
check "that rerun needs no --skip-backup" out_lacks '--skip-backup'
check "that rerun goes on to pull" log_has '^pull'

new_install 0.3.1
FAKE_DB_RUNNING=0 run_installer --version 0.4.0 --skip-backup
check "--skip-backup upgrades without a dump" test "$?" -eq 0
check "--skip-backup takes no dump" log_lacks pg_dump

# Retention keeps the newest --keep, the oldest of each version and the file just made.
new_install 0.3.1
mkdir -p "$DIR/backups"
for n in 1 2 3 4 5 6; do
  write_dump "$DIR/backups/hostpanel-2026010${n}-100000-0.2.0.sql" "old $n"
done
run_installer --version 0.4.0 --keep 3
check "retention: the newest --keep and the oldest of the version remain" test "$(backups_count hostpanel)" -eq 4
check "retention: the oldest of its version is kept" has_file hostpanel-20260101-100000-0.2.0.sql
check "retention: the newest old backup is kept" has_file hostpanel-20260106-100000-0.2.0.sql
check "retention: a middle one is removed" lacks_file hostpanel-20260102-100000-0.2.0.sql
check "retention: the new backup is kept" test -n "$(one_backup 0.3.1)"

# A re-run on the bad version must not push the true pre-upgrade backup out.
new_install 0.4.0
mkdir -p "$DIR/backups"
write_dump "$DIR/backups/hostpanel-20260101-100000-0.3.1.sql" "pre-upgrade"
for n in 1 2 3; do
  write_dump "$DIR/backups/hostpanel-2026020${n}-100000-0.4.0.sql" "rerun $n"
done
run_installer --keep 2
check "retention: the oldest backup of the older version survives re-runs" has_file hostpanel-20260101-100000-0.3.1.sql
check "retention: the oldest backup of the current version survives" has_file hostpanel-20260201-100000-0.4.0.sql

# The file just made is never pruned, even when older-looking names sort after it.
new_install 0.3.1
mkdir -p "$DIR/backups"
write_dump "$DIR/backups/hostpanel-20200101-100000-0.3.1.sql" "long ago"
for n in 1 2; do
  write_dump "$DIR/backups/hostpanel-2999010${n}-100000-0.3.1.sql" "future $n"
done
run_installer --version 0.4.0 --keep 1
check "the backup just made survives a clock that ran ahead" test -n "$(ls "$DIR/backups"/hostpanel-*-0.3.1.sql | grep -v '2020\|2999' || true)"

# Many backups at once: the prune does not depend on a pipe that can be cut short.
new_install 0.3.1
mkdir -p "$DIR/backups"
for n in $(seq 10 69); do
  write_dump "$DIR/backups/hostpanel-202601${n}-100000-0.2.0.sql" "bulk $n"
done
run_installer --version 0.4.0 --keep 3
check "pruning 60 backups succeeds" test "$?" -eq 0
check "pruning 60 backups leaves the newest 3 and the oldest" test "$(backups_count hostpanel)" -eq 4

# Names are in UTC: a clock 14 hours ahead must not show in the file name.
new_install 0.3.1
TZ=Pacific/Kiritimati run_installer --version 0.4.0
check "the backup name carries the UTC time" test -n "$(ls "$DIR/backups"/hostpanel-"$(date -u +%Y%m%d-%H)"*-0.3.1.sql 2>/dev/null | head -n 1)"

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
write_dump "$DIR/backups/hostpanel-20260101-100000-0.2.0.sql" "older dump"
write_dump "$DIR/backups/hostpanel-20260201-100000-0.3.1.sql" "dump of 0.3.1"
run_installer --rollback --yes
check "rollback with --yes succeeds" test "$?" -eq 0
check "rollback restores the newest backup of another version" grep -q 'dump of 0.3.1' "$RESTORED"
check "rollback sets IMAGE_TAG to the recorded version" env_is IMAGE_TAG 0.3.1
check "rollback downloads the files of the recorded release" grep -q '/v0.3.1/' "$CURL_LOG"
check "rollback saves the current database first" test -n "$(ls "$DIR/backups"/pre-rollback-*-0.4.0.sql 2>/dev/null | head -n 1)"
check "the safety copy is mode 600" mode_is "$(ls "$DIR/backups"/pre-rollback-*-0.4.0.sql | head -n 1)" 600
check "rollback stops the application first" before '^stop' pg_dump
check "rollback saves before it restores" before pg_dump restore
check "rollback restores before it downloads the old files" before restore '^curl'
check "rollback downloads before it pulls" before '^curl' '^pull'
check "rollback restores before it starts" before restore '^up'
check "rollback replaced the compose file only after the restore" grep -q 'stand-in' "$DIR/docker-compose.hub.yml"

# The services to stop come from the compose file, and the database is not among them.
new_install 0.4.0
mkdir -p "$DIR/backups"
write_dump "$DIR/backups/hostpanel-20260201-100000-0.3.1.sql" "dump of 0.3.1"
FAKE_SERVICES="hostpanel-db hostpanel-api hostpanel-extra" run_installer --rollback --yes
check "rollback stops every service the compose file lists" log_has '^stop hostpanel-api hostpanel-extra$'
check "rollback never stops the database" log_lacks '^stop.*hostpanel-db'

new_install 0.4.0
mkdir -p "$DIR/backups"
write_dump "$DIR/backups/hostpanel-20260201-100000-0.3.1.sql" "dump of 0.3.1"
FAKE_CONFIG=fail run_installer --rollback --yes
check "rollback without a service list stops before any change" test "$?" -ne 0
check "rollback without a service list stops nothing" log_lacks '^stop'
check "rollback without a service list restores nothing" log_lacks restore

# A re-run of the installer on the bad version made newer backups of that version: skip them.
new_install 0.4.0
mkdir -p "$DIR/backups"
write_dump "$DIR/backups/hostpanel-20260101-100000-0.2.0.sql" "dump of 0.2.0"
write_dump "$DIR/backups/hostpanel-20260201-100000-0.3.1.sql" "dump of 0.3.1"
write_dump "$DIR/backups/hostpanel-20260301-100000-0.4.0.sql" "rerun on 0.4.0"
write_dump "$DIR/backups/hostpanel-20260302-100000-0.4.0.sql" "second rerun on 0.4.0"
run_installer --rollback --yes
check "rollback skips newer backups of the version in use" grep -q 'dump of 0.3.1' "$RESTORED"
check "rollback then goes to the version of that backup" env_is IMAGE_TAG 0.3.1

new_install 0.4.0
printf 'IMAGE_TAG="0.4.0"\r\nAPI_PORT=8080\r\n' > "$DIR/.env"
mkdir -p "$DIR/backups"
write_dump "$DIR/backups/hostpanel-20260201-100000-0.3.1.sql" "dump of 0.3.1"
write_dump "$DIR/backups/hostpanel-20260301-100000-0.4.0.sql" "rerun on 0.4.0"
run_installer --rollback --yes
check "a quoted IMAGE_TAG with a CR is still recognised as the version in use" grep -q 'dump of 0.3.1' "$RESTORED"

new_install 0.4.0
mkdir -p "$DIR/backups"
write_dump "$DIR/backups/hostpanel-20260301-100000-0.4.0.sql" "rerun on 0.4.0"
run_installer --rollback --yes
check "rollback with backups of the current version only fails" test "$?" -ne 0
check "that rollback changes nothing" log_lacks '^stop'
check "that rollback says why" out_has 'version now installed'

new_install 0.4.0
printf 'IMAGE_TAG=0.4.0 bad\n' > "$DIR/.env"
mkdir -p "$DIR/backups"
write_dump "$DIR/backups/hostpanel-20260201-100000-0.3.1.sql" "dump of 0.3.1"
run_installer --rollback --yes
check "rollback with an unusable current IMAGE_TAG is refused" test "$?" -ne 0
check "that refusal stops nothing" log_lacks '^stop'

new_install 0.4.0
mkdir -p "$DIR/backups"
write_dump "$DIR/backups/hostpanel-20260201-100000-0.3.1.sql" "dump of 0.3.1"
printf '%s\n' '-- a dump' 'CREATE TABLE t (id int);' > "$DIR/backups/hostpanel-20260301-100000-0.2.5.sql"
printf -- '-- cut\n' > "$DIR/backups/hostpanel-20260401-100000-0.3.0.sql"
run_installer --rollback --yes
check "a backup without the completion line is refused" test "$?" -ne 0
check "that refusal restores nothing" log_lacks restore
check "that refusal stops nothing" log_lacks '^stop'

new_install 0.4.0
mkdir -p "$DIR/backups"
write_dump "$DIR/backups/hostpanel-20260201-100000-0.3.1.sql" "dump of 0.3.1"
run_installer --rollback
check "rollback without --yes and without a terminal refuses" test "$?" -ne 0
check "a refused rollback changes nothing" log_lacks restore
check "a refused rollback keeps IMAGE_TAG" env_is IMAGE_TAG 0.4.0
check "a refused rollback stops no service" log_lacks '^stop'

new_install 0.4.0
mkdir -p "$DIR/backups"
write_dump "$DIR/backups/hostpanel-20260201-100000-0.3.1.sql" "dump of 0.3.1"
FAKE_DUMP=fail run_installer --rollback --yes
check "a rollback that cannot save the current database stops" test "$?" -ne 0
check "that rollback restores nothing" log_lacks restore
check "that rollback keeps IMAGE_TAG" env_is IMAGE_TAG 0.4.0
check "that rollback downloads nothing, so the current compose file stays" no_curl
check "that rollback keeps the current compose file" old_compose
check "that rollback says the services are stopped" out_has 'STOPPED'

new_install 0.4.0
mkdir -p "$DIR/backups"
write_dump "$DIR/backups/hostpanel-20260201-100000-0.3.1.sql" "dump of 0.3.1"
FAKE_DUMP=truncated run_installer --rollback --yes
check "a truncated safety copy stops the rollback" test "$?" -ne 0
check "a truncated safety copy: nothing is restored" log_lacks restore
check "a truncated safety copy: no file is left" test "$(backups_count pre-rollback)" -eq 0

new_install 0.4.0
mkdir -p "$DIR/backups"
write_dump "$DIR/backups/hostpanel-20260201-100000-0.3.1.sql" "dump of 0.3.1"
FAKE_STOP=fail run_installer --rollback --yes
check "a rollback that cannot stop the services stops" test "$?" -ne 0
check "that rollback says what to run" out_has 'up -d'
check "that rollback takes no dump" log_lacks pg_dump

new_install 0.4.0
run_installer --rollback --yes
check "rollback with no backup fails" test "$?" -ne 0
check "rollback with no backup touches nothing" log_lacks '^stop'

new_install 0.4.0
mkdir -p "$DIR/backups"
write_dump "$DIR/backups/hostpanel-20260201-100000-latest.sql" "moving"
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
write_dump "$DIR/backups/hostpanel-20260201-100000-0.3.1.sql" "dump of 0.3.1"
run_installer --rollback --yes --version 0.2.0
check "--rollback with --version is refused" test "$?" -ne 0

# The database container is stopped but its volume exists: the rollback starts it alone.
new_install 0.4.0
mkdir -p "$DIR/backups"
write_dump "$DIR/backups/hostpanel-20260201-100000-0.3.1.sql" "dump of 0.3.1"
FAKE_DB_RUNNING=0 run_installer --rollback --yes
check "rollback with a stopped database succeeds" test "$?" -eq 0
check "rollback starts the database on its own first" before '^up -d --no-deps' '^stop'
check "rollback awaits the database before the dump" before '^ready' pg_dump

# A failed restore: the services are stopped, the live database stays as it was, nothing is pruned.
new_install 0.4.0
mkdir -p "$DIR/backups"
write_dump "$DIR/backups/hostpanel-20260201-100000-0.3.1.sql" "dump of 0.3.1"
for n in 1 2 3; do
  write_dump "$DIR/backups/pre-rollback-2026010${n}-100000-0.4.0.sql" "real data $n"
done
FAKE_RESTORE=fail run_installer --rollback --yes --keep 1
check "a failed restore fails the rollback" test "$?" -ne 0
check "a failed restore: the output says the services are stopped" out_has 'STOPPED'
check "a failed restore: the output says the live database was not changed" out_has 'not changed'
check "a failed restore: the output names the safety copy" out_has 'pre-rollback-.*-0.4.0.sql'
check "a failed restore: the output prints the restore command" out_has 'dropdb -U'
check "a failed restore: the output prints the start command" out_has 'docker compose -f docker-compose.hub.yml up -d'
check "a failed restore: IMAGE_TAG is unchanged" env_is IMAGE_TAG 0.4.0
check "a failed restore: the compose file is unchanged" old_compose
check "a failed restore: nothing is downloaded" no_curl
check "a failed restore: nothing is started" log_lacks '^up'
check "a failed restore: the safety copy exists" test "$(backups_count pre-rollback)" -eq 4
check "a failed restore prunes no earlier safety copy" has_file pre-rollback-20260102-100000-0.4.0.sql

# The same rollback again, now with a working restore: the earlier safety copies are still there to the end.
FAKE_RESTORE=ok run_installer --rollback --yes --keep 1
check "the rollback repeated after a failed restore succeeds" test "$?" -eq 0
check "after the success the oldest safety copy is still there" has_file pre-rollback-20260101-100000-0.4.0.sql
check "after the success a middle safety copy is pruned" lacks_file pre-rollback-20260102-100000-0.4.0.sql

new_install 0.4.0
mkdir -p "$DIR/backups"
write_dump "$DIR/backups/hostpanel-20260201-100000-0.3.1.sql" "dump of 0.3.1"
FAKE_RESTORE=swap run_installer --rollback --yes
check "a restore that breaks half way fails the rollback" test "$?" -ne 0
check "that failure says the database is missing" out_has 'MISSING'
check "that failure prints the restore command for the safety copy" out_has 'pre-rollback-.*sql'

# A database with no tables is not worth a safety copy and must not push a real one out.
new_install 0.4.0
mkdir -p "$DIR/backups"
write_dump "$DIR/backups/hostpanel-20260201-100000-0.3.1.sql" "dump of 0.3.1"
for n in 1 2; do
  write_dump "$DIR/backups/pre-rollback-2026010${n}-100000-0.4.0.sql" "real data $n"
done
FAKE_DUMP=notables run_installer --rollback --yes --keep 1
check "a rollback over a database without tables succeeds" test "$?" -eq 0
check "no safety copy is kept for a database without tables" test "$(backups_count pre-rollback)" -eq 2
check "the earlier safety copies stay" has_file pre-rollback-20260102-100000-0.4.0.sql

# After the restore: a failure to download, pull or start says in what state things are and what to run.
new_install 0.4.0
mkdir -p "$DIR/backups"
write_dump "$DIR/backups/hostpanel-20260201-100000-0.3.1.sql" "dump of 0.3.1"
FAKE_CURL=fail run_installer --rollback --yes
check "a failed download after the restore fails the rollback" test "$?" -ne 0
check "that failure says the database is restored" out_has 'database is restored'
check "that failure says what to run" out_has 'docker compose -f docker-compose.hub.yml up -d'
check "that failure keeps IMAGE_TAG" env_is IMAGE_TAG 0.4.0

new_install 0.4.0
mkdir -p "$DIR/backups"
write_dump "$DIR/backups/hostpanel-20260201-100000-0.3.1.sql" "dump of 0.3.1"
FAKE_PULL=fail run_installer --rollback --yes
check "a failed pull after the restore fails the rollback" test "$?" -ne 0
check "that failure says the database is restored" out_has 'database is restored'
check "that failure prints the pull and up commands" out_has 'pull && docker compose -f docker-compose.hub.yml up -d'
check "that failure starts nothing" log_lacks '^up'

new_install 0.4.0
mkdir -p "$DIR/backups"
write_dump "$DIR/backups/hostpanel-20260201-100000-0.3.1.sql" "dump of 0.3.1"
FAKE_UP=fail run_installer --rollback --yes
check "a failed start after the restore fails the rollback" test "$?" -ne 0
check "that failure says the database is restored" out_has 'database is restored'
check "that failure prints the start command" out_has 'up -d'

# ── Real postgres ─────────────────────────────────────────────────────────────

# Runs one function of install.sh against the postgres test stack. $@ function and arguments
pg_call() {
  HOSTPANEL_SOURCE_ONLY=1 INSTALL_DIR="$PGDIR" KEEP=5 COMPOSE_PROJECT_NAME="$PG_PROJECT" \
    HOSTPANEL_DB_WAIT_ATTEMPTS=60 HOSTPANEL_DB_WAIT_INTERVAL=1 \
    bash -c 'source "$0"; set +e +o pipefail; "$@"' "$INSTALL_SH" "$@" > "$PGDIR/call.out" 2>&1
}

# Runs one SQL statement in the test database and prints the result. $1 statement
pg_sql() {
  ( cd "$PGDIR" && COMPOSE_PROJECT_NAME="$PG_PROJECT" docker compose -f docker-compose.hub.yml exec -T hostpanel-db \
      psql -U hostpanel -d hostpanel -tA -c "$1" 2>/dev/null )
}

# Runs one statement in the test server's maintenance database. $1 statement
pg_sql_admin() {
  ( cd "$PGDIR" && COMPOSE_PROJECT_NAME="$PG_PROJECT" docker compose -f docker-compose.hub.yml exec -T hostpanel-db \
      psql -U hostpanel -d postgres -tA -c "$1" 2>/dev/null )
}

# Dump, restore and a corrupt dump against a real postgres container.
run_postgres_tests() {
  local image="${HOSTPANEL_TEST_POSTGRES_IMAGE:-postgres:16-alpine}"
  PGDIR="$WORK/pg"
  mkdir -p "$PGDIR"
  cat > "$PGDIR/docker-compose.hub.yml" <<COMPOSE
services:
  hostpanel-db:
    image: ${image}
    environment:
      POSTGRES_USER: hostpanel
      POSTGRES_PASSWORD: test
      POSTGRES_DB: hostpanel
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -h 127.0.0.1 -U hostpanel -d hostpanel"]
      interval: 2s
      timeout: 3s
      retries: 30
COMPOSE
  PG_STARTED=1
  if ! ( cd "$PGDIR" && COMPOSE_PROJECT_NAME="$PG_PROJECT" docker compose -f docker-compose.hub.yml up -d --wait >/dev/null 2>&1 ); then
    check "the postgres test stack starts" false
    return
  fi
  check "the postgres test stack starts" true
  check "pg_isready is awaited on the real database" pg_call wait_for_database
  pg_sql "CREATE TABLE items (id int); INSERT INTO items VALUES (1), (2), (3);" >/dev/null

  check "a real dump is taken" pg_call dump_database "$PGDIR/good.sql"
  check "a real dump ends with the completion line" pg_call dump_is_complete "$PGDIR/good.sql"
  check "a real dump creates the table" pg_call dump_has_tables "$PGDIR/good.sql"
  check "a real dump is mode 600" mode_is "$PGDIR/good.sql" 600
  head -n -8 "$PGDIR/good.sql" > "$PGDIR/cut.sql"
  check "a real dump cut short is not a finished dump" test "$(pg_call dump_is_complete "$PGDIR/cut.sql"; echo $?)" -ne 0

  pg_sql "INSERT INTO items VALUES (4);" >/dev/null
  check "a real restore succeeds" pg_call restore_dump "$PGDIR/good.sql"
  check "a real restore brings back the dumped data" test "$(pg_sql 'SELECT count(*) FROM items')" = "3"
  check "a real restore leaves no temporary database" test "$(pg_sql_admin "SELECT count(*) FROM pg_database WHERE datname IN ('hostpanel_restore', 'hostpanel_replaced')")" = "0"

  pg_sql "INSERT INTO items VALUES (5);" >/dev/null
  sed '0,/^CREATE TABLE/s//THIS IS NOT SQL;\nCREATE TABLE/' "$PGDIR/good.sql" > "$PGDIR/bad.sql"
  check "a corrupt dump fails the restore" test "$(pg_call restore_dump "$PGDIR/bad.sql"; echo $?)" = "1"
  check "a corrupt dump leaves the live data as it was" test "$(pg_sql 'SELECT count(*) FROM items')" = "4"
  check "a corrupt dump leaves no temporary database" test "$(pg_sql_admin "SELECT count(*) FROM pg_database WHERE datname IN ('hostpanel_restore', 'hostpanel_replaced')")" = "0"
}

if [ "${HOSTPANEL_TEST_POSTGRES:-0}" = "1" ]; then
  run_postgres_tests
else
  printf 'skip  real postgres tests (set HOSTPANEL_TEST_POSTGRES=1 to run them)\n'
fi

printf '\n%s checks, %s failed\n' "$CHECKS" "$FAILURES"
[ "$FAILURES" -eq 0 ]
