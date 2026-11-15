---
title: Migration in fifteen minutes
description: A short guide to moving clients, invoices and services from WHMCS into Hostpanel in five steps, with the checks to make before and after.
keywords: "migration guide, whmcs import, quick migration, import checklist"
---

# Migration in fifteen minutes

<Label type="admin" />

This is the short route for moving from WHMCS. It lists what to prepare, the five steps and the checks afterwards. Every step links to the full page, so read [Moving from WHMCS](./from-another-system) when something is unclear. The import runs in the background, so how long it takes depends on how many records you move. The steps below are the part you do by hand.

## Before you start

- Make a backup of the Hostpanel database and `.env`. A migration cannot be rolled back automatically. See [Back up and restore](/operators/backup).
- Check that the WHMCS install runs on PHP 8.1 or later and has a public HTTPS address that your Hostpanel server can reach. You need admin access to both systems and FTP or SSH access to the WHMCS files.
- In Hostpanel, set up your currencies, hosting servers and support departments first, see [Currencies, renewals and jobs](/staff/currencies), [Servers and server groups](/servers/servers) and [Support tickets](/staff/support-tickets). Records whose parent does not exist here are skipped.
- Tell your customers to use "Forgot password" the first time they sign in. Passwords are not imported.

## The five steps

<Steps>
<li>

**Create the job.** In Hostpanel, go to **Migration Tools** and click **New Migration**. Enter the root address of WHMCS as the **Source URL**, pick the data to import and click **Create Migration**. A secret key is generated for the job.

</li>
<li>

**Install the addon.** Download `innovayse-migration.zip` from the **Setup Required** card and extract it into the root folder of WHMCS. The files land in `modules/addons/innovayse_migration/`.

</li>
<li>

**Enter the key.** In WHMCS, go to **Configuration > System Settings > Addon Modules**, activate **Hostpanel Migration**, click **Configure**, paste the key into **Migration Key** and save.

</li>
<li>

**Check the connection.** In Hostpanel, click **Check** and wait for **Plugin connected**. If the card says **Not reachable**, check the source URL, that the addon is active and that the key matches.

</li>
<li>

**Start the import.** Click **Start Import**. The **Migrated**, **Skipped** and **Failed** counters update while the background worker runs, and closing the browser does not stop it.

</li>
</Steps>

Records are imported in a fixed order: clients first, then contacts, products, invoices, services and the rest, because a service needs its client and product to exist. The full list is in [What is imported](./from-another-system#what-is-imported).

## When the import finishes

1. Open the **Import Report** and filter by **Skipped** and **Failed**. Each skip reason has a fix in [Import report and skip reasons](./import-report).
2. Fix the cause, for example create the missing department or a product with the matching name, and click **Retry import**. Records that already exist are skipped, so a retry only adds what is new. See [Run it again safely](./from-another-system#run-it-again-safely).
3. Spot-check the data: the client count, open invoices and balances, active services on the right servers, domain expiry dates and open tickets. Pick five clients and compare their services, invoices and tickets end to end.

## Clean up

When the result is verified, clear the **Migration Key** in the WHMCS addon settings and save, deactivate the addon and delete the `modules/addons/innovayse_migration/` folder. Delete the migration job when you no longer need its report, which cannot be undone. Details are in [After the move](./from-another-system#after-the-move).
