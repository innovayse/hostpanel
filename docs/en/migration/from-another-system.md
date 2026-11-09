---
title: Moving from WHMCS
description: How to import clients, invoices, services, domains and tickets into Hostpanel from WHMCS.
---

# Moving from WHMCS

<Label type="admin" />

<DemoLink path="/migration" />

Hostpanel imports your data from WHMCS. You install a small PHP addon in WHMCS. Hostpanel then asks
the addon for your records, one page at a time, and imports them. Nothing is written back to WHMCS.

## What you need

- A WHMCS install that runs on PHP 8.1 or later. The addon uses PHP 8.1 features.
- A public HTTPS address for that WHMCS install. Hostpanel calls it from your Hostpanel server.
- Admin access to both systems, and FTP or SSH access to the WHMCS files.

## From WHMCS in five steps

1. In Hostpanel, go to **Migration Tools**, click **New Migration**, enter your WHMCS address and
   pick the data to import. Hostpanel creates a secret key.
2. Download `innovayse-migration.zip` from the job card and extract it into the root folder of
   WHMCS.
3. In WHMCS, go to **Configuration > System Settings > Addon Modules**, activate
   **Hostpanel Migration**, click **Configure** and paste the key into **Migration Key**.
4. In Hostpanel, click **Check**. Wait for **Plugin connected**.
5. Click **Start Import**, then review the **Import Report** when it finishes.

The sections below explain each step and what to do when something goes wrong.

## How a migration works

A migration job holds a secret key. The addon on the source system reads every request, checks the
key, and returns one page of records at a time. Hostpanel imports each page before asking for the
next. The pull runs in the background, so closing the browser does not stop it.

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

Records the addon does not export are not imported, and you must set them up again by hand. These
include payment gateway credentials, email logs and two-factor authentication settings.

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
- **Source URL**: the root address of your WHMCS install, for example `https://billing.example.com`.
  Hostpanel adds the addon path `/modules/addons/innovayse_migration/api.php` for you. You can also
  paste the full address that the addon's own page shows; the path is not added twice.
- **Label** (optional): a name for this job, for example `Production Server`.
- **Data to import**: the record types to pull. Use **Select All** or pick individual types.

Click **Create Migration**. A secret key is generated for the job.

</li>
<li>

**Install the addon in WHMCS**

Download `innovayse-migration.zip` from the **Setup Required** card and extract it into the root
folder of WHMCS. The files land in:

```
/modules/addons/innovayse_migration/
```

In the WHMCS admin area, go to **Configuration > System Settings > Addon Modules**. Find
**Hostpanel Migration** and click **Activate**, then **Configure**. Paste the secret key from the
**Setup Required** card into the **Migration Key** field and click **Save Changes**.

Under **Access Control**, tick the admin roles that may open the addon's page in WHMCS. That page
shows whether a key is set and the **Source URL** Hostpanel calls. The import itself does not need
any role to be ticked.

</li>
<li>

**Test the connection**

Click **Check**. Hostpanel calls the addon's ping endpoint. The card shows one of:
- **Plugin connected**: the key was accepted.
- **Checking…**: the request is in flight.
- **Waiting for plugin…**: waiting to retry.
- **Not reachable**: the addon did not answer or refused the key. Check the source URL, confirm the
  addon is active in WHMCS, and confirm the key in the addon settings matches the one on the card.

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
A retry resets the counters and starts from the beginning. Records that were already imported are
skipped, so a retry only adds what is new.

| Reason | What to do |
|---|---|
| Source could not be reached | Check the source URL and confirm WHMCS is running. |
| Source refused the request, check the key | The addon answered with an error status. Most often the key is wrong or the addon is not active: re-copy the key from the job card into the addon settings. The same reason is shown when the addon answers 429 (too many wrong keys from this address; wait 15 minutes), 503 (the addon cannot write its attempt log in the server's temp folder) or 500 (the export failed; check the WHMCS error log). |
| Records could not be imported | Check the server log for details. Fix the underlying problem and retry. |
| Import was stopped | The pull was cancelled. Retry when ready. |
| The server stopped before the import finished | The process was interrupted (restart or crash). The recovery job runs every 5 minutes and fails any job with no heartbeat for 15 minutes (both configurable). Retry when the server is stable. |
| The source address is not allowed, use a public HTTPS address | The source URL must use HTTPS and resolve to a public address. Internal addresses are not allowed. |

## Run it again safely

<Callout type="note">
Every record type skips duplicates on a second run. Product groups, knowledgebase categories and
download categories are reused silently. All other existing records are logged as Skipped with
"Already exists" and left unchanged.
</Callout>

Records are matched as follows: clients by email address, contacts by email, products by name,
domains by domain name, services by client and the source service id (rows imported before the id was kept, by client and domain or panel username), invoices by
client and external id, tickets and quotes by client and subject, orders by order number,
transactions by client and transaction id, knowledgebase articles by title, announcements by title
and date (by title alone when the source has no date), network issues by title and start date (by
title alone when the source has no start date), downloads by title and category, ticket replies by
ticket, author, role and message. Download categories are matched by name.

Every service in the source record carries its own id, so a retry skips a service already imported
even when it has no domain or panel username, and two services that share a domain are both
imported. Only services imported before the id was kept are matched by client and domain (or panel
username); one of those with neither has no match key and is imported again on a retry.

For types that have a match key (clients, contacts, products, services, domains, invoices, tickets, quotes, orders, transactions, knowledgebase articles, announcements, network issues, downloads and ticket replies), running the same job a second time only adds records that are new since the last run.

To re-import a changed record, delete it in Hostpanel first, then retry the job.

## Check the result

After the import completes, spot-check the data:

- Client count matches WHMCS.
- Open invoices and balances are correct.
- Active services are on the right servers.
- Domains have the right expiry dates.
- Open tickets are in the right departments.
- Pick five clients and check their services, invoices and tickets end to end.

Open the **Import Report** to review every skipped and failed record. See
[Import report and skip reasons](./import-report).

## After the move

Once the migration is complete and verified:

- In WHMCS, clear the addon's **Migration Key** field and save. With no key set, the addon refuses
  every request. Then deactivate the addon and delete the `modules/addons/innovayse_migration/`
  folder.
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
  autoload.php
  innovayse_migration.php
  lib/
    Module.php
    ApiKernel.php
    Router.php
    Paging/
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
