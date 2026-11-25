---
title: Adding an existing hosting account
description: "How staff attach a service to a hosting account that already exists on a server, what Hostpanel checks on the panel first, and the confirmations it asks for."
keywords: "existing account, add account, hosting account, server, panel check, duplicate service"
---

# Adding an existing hosting account <Label type="admin" />

Use **Add account** when the hosting account is already on a server and only the service is missing in Hostpanel: the account was restored after a reinstall, moved from another server or set up by hand. Nothing is created on the panel, no welcome email is sent and the service starts **Active**.

Open the client, go to **Products/Services** and click **Add account**. Choose the product, the server, the domain and the panel username, set the billing cycle, the recurring amount and the day the current period began (today when empty).

## What Hostpanel checks before it saves

1. **The account is on that server.** Hostpanel asks the server's panel to read the account by username. If the panel answers that it has no such account, nothing is saved and the answer is `SERVICE_HOSTING_ACCOUNT_NOT_ON_SERVER`. Check that you chose the server the account really lives on.
2. **No second live service for the same account.** A service that is not terminated already holds that username on that server: `SERVICE_HOSTING_ACCOUNT_EXISTS`. Two services for one account would suspend, bill and terminate it twice.
3. **The domain is not held by another live service.** If another service that is not terminated already uses the domain (on any server, for any client) the answer is `SERVICE_DOMAIN_ALREADY_SERVED`. The form shows the sentence and a checkbox; tick it to add the service anyway, for example when one domain really carries two services. A terminated service never counts.

When the panel reports the address the account is served on, it is recorded on the service as its **Assigned IP**.

## A panel that cannot read accounts

Reading an account needs a panel plugin that declares `provisioningPanelAccount`. The built in cPanel provider and a plugin without it cannot read accounts, so Hostpanel cannot check that the account exists and answers `SERVICE_HOSTING_ACCOUNT_UNVERIFIED`. The form offers a checkbox that you tick when you know the account is there, and the service is saved without an assigned address. The confirmation never overrides a panel that says the account is missing.

Changing the server, the username or the domain takes a confirmation back, because it vouched for those values.

## When the panel cannot be asked

| Code | HTTP status | Meaning |
|------|-------------|---------|
| `SERVICE_PANEL_SYNC_UPSTREAM_FAILED` | 502 | The panel did not answer or failed. Try again once it is reachable. |
| `PANEL_PLUGIN_UNAVAILABLE` | 503 | The plugin that serves the server's panel is switched off, not configured or being migrated. |

A confirmation does not stand in for either: the account must be checked, or the panel must say it cannot check.
