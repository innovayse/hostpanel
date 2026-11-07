---
title: Upgrade
description: How to upgrade Hostpanel to a newer version using the installer or docker compose.
---

# Upgrade

How to move to a newer Hostpanel release using the installer or manually with
docker compose.

## Before you upgrade

<Callout type="warning">
Take a database dump before every upgrade. Database migrations run forward automatically on every API start and do not run backwards. If you need to go back, you will need the dump. The installer takes one for you (see <a href="#upgrade-with-the-installer">Upgrade with the installer</a>); an upgrade by hand needs your own. See <a href="./backup">Back up and restore</a>.
</Callout>

Read the changelog entry for the new release before upgrading. Take a database
dump and note the current `IMAGE_TAG` in `/opt/hostpanel/.env`. You will need it
if you roll back.

### Check for duplicate gateway payments

Release 0.4.0 adds a unique index on gateway payment references. If any two
gateway payment rows share the same payment method and reference, the migration stops
and the upgrade fails. Run the check script before upgrading. An empty result means
the migration will run cleanly.

The script is part of release 0.4.0. It works on a 0.3.x database and on a 0.4.0 database,
so run it before the upgrade. Fetch it from the release tag:

```bash
curl -fsSL -o /tmp/check-dup-payments.sh \
  https://raw.githubusercontent.com/innovayse/hostpanel/v0.4.0/scripts/check-duplicate-gateway-payments.sh \
  && sh /tmp/check-dup-payments.sh hostpanel-db
```

Downloading the script first means a failed download does not pipe an empty response
into `sh`. The first argument is the database container name (default: `hostpanel-db`).
Pass a different name if your container is named otherwise.

If the server has no outbound internet access, copy the script from the repository
and run it over SSH:

```bash
ssh user@your-server 'sh -s -- hostpanel-db' < scripts/check-duplicate-gateway-payments.sh
```

If the script lists any rows, give each a distinct reference (or delete the
duplicate), then run it again before upgrading.

### The database is renamed

Release 0.4.0 renames every table, key, index and constraint to PascalCase (columns
already were). The migration renames them in place and keeps all data. Update your own
SQL, reports and scripts to the new names. A dump taken before the upgrade restores only
onto the previous version, so keep it for a rollback and do not restore it onto 0.4.0.

### Configuration that must be set

Release 0.4.0 requires these settings. Set them in `.env` before the first start:

- `ENCRYPTION_KEY` in every environment.
- `SESSION_ENCRYPTION_KEY` for the client app sessions. The installer generates it for you.
- `EDGE_SCHEME`, the scheme the public hosts are reached with (`https://` or `http://`).
- The trusted proxy counts under `RateLimit` (`KnownProxies`, `KnownNetworks`, `TrustedProxyCount`).
- Absolute `Cors:AllowedOrigins` entries such as `https://host.example.com`.

In Production the API refuses to start while a secret still holds a placeholder value.

### Saved cards and the payment plugin

Release 0.4.0 removes the built in card gateways. Their plugins are not publicly
available yet, so if you take card payments, stay on 0.3.1 until a card plugin is
available. Without a card plugin no card payment method is offered at checkout. Bank
transfer keeps working.

The upgrade keeps the saved card customer references. It moves them to the payment
plugin with the id `stripe`, because the only gateway that ever stored them was that
one, and they are used once a plugin with that id is installed. If your plugin uses
another id, rewrite `GatewayModuleId` in `GatewayCustomers` to that id. Until a
matching plugin is installed, those clients are asked for their card again.

### Uploaded plugins and signed packages

Release 0.4.0 verifies the signature of every plugin on startup. Plugins
uploaded before this release carry no signature file (`plugin.sig`) and are
**skipped with a warning in the API log** on a production server after the upgrade. To restore them:

- Replace each plugin with a signed package, **or**
- Switch on **Allow unsigned plugins** in the admin panel under **Plugins** and
  restart the API.

The API log names each skipped plugin. Development servers are not affected.

## Upgrade with the installer

**1. Let the installer take the database backup, and back up `.env`.**

When `.env` already exists, the installer dumps the database through the running
`hostpanel-db` container before it downloads or changes anything. The dump goes to
`backups/` in the install directory, in a file named with the date, the time and the
version it belongs to, for example `backups/hostpanel-20261107-101500-0.3.1.sql`.
The file and the folder are readable by their owner only.

- If the dump fails, comes out empty or is cut short (a finished dump ends with the
  `-- PostgreSQL database dump complete` line), the installer stops with an error
  before it touches the compose file, `.env` or the running stack.
