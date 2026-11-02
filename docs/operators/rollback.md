---
title: Rollback
description: How to go back to a previous Hostpanel version after an upgrade.
---

# Rollback

How to go back to a previous Hostpanel release when an upgrade causes a problem.

<Callout type="warning">
If the new version added a database migration, restore the dump you took before
the upgrade first — migrations do not run backwards. Running the old images against
a migrated database can cause data corruption.
</Callout>

## Before you roll back

Check whether the release you are rolling back from added any database migrations.
If it did, you must restore your pre-upgrade dump as part of the rollback.

## Roll back the images

**1. Stop the application containers** (leave the database running):

```bash
cd /opt/hostpanel
docker compose -f docker-compose.hub.yml stop \
  hostpanel-api hostpanel-client hostpanel-admin hostpanel-caddy
```

**2. Set `IMAGE_TAG` in `/opt/hostpanel/.env`** back to the previous version.
No leading `v` — use `1.3.2`, not `v1.3.2`:

```bash
# e.g. IMAGE_TAG=1.3.2
```

**3. If the new release added migrations, restore your pre-upgrade database dump.**

Run these commands against the still-running `hostpanel-db`:

```bash
cd /opt/hostpanel

# Safety dump of the current (migrated) state:
docker compose -f docker-compose.hub.yml exec -T hostpanel-db \
  sh -c 'pg_dump -U "$POSTGRES_USER" "$POSTGRES_DB"' \
  > pre-rollback-$(date +%Y-%m-%d-%H%M%S).sql

# Drop and recreate, then load the pre-upgrade dump
# (replace the filename with the dated file from upgrade step 1):
docker compose -f docker-compose.hub.yml exec -T hostpanel-db \
  sh -c 'dropdb -U "$POSTGRES_USER" "$POSTGRES_DB" && \
         createdb -U "$POSTGRES_USER" "$POSTGRES_DB"'

docker compose -f docker-compose.hub.yml exec -T hostpanel-db \
  sh -c 'psql -v ON_ERROR_STOP=1 --single-transaction \
         -U "$POSTGRES_USER" "$POSTGRES_DB"' < pre-upgrade-2024-01-15-103000.sql
```

**4. Pull the old images and restart the stack:**

```bash
cd /opt/hostpanel
docker compose -f docker-compose.hub.yml pull
docker compose -f docker-compose.hub.yml up -d
```

**5.** Check that `GET /health` returns `200` and that you can sign in.

## After rolling back

Open a bug report describing what went wrong so the issue can be fixed in the
next release. Include the version you rolled back from and the error you saw in
the API log:

```bash
cd /opt/hostpanel && docker compose -f docker-compose.hub.yml logs hostpanel-api
```
