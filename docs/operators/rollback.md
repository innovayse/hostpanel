---
title: Rollback
description: How to go back to a previous Hostpanel version after an upgrade.
---

# Rollback

How to go back to a previous Hostpanel release when an upgrade causes a problem.

<Callout type="warning">
If the new version added a database migration, restore the dump you took before the upgrade first — migrations do not run backwards. Running the old images against a migrated database can cause data corruption.
</Callout>

## Before you roll back

Check whether the release you are rolling back from added any database migrations.
If it did, you must restore your pre-upgrade dump as part of the rollback.

## Roll back with the installer

Every upgrade with the installer leaves a database backup named with the version it
belongs to (see [Upgrade](/operators/upgrade)). `--rollback` uses the newest one:

```bash
curl -fsSL   https://raw.githubusercontent.com/innovayse/hostpanel/main/install.sh   | bash -s -- --rollback
```

Add `--dir <path>` for an install that is not in `/opt/hostpanel`. It then:

1. Shows the backup it will use and the version it returns to, and asks for confirmation.
   Add `--yes` to confirm in advance in a script. Without a terminal and without `--yes` it
   stops and changes nothing.
2. Downloads the compose file and Caddyfile of that version and stops the application
   services, leaving the database running.
3. Saves the current database to `backups/pre-rollback-<date>-<time>-<version>.sql`, so
   the rollback itself can be undone. If that copy cannot be made, it stops before
   replacing anything.
4. Replaces the database with the backup, sets `IMAGE_TAG` to the recorded version, pulls
   those images and starts the stack.

<Callout type="danger">
A rollback permanently removes everything written after the backup was taken: orders, payments, tickets and any other data. The copy saved in step 3 is the only way back to it.
</Callout>

`--rollback` cannot be combined with `--version`, because the version is the one recorded
with the backup. A backup taken while `IMAGE_TAG` was `latest` is refused; restore it by
hand as described below. To use an older backup than the newest, remove the newer files
from `backups/` first, or follow the manual steps.

## Roll back by hand

**1. Stop the application containers** (leave the database running):

```bash
cd /opt/hostpanel
docker compose -f docker-compose.hub.yml stop \
  hostpanel-api hostpanel-client hostpanel-admin hostpanel-caddy
```

**2. If the new release added migrations, restore your pre-upgrade database dump before running the installer:**

<Callout type="danger">
Restoring the pre-upgrade dump permanently removes everything written after the upgrade: orders, payments, tickets and any other data. Confirm you have the right dump file before proceeding.
</Callout>

Replace `pre-upgrade-2024-01-15-103000.sql` with the dated file created in upgrade step 1.

```bash
cd /opt/hostpanel

# Safety dump of the current (migrated) state:
umask 077
docker compose -f docker-compose.hub.yml exec -T hostpanel-db \
  sh -c 'pg_dump -U "$POSTGRES_USER" "$POSTGRES_DB"' \
  > pre-rollback-$(date +%Y-%m-%d-%H%M%S).sql

# Verify the pre-upgrade dump file, then drop and restore:
if ! test -s pre-upgrade-2024-01-15-103000.sql; then
  echo "Dump file is empty or missing, aborting."
else
  docker compose -f docker-compose.hub.yml exec -T hostpanel-db \
    sh -c 'dropdb -U "$POSTGRES_USER" "$POSTGRES_DB" && \
           createdb -U "$POSTGRES_USER" "$POSTGRES_DB"'
  docker compose -f docker-compose.hub.yml exec -T hostpanel-db \
    sh -c 'psql -v ON_ERROR_STOP=1 --single-transaction \
           -U "$POSTGRES_USER" "$POSTGRES_DB"' < pre-upgrade-2024-01-15-103000.sql
fi
```

**3. Roll back the compose files, Caddyfile and `IMAGE_TAG`** using the installer:

```bash
curl -fsSL \
  https://raw.githubusercontent.com/innovayse/hostpanel/main/install.sh \
  | bash -s -- --version 1.3.2
```

This restores the compose file and Caddyfile to the old version, sets `IMAGE_TAG`,
pulls the old images and starts the stack. Replace `1.3.2` with the version you are
rolling back to — the value you noted in upgrade step 1.

**4.** Check that `GET /health` returns `200` and that you can sign in.

## After rolling back

Open a bug report describing what went wrong so the issue can be fixed in the
next release. Include the version you rolled back from and the error you saw in
the API log:

```bash
cd /opt/hostpanel && docker compose -f docker-compose.hub.yml logs hostpanel-api
```
