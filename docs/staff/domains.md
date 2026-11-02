---
title: Domains
---

# Domains <Label type="admin" />

## Domain statuses

| Status | What it means |
|--------|---------------|
| Pending Registration | Registration requested; waiting for the registrar to confirm |
| Pending Transfer | Transfer initiated; waiting for the registrar to complete it |
| Active | Registered and resolving normally |
| Expired | Registration period ended; domain is no longer resolving |
| Redemption | In the registrar's redemption grace period; recoverable with a fee |
| Transferred | Transferred out to another registrar |
| Cancelled | Registration was cancelled before it became Active |

Customers see Pending Registration, Active, Expired and Cancelled in their client portal. See [Client portal guide](/customers/portal) for the customer view.

## Domain registration

When a client orders and pays for a domain:

1. Hostpanel sends a registration request to the registrar.
2. The domain moves to **Pending Registration**.
3. Most registrars confirm immediately and the domain becomes **Active**. Some confirm later; `SyncPendingDomainStatusesCommand` checks all pending domains every **15 minutes** and activates them once the registrar confirms.

When registration succeeds, the customer receives `domain-registered`. If registration fails, the customer receives `domain-registration-failed`, staff receive `domain-registration-failed-admin`, and the order line is refunded automatically. The domain record stays Cancelled.

## Domain transfer in

When a client transfers a domain in:

1. The client provides the domain name and its authorization code (EPP code) during the order.
2. The domain moves to **Pending Transfer**.
3. When the registrar confirms the transfer, the domain moves to **Active** and the customer receives `domain-transferred`.

To cancel a pending transfer, open the domain page and use the **Registrar Commands** card. If the registrar refuses the cancellation, the panel shows `DOMAIN_TRANSFER_CANCEL_UPSTREAM_FAILED`.

## The domain page

Open a client, go to their **Domains** tab and click a domain to open its detail page.

**Cards on the page:**

| Card | Contents |
|------|---------|
| Domain Info | Order, order type, domain name, registrar, first payment amount, recurring amount |
| Nameservers | Nameserver 1–5; saved to the registrar on change (`NAMESERVERS_UPSTREAM_FAILED` if the registrar refuses) |
| Registrar Commands | Register, Renew, Modify Contact Details, Get EPP Code, Enable / Disable ID Protection |
| Dates & Billing | Registration period, expiry date, renewal price, subscription ID |
| Management Tools | DNS Management, Email Forwarding, ID Protection, Auto Renew, Registrar Lock |
| Admin Notes | Staff-only notes |
| DNS Records | DNS record table (add, edit, delete; `DNS_RECORD_UPSTREAM_FAILED` if the registrar refuses) |
| Email Forwarding | Forwarding rule table |
| Domain Reminder History | Table of reminder emails sent for this domain |

**Management Tools toggles:**
- **DNS Management** — enables the DNS record table.
- **Email Forwarding** — enables the email forwarding table.
- **ID Protection** / **WHOIS Privacy** — hides registrant contact details in WHOIS (`WHOIS_PRIVACY_UPSTREAM_FAILED` if refused; `REGISTRAR_WHOIS_PRIVACY_UNAVAILABLE` if the registrar does not support it).
- **Auto Renew** — controls whether a renewal invoice is raised automatically before expiry.
- **Registrar Lock** — prevents unauthorised transfers out (`REGISTRAR_LOCK_UPSTREAM_FAILED` if the registrar refuses).

A domain that has no registrar reference returns `DOMAIN_NO_REGISTRAR_REF` when a registrar command is attempted. Cancelling a domain that cannot be cancelled returns `DOMAIN_CANCEL_NOT_ALLOWED`.

## All domains

- **Domains** — shows all active and historical domain records across all clients, with filters for status, registrar and TLD.
- **Domain Registrations** — shows registration and transfer orders in progress, useful for monitoring pending domains.

## Domain renewal

A renewal invoice is raised automatically by `ProcessRenewalsCronCommand` (runs at **05:00 UTC**). Renewal reminder emails are sent by `SendRenewalRemindersCronCommand` (runs at **06:00 UTC**) on the days configured in `billing.renewal.reminder_days`.
- The domain must be Active.
- Auto Renew must be enabled on the domain.
- The invoice is raised `billing.renewal.invoice_days_before` days before the expiry date.

After payment, Hostpanel sends a renewal request to the registrar. If the registrar accepts, the expiry date is extended and the customer receives `domain-renewed`. If the registrar refuses, staff receive `domain-renewal-failed-admin`.

**Manual renewal:** On the domain page, click **Renew** in the Registrar Commands card, enter the number of years and confirm. The panel shows `RENEWAL_INVOICE_OPEN` if there is already an open renewal invoice for this domain — pay or cancel that invoice first.

Attempting to renew a domain that cannot be renewed (wrong status) returns `DOMAIN_RENEW_NOT_ALLOWED`.

## Expiry warnings

`CheckDomainExpiriesCommand` runs daily at **09:00 UTC**. For each domain approaching expiry it:

1. Sends one reminder email per threshold: **30**, **14**, **7** and **1 day** before expiry (these thresholds are fixed and not configurable with `billing.renewal.reminder_days`; that setting is for service renewal reminders).
2. Marks domains past their expiry date as **Expired** and sends the `domain-expired` email.
3. Skips a reminder if an open renewal invoice already exists and renewal reminders are configured.

Mail templates used: `domain-expiring` (approaching expiry) and `domain-expired` (past expiry).

The **Domain Reminder History** table on the domain page shows each reminder with its label and sent timestamp:

| Label | When sent |
|-------|-----------|
| 30 days before expiry | 30 days before the expiry date |
| 14 days before expiry | 14 days before the expiry date |
| 7 days before expiry | 7 days before the expiry date |
| 1 day before expiry | 1 day before the expiry date |
| Expired notice | After the expiry date passes |
| Redemption notice | While the domain is in the redemption period |

The **Domain Renewal Reminder Emails** report (under Reports) lists all reminder emails sent across all clients.

## When the registrar refuses

| Error | What to check |
|-------|---------------|
| `REGISTRAR_DOMAIN_UNAVAILABLE` | The domain is already taken or restricted |
| `REGISTRAR_INSUFFICIENT_FUNDS` | Top up the registrar account balance |
| `REGISTRAR_CONTACT_VALIDATION_FAILED` | Update the client's contact details to pass the registrar's validation rules |
| `REGISTRAR_OPERATION_UNSUPPORTED` | This registrar does not support the operation (e.g. ID protection) |
| `REGISTRAR_ORDER_UPSTREAM_FAILED` | The registrar returned an unexpected error; check the Gateway Transaction Log |
| `DOMAIN_REGISTRATION_UPSTREAM_FAILED` | Registration failed at the registrar; check the account balance and domain eligibility |
| `DOMAIN_RENEWAL_UPSTREAM_FAILED` | Renewal failed at the registrar; check balance and domain status |
| `DOMAIN_TRANSFER_UPSTREAM_FAILED` | Transfer failed; check the EPP code and registrar lock status |
| `DOMAIN_EPP_CODE_UPSTREAM_FAILED` | Could not retrieve the EPP code; check the registrar connection |
| `DOMAIN_PRODUCT_NOT_CONFIGURED` | No domain product is linked to this TLD; check TLD Pricing |
| `DOMAIN_ALREADY_REGISTERED` | A domain record for this name already exists in the system |
