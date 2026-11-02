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

**1. Stop the stack:**

```bash
cd /opt/hostpanel && docker compose -f docker-compose.hub.yml down
```

**2. If the new release added migrations, restore your pre-upgrade database dump.**

See [Restore a dump](/operators/backup#restore-a-dump) and use the dump you took
before the upgrade. Keep `hostpanel-db` running while restoring, then bring it
down again before the next step.

**3. Set `IMAGE_TAG` in `/opt/hostpanel/.env`** back to the previous version.
No leading `v` — use `1.3.2`, not `v1.3.2`:

```bash
# e.g. IMAGE_TAG=1.3.2
```

**4. Pull the old images and restart:**

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
