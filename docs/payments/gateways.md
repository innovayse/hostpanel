---
title: Payment gateways
description: "Configure payment methods in Hostpanel: bank transfer, payment gateway plugins, gateway settings, currencies and what customers see at checkout."
keywords: "payment gateways, bank transfer, gateway settings, currencies, checkout"
---

# Payment gateways <Label type="admin" />

<DemoLink path="/integrations/payments" />

Hostpanel ships with one payment method, bank transfer. Every online payment gateway comes from a plugin that you install. No payment gateway is enabled by default; an operator enables each one after entering their credentials.

## Payment methods

| Method | How it works | When to use |
|--------|-------------|-------------|
| **Bank Transfer** | Built in; offline. The client sees your bank details and a reference, and staff confirm or reject the transfer in **Billing**, **Pending Bank Transfers** | Markets where bank transfer is the norm, or as a fallback |
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
| `swift` | SWIFT / BIC | Text | No | For clients paying from abroad |
| `bank_name` | Bank Name | Text | No | |
| `instructions` | Payment Instructions | Text Area | No | Free text shown below the account details |

All fields are optional. A detail left blank is not shown to the client.

#### How a bank transfer is paid

1. The client chooses **Pay by bank transfer** on the checkout or the invoice page. The invoice moves to **Payment Pending**, and the page shows your bank details with a copy button for each, the amount, and a reference built from the invoice number (for example `INV-123`). The client gets the same details by email and you get a notice.
2. When the money reaches your bank, open **Billing**, **Pending Bank Transfers** and press **Confirm**. The invoice is paid exactly like any payment you record by hand: the receipt email goes out, the payment is written to the ledger and the order is fulfilled. Pressing Confirm twice, or from two screens at once, pays the invoice once.
3. If the money did not arrive, press **Reject** and write a note. The invoice is open again, the client reads your note by email and can pay again, by bank transfer or another method.

A transfer waits only while the invoice is Payment Pending; the invoice cannot be cancelled meanwhile, so decide the transfer first. Attaching a receipt to a transfer is not supported.

## Currencies and gateways

A gateway charges only in the currencies it supports. When a client tries to pay an invoice whose billing currency the selected gateway does not accept, the payment is refused with `GATEWAY_CURRENCY_MISMATCH`. Configure the gateway in the right currency, or add a currency that the gateway supports. See [Currencies, renewals and jobs](/staff/currencies).

## What customers see

Clients choose their payment method on the checkout page (for example "Pay by bank transfer") and on the invoice pay page. After an online payment they land on the payment result page. See the [client portal guide](/customers/portal) for the full flow.

## Screens that are not built yet

- **List Disputes** — disputes will be listed here once a payment gateway reports them (coming soon).
- **Offline Credit Card Processing** — offline card processing will be available here (coming soon).
- **Direct Debit Processing** — a read-only report page; no actions are available.
