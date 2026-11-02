---
title: Upgrade
description: How to upgrade Hostpanel to a newer version using the installer or docker compose.
---

# Upgrade

How to move to a newer Hostpanel release using the installer or manually with
docker compose.

## Before you upgrade

<Callout type="warning">
Take a database dump before every upgrade. Database migrations run forward automatically on every API start and do not run backwards. If you need to go back, you will need the dump. See <a href="./backup">Back up and restore</a>.
</Callout>

Read the changelog entry for the new release before upgrading. Take a database
dump and note the current `IMAGE_TAG` in `/opt/hostpanel/.env` — you will need it
if you roll back.

### Check for duplicate gateway payments

The next release adds a unique index on gateway payment references. If any two
ledger rows share the same payment method and reference the migration stops and
the upgrade fails. Run the check script before upgrading — an empty result means
the migration will run cleanly.

The script is included starting from the release that adds the unique index
(the first release after v0.3.1). Fetch it from the release tag you are
upgrading to (replace `v1.0.0` with the real tag — a 404 means the release
does not contain the script yet and you can skip this step):

```bash
curl -fsSL -o /tmp/check-dup-payments.sh \
  https://raw.githubusercontent.com/innovayse/hostpanel/v1.0.0/scripts/check-duplicate-gateway-payments.sh \
  && sh /tmp/check-dup-payments.sh hostpanel-db
```

Downloading the script first means a 404 (script not in this release yet) fails the
`curl` step and does not pipe an empty response into `sh`. The first argument is the
database container name (default: `hostpanel-db`) — pass a different name if your
container is named otherwise.

If the server has no outbound internet access, copy the script from the repository
and run it over SSH:

```bash
ssh user@your-server 'sh -s -- hostpanel-db' < scripts/check-duplicate-gateway-payments.sh
```

If the script lists any rows, give each a distinct reference (or delete the
duplicate), then run it again before upgrading.

### Uploaded plugins and signed packages

The next release verifies the signature of every plugin on startup. Plugins
uploaded before this release carry no signature file (`plugin.sig`) and are
**skipped with a warning in the API log** on a production server after the upgrade. To restore them:

- Replace each plugin with a signed package, **or**
- Switch on **Allow unsigned plugins** in the admin panel under **Plugins** and
  restart the API.

The API log names each skipped plugin. Development servers are not affected.

## Upgrade with the installer

**1. Take a database dump and back up `.env`. Note the current `IMAGE_TAG`:**

```bash
umask 077
cd /opt/hostpanel
docker compose -f docker-compose.hub.yml exec -T hostpanel-db \
  sh -c 'pg_dump -U "$POSTGRES_USER" "$POSTGRES_DB"' \
  > pre-upgrade-$(date +%Y-%m-%d-%H%M%S).sql
cp .env .env.bak
```

Note the `IMAGE_TAG` value from `.env` — you will need it if you roll back.
If `IMAGE_TAG` is `latest`, find the exact version the running containers use before pulling new images:

```bash
docker image ls --format '{{.Repository}}:{{.Tag}}' | grep hostpanel
```

**2. Run the installer with `--version`:**

```bash
curl -fsSL \
  https://raw.githubusercontent.com/innovayse/hostpanel/main/install.sh \
  | bash -s -- --version 1.4.0
```

Always pass `--version`. The installer only updates `IMAGE_TAG` when `--version`
or `HOSTPANEL_VERSION` is given. Running it without `--version` downloads updated
compose and Caddyfile files but leaves `IMAGE_TAG` unchanged, so the old images
run with the new stack files.

**3. Check the output** for any warnings about new settings added to `.env` and review them.

**4. Verify the upgrade** — see [What to check after an upgrade](#what-to-check-after-an-upgrade).

When `.env` already exists the installer:

- Keeps all your existing settings — it does **not** overwrite `.env` or ask the
  domain and email questions again.
- Updates `IMAGE_TAG` to the version given with `--version` or `HOSTPANEL_VERSION`.
- Adds any new keys introduced in `.env.example` since your install. New secrets
  (e.g. `SESSION_ENCRYPTION_KEY`) are generated automatically; other new keys get
  the example value and a warning is printed — review them.
- Re-applies the TLS mode only when `--no-tls` or `HOSTPANEL_TLS` is passed.

## Upgrade by hand

**1. Set `IMAGE_TAG` in `/opt/hostpanel/.env`** to the target version. No leading `v` — use `1.4.0`, not `v1.4.0`:

```bash
# e.g. IMAGE_TAG=1.4.0
```

**2. Pull the new images and restart the stack:**

```bash
cd /opt/hostpanel
docker compose -f docker-compose.hub.yml pull
docker compose -f docker-compose.hub.yml up -d
```

Image tags on Docker Hub follow the pattern `<namespace>/hostpanel:<component>-<version>`,
for example `innovayse/hostpanel:api-1.4.0`. The moving `latest` tags
(`api-latest`, `client-latest`, `admin-latest`) always point at the current release.

## What to check after an upgrade

- The API log shows no startup errors:

  ```bash
  cd /opt/hostpanel && docker compose -f docker-compose.hub.yml logs hostpanel-api
  ```

- `GET /health` returns `200`.
- You can sign in to the admin panel.
- A test invoice PDF downloads correctly.
- The recurring billing jobs ran as expected — see the [billing guide](/staff/billing).

## Go back to a previous version

See [Rollback](/operators/rollback).
