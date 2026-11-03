---
title: Moving from another billing system
description: How to import clients, invoices, services and domains into Hostpanel from an existing billing system.
---

# Moving from another billing system

<Label type="admin" />

Hostpanel pulls data from the source system: a PHP addon installed there answers the panel's
requests page by page, and Hostpanel reads and imports the records. Nothing is written back to the
source system.

## How a migration works

A migration job holds a secret key. The addon on the source system reads every request, checks the
key, and returns one page of records at a time. Hostpanel imports each page before asking for the
next. The pull runs in the background — closing the browser does not stop it.

If the server restarts mid-pull, the recovery job (`RecoverInterruptedMigrationJobsCommand`) runs
every five minutes and marks as failed any job whose heartbeat has not updated in the last fifteen
minutes (both intervals are configurable), so the job can be retried.

The source address must be a public HTTPS address. Internal addresses and non-HTTPS schemes are not
allowed.

## What is imported

Records are pulled in this order. The order matters: a service needs its client and product to
exist first; a ticket reply needs its ticket.

| Order | Record type | Notes |
|---|---|---|
| 1 | Clients | One account per client. |
| 2 | Contacts | Sub-contacts of an imported client. |
| 3 | Products | Product catalogue, with product groups. |
| 4 | Invoices | Linked to the client by email. |
| 5 | Services | Linked to the client and to a product by name. |
| 6 | Domains | Linked to the client. |
| 7 | Tickets | Linked to the client. |
| 8 | Orders | Linked to products here. |
| 9 | Transactions | Linked to the invoice. |
| 10 | Quotes | Linked to the client. |
| 11 | Knowledgebase | Articles and their categories. |
| 12 | Ticket Replies | Replies on imported tickets. |
| 13 | Announcements | Public announcements. |
| 14 | Downloads | Files and their categories. |
| 15 | Network Issues | Status page incidents. |

You choose which types to include when you create the job. Unselected types are skipped entirely.

## What is not imported

**Passwords.** Migrated clients receive a random unusable password. They must reset it through the
password reset flow before they can sign in.

<Callout type="note">
Tell your customers to use "Forgot password" the first time they sign in after the move.
</Callout>

Records the addon does not export — for example, payment gateway credentials, email logs and
two-factor authentication settings — are not imported and must be set up again manually.

## Before you start

<Callout type="danger">
Back up the Hostpanel database before the first import. A migration cannot be rolled back
automatically; a backup is the only recovery path if something goes wrong.
</Callout>

<Callout type="tip">
Set up currencies, hosting servers and support departments in Hostpanel before importing. Services
are linked to products and servers; tickets are linked to departments. Records whose parent does
not exist here are skipped.
</Callout>

## Create a migration

<Steps>
<li>

**Open Migration Tools**

Go to **Migration Tools** in the admin panel and click **New Migration**.

Fill in:
- **Source URL** — the root address of the source system, for example `https://billing.example.com`.
  The addon path `/modules/addons/innovayse_migration/api.php` is appended automatically.
- **Label** (optional) — a name for this job, for example `Production Server`.
- **Data to import** — select the record types to pull. Use **Select All** or pick individual types.

Click **Create Migration**. A secret key is generated for the job.

</li>
<li>

**Install the addon on the source system**

Download `innovayse-migration.zip` from the **Setup Required** card and extract it to the root of
the source system. The files land in:

```
/modules/addons/innovayse_migration/
```

Open the source system's admin panel, go to **Admin → Addons**, find **Innovayse Migration** and
activate it. Open the addon's settings page and paste the secret key from the **Setup Required**
card into the **Migration Key** field.

</li>
<li>

**Test the connection**

Click **Check**. Hostpanel calls the addon's ping endpoint. The card shows one of:
- **Plugin connected** — the key was accepted.
- **Checking…** — the request is in flight.
- **Waiting for plugin…** — waiting to retry.
- **Not reachable** — the addon did not answer or refused the key. Check the source URL, confirm
  the addon is active, and confirm the key in the addon settings matches the one on the card.

</li>
<li>

**Start the import**

Click **Start Import**. The job moves to **Queued**, then to **Running** as the background worker
picks it up. The **Migrated**, **Skipped** and **Failed** counters update as records are processed.

</li>
</Steps>

## Job statuses

| Status | Meaning |
|---|---|
| Pending | Job created, not yet started. |
| Queued | Start requested; the background worker has not picked it up yet. |
| Running | Worker is actively pulling data. |
| Completed | All selected record types were pulled without error. |
| Failed | The pull stopped before it finished. |

## When a job fails

The failure reason is shown on the job card. Click **Retry import** to queue the pull again.
A retry resets the counters and starts from the beginning; records that already exist are skipped,
so retrying is safe.

