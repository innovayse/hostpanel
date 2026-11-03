---
title: Payment gateways
---

# Payment gateways <Label type="admin" />

Hostpanel ships with three payment methods. No payment gateway is enabled by default; an operator enables each one after entering their credentials.

## What ships with Hostpanel

| Method | How it works | When to use |
|--------|-------------|-------------|
| **Bundled card gateway** | Card form on the checkout page; the card number never reaches Hostpanel | Accepting debit and credit cards directly at checkout |
| **Bundled bank gateway** | Plugin; the client is redirected to the bank's hosted card page | Card payments routed through a local bank gateway |
| **Bank Transfer** | Offline; the client pays by bank transfer and staff record the payment manually | Markets where bank transfer is the norm, or as a fallback |

<Callout type="note">
The gateway names shown in your admin panel depend on which plugins are installed. The bundled bank gateway ships as a plugin pre-installed in the server image; it does not appear in the Plugin Manager.
</Callout>

## The Payment Gateways screen

Go to **Integrations**, category **Payment Gateways**. Each method appears as a card showing:

- **Active** — configured and enabled
- **Inactive** — configured but not enabled
- **Not configured** — fields required for connection are empty
- **Plugin** badge — the integration is provided by a plugin rather than built in to the server

Click **Configure →** on a card to open the integration page.

## Configure a gateway

<Steps>
<li>

Open the integration page with **Configure →**.

</li>
<li>

Fill in each field. The form label reads "Enter {label}" for text fields and "Choose {label}" for selects. Required fields must not be empty before the gateway will connect.

</li>
<li>

Click **Save Changes**. The banner reads "Settings saved." Secrets are stored encrypted and shown blank on every subsequent visit.

</li>
<li>

If the gateway has a **Test Connection** button, click it and confirm the **Connection Status** panel reads "Connection OK" before enabling. A failed test shows "Connection Failed" with an endpoint and round-trip detail.

</li>
<li>

Toggle **Enabled** on. The card on the Integrations page now shows **Active**.

</li>
</Steps>

## Gateway settings

<Callout type="tip">
Use the gateway's test mode or test address before switching to live. For the card gateway, set Mode to Test or Sandbox. For the bank gateway, point Gateway base URL at the bank's test address.
</Callout>

### Bundled card gateway

| Key | Label | Type | Required | Notes |
|-----|-------|------|----------|-------|
| `secret_key` | Secret Key | Secret | Yes | Never shown again after saving |
| `publishable_key` | Publishable Key | Text | Yes | Used by the client portal's card form |
| `mode` | Mode | Select | No | Live or Test |

The client portal's card form also needs `STRIPE_PUBLISHABLE_KEY` set in the deployment environment (see [Configuration reference](/operators/configuration)). This is the same key as `publishable_key` above.

### Bundled bank gateway

The bank gateway is installed as a plugin and configured on its integration page.

| Key | Label | Type | Required | Notes |
|-----|-------|------|----------|-------|
| `gateway_url` | Gateway base URL | Text | Yes | e.g. `https://pg.example.am` — use the bank's test address to run test payments |
| `username` | API Username | Text | Yes | |
| `password` | API Password | Secret | Yes | |
| `currency` | Currency (ISO 4217 numeric) | Text | No | Default: `051` |
| `language` | Payment page language | Text | No | Default: `hy` |

There is no test mode switch. Point `gateway_url` at the bank's test address to run test payments.

### Bank Transfer

| Key | Label | Type | Required | Notes |
|-----|-------|------|----------|-------|
| `account_name` | Account Name | Text | No | Shown to the client on the payment page |
| `iban` | IBAN | Text | No | |
| `bank_name` | Bank Name | Text | No | |
| `instructions` | Payment Instructions | Text Area | No | Free text shown below the account details |

All fields are optional. An operator can leave them all blank and record payments manually without showing any instructions.

## Currencies and gateways

A gateway charges only in the currencies it supports. When a client tries to pay an invoice whose billing currency the selected gateway does not accept, the payment is refused with `GATEWAY_CURRENCY_MISMATCH`. Configure the gateway in the right currency, or add a currency that the gateway supports. See [Currencies, renewals and jobs](/staff/currencies).

## What customers see

Clients choose their payment method on the checkout page ("Pay by card", "Pay by bank transfer") and on the invoice pay page. After a card payment they land on the payment result page. See the [client portal guide](/customers/portal) for the full flow.

## Screens that are not built yet

- **List Disputes** — disputes will be listed here once a payment gateway reports them (coming soon).
- **Offline Credit Card Processing** — offline card processing will be available here (coming soon).
- **Direct Debit Processing** — a read-only report page; no actions are available.