- If the database container is stopped but its data volume exists, the installer starts
  the database alone, waits until it accepts connections and then takes the dump. If
  there is no database yet (a first install that stopped before it started), there is
  nothing to back up and the installer goes on. `--skip-backup` skips the dump and
  leaves the data unprotected; do not use it on a stack that holds data.
- A database without any table is not kept as a backup, so it never replaces one that
  holds data.
- The newest 5 backups are kept, and so is the oldest backup of each of the newest 10
  versions, because the one taken right before an upgrade is the way back to the old
  version even after you run the installer again. Older ones are removed, so the folder
  does not grow with every release. Change the numbers with `--keep <n>` or
  `HOSTPANEL_BACKUP_KEEP`, and `--keep-versions <n>` or
  `HOSTPANEL_BACKUP_KEEP_VERSIONS`. Backup names use UTC.
- Keep a copy of `.env` as well, since the dump cannot be read without its
  `ENCRYPTION_KEY`: `cp .env .env.bak`.
- Install with `--version <x.y.z>`. A backup taken while `IMAGE_TAG` is the moving
  `latest` has no version to go back to, so `--rollback` refuses it.

If you prefer to take the dump yourself, or to keep an extra copy elsewhere:

```bash
umask 077
cd /opt/hostpanel
docker compose -f docker-compose.hub.yml exec -T hostpanel-db \
  sh -c 'pg_dump -U "$POSTGRES_USER" "$POSTGRES_DB"' \
  > pre-upgrade-$(date +%Y-%m-%d-%H%M%S).sql
cp .env .env.bak
```

Note the `IMAGE_TAG` value from `.env`. You will need it if you roll back.
If `IMAGE_TAG` is `latest`, find the exact version the running containers use before pulling new images:

```bash
docker image ls --format '{{.Repository}}:{{.Tag}}' | grep hostpanel
```

**2. Run the installer with `--version`:**

```bash
curl -fsSL \
  https://raw.githubusercontent.com/innovayse/hostpanel/main/install.sh \
  | bash -s -- --version 0.4.0
```

Always pass `--version`. The installer only updates `IMAGE_TAG` when `--version`
or `HOSTPANEL_VERSION` is given. Running it without `--version` downloads updated
compose and Caddyfile files but leaves `IMAGE_TAG` unchanged, so the old images
run with the new stack files.

**3. Check the output** for any warnings about new settings added to `.env` and review them.

**4. Verify the upgrade.** See [What to check after an upgrade](#what-to-check-after-an-upgrade).

At the end of an upgrade the installer prints the backup file it took and the command
that goes back. To go back, run the installer with `--rollback`, see
[Roll back with the installer](/operators/rollback#roll-back-with-the-installer).

When `.env` already exists the installer:

- Keeps all your existing settings. It does **not** overwrite `.env` or ask the
  domain and email questions again.
- Updates `IMAGE_TAG` to the version given with `--version` or `HOSTPANEL_VERSION`.
- Adds any new keys introduced in `.env.example` since your install. New secrets
  (e.g. `SESSION_ENCRYPTION_KEY`) are generated automatically; other new keys get
  the example value and a warning is printed. Review them.
- Re-applies the TLS mode only when `--no-tls` or `HOSTPANEL_TLS` is passed.

## Upgrade by hand

**1. Set `IMAGE_TAG` in `/opt/hostpanel/.env`** to the target version. No leading `v`, use `0.4.0`, not `v0.4.0`:

```bash
# e.g. IMAGE_TAG=0.4.0
```

**2. Pull the new images and restart the stack:**

```bash
cd /opt/hostpanel
docker compose -f docker-compose.hub.yml pull
docker compose -f docker-compose.hub.yml up -d
```

Image tags on Docker Hub follow the pattern `<namespace>/hostpanel:<component>-<version>`,
for example `innovayse/hostpanel:api-0.4.0`. The moving `latest` tags
(`api-latest`, `client-latest`, `admin-latest`) always point at the current release.

## What to check after an upgrade

- The API log shows no startup errors:

  ```bash
  cd /opt/hostpanel && docker compose -f docker-compose.hub.yml logs hostpanel-api
  ```

- `GET /health` returns `200`.
- You can sign in to the admin panel.
- A test invoice PDF downloads correctly.
- The recurring billing jobs ran as expected, see the [billing guide](/staff/billing).

## Go back to a previous version

See [Rollback](/operators/rollback).

A dump taken before the upgrade to 0.4.0 restores only onto the previous version. The
upgrade renames the database tables, keys and indexes to PascalCase, so restore that
dump together with the previous images and never onto 0.4.0.
