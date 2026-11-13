---
title: Back up and restore
description: How to back up the Hostpanel database and volumes, and how to restore them.
---

# Back up and restore

How to take a database dump, back up the volumes that hold uploaded files and
restore them on the same or a different server.

## What to back up

| Item | Why |
|---|---|
| `/opt/hostpanel/.env` | Contains `ENCRYPTION_KEY`: without it, encrypted columns cannot be read after a restore. |
| `hostpanel_pgdata` volume (or a SQL dump) | All application data: clients, orders, invoices, services, settings. |
| `hostpanel_hostpanel_branding` volume | Logos, favicons and other branding files uploaded in the admin panel. |
| `hostpanel_hostpanel_plugins` volume | Plugins installed through the admin panel. |

The Redis and RabbitMQ volumes hold transient data (sessions, queued messages)
and do not need to be backed up.

The `hostpanel_caddydata` volume holds issued TLS certificates. Losing it causes
re-issuance on next start, which can hit Let's Encrypt rate limits. Back it up
if you want to avoid re-issuance, but it is not required for a data restore.

<Callout type="danger">
Without the same <code>ENCRYPTION_KEY</code> from <code>.env</code>, a restored database cannot
be read: server passwords, API tokens and access hashes are stored encrypted.
Always back up <code>.env</code> together with every dump.
</Callout>

Losing `SESSION_ENCRYPTION_KEY` is less severe: it only signs everyone out of
the client portal.

## Take a database dump

**1. Run `pg_dump` against the running `hostpanel-db` container:**

```bash
umask 077
cd /opt/hostpanel
docker compose -f docker-compose.hub.yml exec -T hostpanel-db \
  sh -c 'pg_dump -U "$POSTGRES_USER" "$POSTGRES_DB"' \
  > backup-$(date +%Y-%m-%d-%H%M%S).sql
```

**2. Copy the dump and `.env` to off-site storage** (object storage, an off-site backup service).

Alternatively, stop the stack and snapshot the `hostpanel_pgdata` Docker volume
directly, which preserves the raw data files.

## Restore a dump

<Callout type="danger">
Restoring a dump permanently removes everything written after the dump was taken: orders, payments, tickets and any other data. Confirm you have the right dump file before proceeding.
</Callout>

**1. Stop the application containers** (keep the database running):

```bash
cd /opt/hostpanel
docker compose -f docker-compose.hub.yml stop \
  hostpanel-api hostpanel-client hostpanel-admin hostpanel-caddy
```

**2. Dump the current database state** before overwriting it:

```bash
umask 077
docker compose -f docker-compose.hub.yml exec -T hostpanel-db \
  sh -c 'pg_dump -U "$POSTGRES_USER" "$POSTGRES_DB"' \
  > pre-restore-$(date +%Y-%m-%d-%H%M%S).sql
```

**3. Verify the backup file, then drop and restore:**

Replace `backup-2024-01-15-030000.sql` with the actual file name.

```bash
if ! test -s backup-2024-01-15-030000.sql; then
  echo "Backup file is empty or missing, aborting."
else
  docker compose -f docker-compose.hub.yml exec -T hostpanel-db \
    sh -c 'dropdb -U "$POSTGRES_USER" "$POSTGRES_DB" && \
           createdb -U "$POSTGRES_USER" "$POSTGRES_DB"'
  docker compose -f docker-compose.hub.yml exec -T hostpanel-db \
    sh -c 'psql -v ON_ERROR_STOP=1 --single-transaction \
           -U "$POSTGRES_USER" "$POSTGRES_DB"' < backup-2024-01-15-030000.sql
fi
```

**4.** Make sure `.env` contains the same `ENCRYPTION_KEY` that was used when the dump was taken.

**5. Restore the branding and plugins volumes:**

```bash
# Restore branding files:
docker run --rm \
  -v hostpanel_hostpanel_branding:/data \
  -v /path/to/your/backups:/backup \
  alpine sh -c 'cd /data && tar xzf /backup/branding-2024-01-15.tar.gz'

# Restore plugins:
docker run --rm \
  -v hostpanel_hostpanel_plugins:/data \
  -v /path/to/your/backups:/backup \
  alpine sh -c 'cd /data && tar xzf /backup/plugins-2024-01-15.tar.gz'

# Fix ownership so the API can read the restored files:
cd /opt/hostpanel
docker compose -f docker-compose.hub.yml run --rm --no-deps \
  --user 0 --cap-add CHOWN --entrypoint sh hostpanel-api \
  -c 'chown -R "$APP_UID" /app/wwwroot/uploads /app/plugins-installed'
```

**6. Start the full stack:**

```bash
docker compose -f docker-compose.hub.yml up -d
```

**7.** Check that `GET /health` returns `200` and that you can sign in.

## Schedule backups

A nightly cron job on the host is the simplest approach.

**1. Create the backup directory:**

```bash
mkdir -p /var/backups/hostpanel && chmod 700 /var/backups/hostpanel
```

**2. Create `/usr/local/bin/hostpanel-backup`:**

```bash
#!/bin/bash
set -eu
umask 077
DEST=/var/backups/hostpanel/db-$(date +%Y-%m-%d-%H%M%S).sql
cd /opt/hostpanel

# Database dump
docker compose -f docker-compose.hub.yml exec -T hostpanel-db \
  sh -c 'pg_dump -U "$POSTGRES_USER" "$POSTGRES_DB"' > "$DEST"
if ! test -s "$DEST"; then
  echo "pg_dump produced an empty file, aborting" >&2
  rm -f "$DEST"
  exit 1
fi
chmod 600 "$DEST"

# Branding and plugins volumes
BRAND_ARCHIVE=/var/backups/hostpanel/branding-$(date +%Y-%m-%d).tar.gz
PLUGIN_ARCHIVE=/var/backups/hostpanel/plugins-$(date +%Y-%m-%d).tar.gz

docker run --rm \
  -v hostpanel_hostpanel_branding:/data \
  -v /var/backups/hostpanel:/backup \
  alpine tar czf /backup/branding-$(date +%Y-%m-%d).tar.gz -C /data .
chmod 600 "$BRAND_ARCHIVE"

docker run --rm \
  -v hostpanel_hostpanel_plugins:/data \
  -v /var/backups/hostpanel:/backup \
  alpine tar czf /backup/plugins-$(date +%Y-%m-%d).tar.gz -C /data .
chmod 600 "$PLUGIN_ARCHIVE"

# Remove backups older than 30 days
find /var/backups/hostpanel -name 'db-*.sql' -mtime +30 -delete
find /var/backups/hostpanel -name 'branding-*.tar.gz' -mtime +30 -delete
find /var/backups/hostpanel -name 'plugins-*.tar.gz' -mtime +30 -delete
```

```bash
chmod +x /usr/local/bin/hostpanel-backup
```

**3. Create `/etc/cron.d/hostpanel-backup`:**

```
0 3 * * * root /usr/local/bin/hostpanel-backup
```

Copy the dumps and `.env` to off-site storage after each run.
