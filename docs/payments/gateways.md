---
title: Payment gateways
---

# Payment gateways <Label type="admin" />

<DemoLink path="/integrations/payments" />

Hostpanel ships with one payment method, bank transfer. Every online payment gateway comes from a plugin that you install. No payment gateway is enabled by default; an operator enables each one after entering their credentials.

## Payment methods

| Method | How it works | When to use |
|--------|-------------|-------------|
| **Bank Transfer** | Built in; offline. The client pays by bank transfer and staff record the payment manually | Markets where bank transfer is the norm, or as a fallback |
| **Gateway plugins** | Installed by the operator; the client is usually redirected to the gateway's hosted payment page | Accepting cards or local payment methods online |

<Callout type="note">
The gateways shown in your admin panel depend on which plugins are installed. To accept online payments, install a payment gateway plugin from the Plugin Manager, then configure it on its integration page. See [Plugin manager](./plugins).
</Callout>

## The Payment Gateways screen

Go to **Integrations**, category **Payment Gateways**. Bank Transfer and each installed gateway appear as a card showing:

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
Use the gateway's test mode or test address before switching to live. Each gateway plugin documents its own fields and test setup.
</Callout>

### Gateway plugins

An installed gateway plugin lists its own fields on its integration page. The form labels, required fields and the Test Connection button are defined by the plugin, so follow the plugin's own documentation for its settings.

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

Clients choose their payment method on the checkout page (for example "Pay by bank transfer") and on the invoice pay page. After an online payment they land on the payment result page. See the [client portal guide](/customers/portal) for the full flow.

## Screens that are not built yet

- **List Disputes** — disputes will be listed here once a payment gateway reports them (coming soon).
- **Offline Credit Card Processing** — offline card processing will be available here (coming soon).
- **Direct Debit Processing** — a read-only report page; no actions are available.
