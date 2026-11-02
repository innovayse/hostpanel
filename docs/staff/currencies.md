---
title: Currencies, renewals and jobs
---

# Currencies, renewals and jobs <Label type="admin" />

## Currencies

Go to **Settings → Currencies** to manage the currencies your panel accepts.

**Columns:** Code, Numeric, Format, Decimals, Rate to Base, Base, Enabled, Actions.

### Add a currency

Click **Add Currency** and fill in:

| Field | Description |
|-------|-------------|
| ISO Code | Three-letter code, e.g. EUR |
| Numeric Code | ISO numeric code, e.g. 978 |
| Prefix | Symbol shown before the amount, e.g. € |
| Suffix | Symbol shown after the amount (optional) |
| Decimals | Number of decimal places |
| Rate to Base | Exchange rate relative to the base currency |
| Enabled | Whether clients can select this currency |

The **base currency** is always enabled and always has a rate of 1. It cannot be disabled (`CURRENCY_BASE_DISABLE_NOT_ALLOWED`).

- **Make base** — sets this currency as the new base. All other exchange rates are recalculated.
- **Update Exchange Rates** — fetches the latest rates from an external source and updates all non-base currencies automatically.
- **Update Product Prices** — recalculates all product prices from the base currency using current exchange rates.

<Callout type="warning">
Update Product Prices overwrites your existing product prices. This cannot be undone.
</Callout>

## How renewals work

When a service or domain is approaching its renewal date, the system:

1. Raises a renewal invoice `billing.renewal.invoice_days_before` days before the due date.
2. Sends reminder emails on the days listed in `billing.renewal.reminder_days`.
3. If `billing.renewal.auto_apply_credit` is enabled, automatically applies any credit balance toward the renewal invoice.
4. Once the invoice is paid, renews the service or domain for another billing cycle.

## Overdue handling

If a renewal invoice is not paid by its due date, it becomes **Overdue**. From there:

1. After `billing.renewal.suspend_after_overdue_days` days, the service is **Suspended**.
2. After `billing.renewal.terminate_after_suspended_days` more days, the service is **Terminated**.

Paying an overdue invoice unsuspends the service automatically.

Individual services can override the automatic suspension and termination behaviour. On a service record you can set:

- **Automatic billing** — whether renewal invoices are raised automatically for this service.
- **Override Auto-Suspend** — prevent the global rule from suspending this service.
- **Auto-Terminate End of Cycle** — terminate at the end of the billing period instead of waiting for the global rule.


## Renewal settings

<SettingTable :settings="[
  { name: 'billing.renewal.invoice_days_before', type: 'number', default: '14', description: 'Days before a service or domain renews to raise the renewal invoice. Allowed range: 1 to 90.' },
  { name: 'billing.renewal.reminder_days', type: 'string', default: '7,3,1', description: 'Comma-separated list of days before the due date on which to send renewal reminder emails. Each value must be between 1 and 90. Leave empty to send no reminders.' },
  { name: 'billing.renewal.auto_apply_credit', type: 'boolean', default: 'true', description: 'Automatically apply the client credit balance toward renewal invoices when they are raised.' },
  { name: 'billing.renewal.suspend_after_overdue_days', type: 'number', default: '3', description: 'Days after an invoice becomes overdue before the service is suspended. Allowed range: 0 to 90.' },
  { name: 'billing.renewal.terminate_after_suspended_days', type: 'number', default: '14', description: 'Days after a service is suspended before it is terminated. Allowed range: 1 to 90.' },
]" />

Change these in **Settings → System Settings**.

## The daily billing jobs

These jobs run automatically on a schedule. All times are UTC. Each job runs one instance at a time; a run missed while the server was down catches up on the next start. A failed run is logged and the next run proceeds as scheduled.

| Job | Default time | What it does |
|-----|-------------|--------------|
| Process Renewals | 05:00 UTC | Raises renewal invoices for services and domains due within the configured window |
| Process Billable Items | 06:00 UTC | Places queued billable items onto invoices |
| Send Renewal Reminders | 06:00 UTC | Sends renewal reminder emails |
| Check Overdue Invoices | 07:00 UTC | Marks unpaid past-due invoices as Overdue |
| Auto-Terminate Suspended | 08:00 UTC | Terminates services suspended past the configured limit |
| Suspend Overdue Services | 09:00 UTC | Suspends services with overdue invoices past the configured limit |
| Reconcile Gateway Payments | Every 5 min (by default) | Checks pending gateway payments and marks invoices paid when the gateway confirms |

The hour each job runs is set by the `ScheduledJobs` options. See the [Configuration reference](/operators/configuration) for the full list of `ScheduledJobs__*` settings.

## Automated staff emails

The system sends the following emails to staff automatically:

| Email | Trigger |
|-------|---------|
| `gateway-payment-stalled-admin` | A gateway payment has been pending for more than 24 hours |
| `domain-renewal-failed-admin` | An automatic domain renewal failed |
| `service-termination-failed-admin` | An automatic service termination failed |
