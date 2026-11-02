---
title: Back up and restore
description: How to back up the Hostpanel database and volumes, and how to restore them.
---

# Back up and restore

How to take a database dump, back up the volumes that hold uploaded files and
certificates, and restore them on the same or a different server.

## What to back up

| Item | Why |
|---|---|
| `/opt/hostpanel/.env` | Contains `ENCRYPTION_KEY` — without it, encrypted columns cannot be read after a restore. |
| `hostpanel_pgdata` volume (or a SQL dump) | All application data: clients, orders, invoices, services, settings. |
| `hostpanel_hostpanel_branding` volume | Logos, favicons and other branding files uploaded in the admin panel. |
| `hostpanel_hostpanel_plugins` volume | Plugins installed through the admin panel. |
| `hostpanel_caddydata` volume | Issued TLS certificates. Losing them means re-issuance, which can hit Let's Encrypt rate limits. |

The Redis and RabbitMQ volumes hold transient data (sessions, queued messages)
and do not need to be backed up.

<Callout type="danger">
Without the same <code>ENCRYPTION_KEY</code> from <code>.env</code>, a restored database cannot
be read — server passwords, API tokens and access hashes are stored encrypted.
Always back up <code>.env</code> together with every dump.
</Callout>

Losing `SESSION_ENCRYPTION_KEY` is less severe: it only signs everyone out of
the client portal.

## Take a database dump

**1. Run `pg_dump` against the running `hostpanel-db` container:**

```bash
cd /opt/hostpanel
docker compose -f docker-compose.hub.yml exec -T hostpanel-db \
  sh -c 'pg_dump -U "$POSTGRES_USER" "$POSTGRES_DB"' > backup.sql
```

**2. Copy `backup.sql` and `.env` to off-site storage** (object storage, an off-site backup service).

Alternatively, stop the stack and snapshot the `hostpanel_pgdata` Docker volume
directly, which preserves the raw data files.

## Restore a dump

**1. Stop the application containers** (keep the database running):

```bash
cd /opt/hostpanel
docker compose -f docker-compose.hub.yml stop \
  hostpanel-api hostpanel-client hostpanel-admin
```

**2. Drop and recreate the database:**

```bash
docker compose -f docker-compose.hub.yml exec -T hostpanel-db \
  sh -c 'dropdb -U "$POSTGRES_USER" "$POSTGRES_DB" && \
         createdb -U "$POSTGRES_USER" "$POSTGRES_DB"'
```

**3. Load the dump:**

```bash
docker compose -f docker-compose.hub.yml exec -T hostpanel-db \
  sh -c 'psql -U "$POSTGRES_USER" "$POSTGRES_DB"' < backup.sql
```

**4.** Make sure `.env` contains the same `ENCRYPTION_KEY` that was used when the dump was taken.

**5. Start the full stack:**

```bash
docker compose -f docker-compose.hub.yml up -d
```

**6.** Check that `GET /health` returns `200` and that you can sign in.

## Schedule backups

A nightly cron job on the host is the simplest approach. Create
`/etc/cron.d/hostpanel-backup`:

```bash
0 3 * * * root cd /opt/hostpanel && \
  docker compose -f docker-compose.hub.yml exec -T hostpanel-db \
  sh -c 'pg_dump -U "$POSTGRES_USER" "$POSTGRES_DB"' \
  > /var/backups/hostpanel/$(date +\%Y-\%m-\%d).sql
```

Keep at least seven daily dumps. Remove dumps older than thirty days:

```bash
0 4 * * * root find /var/backups/hostpanel -name '*.sql' -mtime +30 -delete
```

Make sure the backup directory exists and is readable only by root:

```bash
mkdir -p /var/backups/hostpanel && chmod 700 /var/backups/hostpanel
```

Copy the dumps and `.env` to off-site storage after each run.
