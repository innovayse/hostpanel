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
belongs to (see [Upgrade](/operators/upgrade)). `--rollback` uses the newest backup
whose version differs from the `IMAGE_TAG` in `.env`. Running the installer again on a
bad version makes newer backups of that same version; they are skipped, so the
rollback still goes to the version you upgraded from. When the installer trims old
backups it keeps the oldest backup of each of the newest 10 versions
(`--keep-versions <n>`), so that one is not lost; the oldest backups of versions
further back go like any other old backup, so the folder does not grow with every
release.

A rollback that is repeated can pick a backup of a **newer** version: after you go
back from 0.4.0 to 0.3.1, the newest backups of another version are the ones of
0.4.0. The installer then says in bold that this moves **forward**, names both
versions and asks with `Move FORWARD to <version>?`. Answer no if you meant to go further
back, and move the newer files out of `backups/` first.

```bash
curl -fsSL   https://raw.githubusercontent.com/innovayse/hostpanel/main/install.sh   | bash -s -- --rollback
```

Add `--dir <path>` for an install that is not in `/opt/hostpanel`. It then:

1. Shows the backup it will use and the version it returns to, and asks for confirmation.
   Add `--yes` to confirm in advance in a script. Without a terminal and without `--yes` it
   stops and changes nothing. A backup that is empty or does not end with the
   `-- PostgreSQL database dump complete` line is refused.
2. Starts the database on its own if it is stopped, waits until it accepts connections,
   and stops every other service listed by `docker compose config --services`.
3. Saves the current database to `backups/pre-rollback-<date>-<time>-<version>.sql`
   (UTC), so the rollback itself can be undone. If that copy cannot be made, it stops
   before replacing anything. A database without tables is not worth a copy and none
   is kept.
4. Restores the backup into a temporary database and renames it into the place of the
   live database only when the whole restore went through. A restore that fails, for
   example because the dump is damaged, leaves the live database exactly as it was.
   A database cannot be renamed while any session holds it, and the server has
   background workers (autovacuum above all) that connect on their own. So each
   rename first ends the sessions that hold the database and is tried up to 10 times,
   a second apart. If it stays blocked, the live database is put back, the installer
   says the database stayed busy (and not that the backup may be damaged) and prints
   the command to run the rollback again, which is safe to repeat.
5. Only now downloads the compose file and Caddyfile of the old version, sets
   `IMAGE_TAG`, pulls those images and starts the stack. So a failure in steps 1 to 4
   leaves the current compose file in place.
6. Removes old `pre-rollback-*` files beyond `--keep`, the oldest one of each version
   excepted. This happens only after the restore succeeded, and never to the copy just
   made.

<Callout type="danger">
A rollback permanently removes everything written after the backup was taken: orders, payments, tickets and any other data. The copy saved in step 3 is the only way back to it.
</Callout>

If something fails after the services were stopped, the installer prints what state the
install is in and the exact commands to run. For a failed restore this is the start
command and, for the case where the database looks wrong, the command that puts the
`pre-rollback` file back by hand:

```bash
cd /opt/hostpanel && docker compose -f docker-compose.hub.yml up -d
```

Every message says exactly where the data is. If the swap itself breaks half way, the
live database is missing: the data from before the rollback is still in the database
`<name>_replaced`, the backup that was being restored is in `<name>_restore` (both
inside the database container, `<name>` being `POSTGRES_DB`), and the `pre-rollback`
file holds the same data as `<name>_replaced`. The installer prints the command that
renames one of them back into place, then the start command. Until a database with
the live name exists, a new `--rollback` is refused before it changes anything,
because its first step would drop both leftovers. When the database had no tables,
no `pre-rollback` copy is made and the message says so instead of naming a file.

A database named `postgres` cannot be rolled back with the installer: the restore
swaps databases by renaming them from the `postgres` maintenance database, so
`--rollback` refuses it before changing anything. Restore by hand as described below.

The rollback does not undo everything an upgrade may have changed. Uploaded files and
the plugin volumes are not part of the database dump and stay as they are, and keys
that a newer release added to `.env` stay in it (unused keys are harmless, but review
them). Only the database, the compose file, the Caddyfile and `IMAGE_TAG` go back.

`--rollback` cannot be combined with `--version`, because the version is the one recorded
with the backup. A backup taken while `IMAGE_TAG` was `latest` is refused; restore it by
hand as described below. To use an older backup than the one chosen, move the newer files
out of `backups/` first, or follow the manual steps.

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

# Verify the pre-upgrade dump file (a finished dump ends with the pg_dump completion line),
# then drop and restore. A damaged dump restored this way leaves the database empty,
# so keep the pre-rollback file above until the stack works:
if ! test -s pre-upgrade-2024-01-15-103000.sql || ! tail -n 5 pre-upgrade-2024-01-15-103000.sql | grep -q "^-- PostgreSQL database dump complete$"; then
  echo "Dump file is empty, cut short or missing, aborting."
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