| Reason | What to do |
|---|---|
| Source could not be reached | Check the source URL and confirm the source system is running. |
| Source refused the request, check the key | The key is wrong or the addon is not active. Re-copy the key from the job card into the addon settings. |
| Records could not be imported | Check the server log for details. Fix the underlying problem and retry. |
| Import was stopped | The pull was cancelled. Retry when ready. |
| The server stopped before the import finished | The process was interrupted (restart or crash). The recovery job runs every 5 minutes and fails any job with no heartbeat for 15 minutes (both configurable). Retry when the server is stable. |
| The source address is not allowed, use a public HTTPS address | The source URL must use HTTPS and resolve to a public address. Internal addresses are not allowed. |

## Run it again safely

<Callout type="warning">
Existing records are never updated by a second run. A record that already exists here is logged as
Skipped with "Already exists" and left unchanged.
</Callout>

Records are matched as follows: clients by email address, contacts by email, products by name,
domains by domain name, invoices by client and external id, tickets and quotes by client and
subject, orders by order number, transactions by client and transaction id.
Running the same job a second time only adds records that are new since the last run.

To re-import a changed record, delete it in Hostpanel first, then retry the job.

## Check the result

After the import completes, spot-check the data:

- Client count matches the source system.
- Open invoices and balances are correct.
- Active services are on the right servers.
- Domains have the right expiry dates.
- Open tickets are in the right departments.
- Pick five clients and check their services, invoices and tickets end to end.

Open the **Import Report** to review every skipped and failed record. See
[Import report and skip reasons](./import-report).

## After the move

Once the migration is complete and verified:

- Remove the addon from the source system, or clear its **Migration Key** field, so the endpoint
  no longer accepts requests.
- Delete old migration jobs (**Delete migration**) when you no longer need their import report.
  This removes the job and its full log and cannot be undone.

---

## For contributors: the addon

The addon lives at `backend/plugins/innovayse_migration/`. Its entry point is
`modules/addons/innovayse_migration/api.php`, which bootstraps the source system, authenticates
the request and dispatches it to `ApiKernel`.

### Layout

```
modules/addons/innovayse_migration/
  api.php
  Module.php
  lib/
    Mapping/
    Database/
    Addon/
    Export/
    Http/
    Auth/
```

### Request format

Every request is a JSON POST body:

<SettingTable :settings="[
  { name: 'key', type: 'string', description: 'The migration key configured in the addon settings.' },
  { name: 'action', type: 'string', description: 'One of: ping, totals, clients, contacts, product_groups, products, invoices, services, domains, tickets, ticket_replies, orders, transactions, quotes, knowledgebase_categories, knowledgebase, announcements, download_categories, downloads, network_issues.' },
  { name: 'page', type: 'number', default: '1', description: 'Page number, from 1.' },
  { name: 'perPage', type: 'number', default: '100', description: 'Page size, 10 to 500.' },
  { name: 'afterId', type: 'number', description: 'Keyset cursor: return only rows whose id is greater than this value. When present, page is ignored.' },
]" />

The `ping` action returns `{"ok":true}`. The `totals` action returns one count per entity type.
Dataset actions return a page of rows.

### Authentication

The addon checks the key in constant time. After 10 failed attempts within a 15-minute window,
the caller's address is locked out until the window expires. A successful authentication clears
the counter.

### Error responses

| HTTP status | Message | Condition |
|---|---|---|
| 401 | `Invalid migration key.` | Key missing, not configured, or wrong. |
| 429 | `Too many failed attempts.` | Address locked out by the throttle. |
| 503 | `Attempt log unavailable.` | The throttle's log file cannot be read or written. |
| 400 | `Unknown action.` | The `action` field names no known action. |
| 500 | `Source system bootstrap not found.` | `init.php` is not where the addon expects it. |
| 500 | `Export failed.` | The exporter threw; details are not returned to the caller. |

### Running the tests

```bash
cd backend/plugins/innovayse_migration
composer install
./vendor/bin/phpunit
```

The tests use an in-memory database driver and do not require a running source system.

### Error codes (Hostpanel API)

The Hostpanel API returns these codes for migration operations:

| Code | Message |
|---|---|
| `MIGRATION_JOB_NOT_FOUND` | No such migration job. |
| `MIGRATION_JOB_NOT_PENDING` | This migration job has already started or finished and cannot be started again. |
| `MIGRATION_JOB_NOT_FAILED` | This migration job has not failed, so it cannot be retried. |
| `MIGRATION_JOB_INVALID_TRANSITION` | This migration job cannot do that in its current state. |
| `MIGRATION_PLUGIN_NOT_FOUND` | The migration plugin is not available on this deployment. |
| `MIGRATION_CONNECTION_UPSTREAM_FAILED` | Could not reach the source system. Check the address and try again. |
