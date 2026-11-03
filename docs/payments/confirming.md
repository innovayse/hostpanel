---
title: How online payments are confirmed
---

# How online payments are confirmed <Label type="admin" />

<Callout type="note">
Hostpanel does not use webhooks. There is nothing to register at the gateway side — all payment confirmation is done by polling.
</Callout>

## Hosted page payments

When a client pays with the card or bank gateway, they are redirected to the gateway's hosted page. After they complete (or abandon) the payment, the gateway sends them back to Hostpanel's payment result page. Hostpanel then queries the gateway for the final result.

Because the redirect can be interrupted (browser closed, network lost), Hostpanel does not rely solely on the return visit — the reconciliation job asks again on its own schedule.

## Card payments at checkout

For the bundled card gateway, the card form is embedded on the checkout page. The client enters their card details and Hostpanel tokenises the card directly with the gateway. After the client confirms the order, Hostpanel charges the token.

## Reconciliation

The `ReconcileGatewayPaymentsCronCommand` job runs every `ReconcileGatewayPaymentsIntervalMinutes` minutes (default **5**) and asks the gateway about every payment session that has not yet received a final answer.

The timing works as follows:

1. A payment session is created. Hostpanel waits `GatewaySessionSettleMinutes` (default **25 minutes**) before asking the gateway — this gives the client time to complete the hosted page flow without the job racing ahead.
2. After the first check, each subsequent check waits longer. The wait starts at `GatewayCheckBackoffStartMinutes` (default **5 minutes**) and doubles each time, up to `GatewayCheckMaxIntervalMinutes` (default **360 minutes** / 6 hours).
3. After `GatewayStalledAfterHours` (default **24 hours**) without a final answer, staff receive the `gateway-payment-stalled-admin` email **once**.

All thresholds are in the `ScheduledJobs` section of System Settings.

<SettingTable :settings="[
  { name: 'ScheduledJobs.ReconcileGatewayPaymentsIntervalMinutes', type: 'number', default: '5', description: 'How often (in minutes) the reconciliation job runs.' },
  { name: 'ScheduledJobs.GatewaySessionSettleMinutes', type: 'number', default: '25', description: 'Minutes to wait after a session is created before asking the gateway for the first time.' },
  { name: 'ScheduledJobs.GatewayCheckBackoffStartMinutes', type: 'number', default: '5', description: 'Minutes to wait after the first check. Each subsequent wait doubles.' },
  { name: 'ScheduledJobs.GatewayCheckMaxIntervalMinutes', type: 'number', default: '360', description: 'Maximum minutes between checks (6 hours).' },
  { name: 'ScheduledJobs.GatewayStalledAfterHours', type: 'number', default: '24', description: 'Hours after which a pending session is considered stalled and staff are notified once.' }
]" />

## Amount checks

When the gateway reports back, Hostpanel checks that the amount and currency match the invoice. A mismatch is refused with `GATEWAY_PAYMENT_MISMATCH` ("The payment gateway reported an amount or currency that does not match the invoice.") and the session is not settled.

## Refunds through a gateway

Online payments made through the card gateway can be refunded from the invoice detail page. See [Invoices and payments](/staff/billing). If the gateway refuses or fails the refund, the error is `GATEWAY_REFUND_REFUSED` ("The payment gateway refused or failed the refund. Nothing was returned through it.").

## The Gateway Transaction Log <Label type="admin" />

Go to **Billing → Gateway Transaction Log**. Subtitle: "View payment gateway transaction logs."

**Filters:** Date Range, Debug Data (search in the raw gateway response), Gateway, Result (Payment, Refund, Credit, Pending).

Each row shows the gateway, result, amount and date. Click a row to see the full debug data — useful for answering "did this payment arrive" when a client says they paid but the invoice is still open.

## Troubleshooting

| Error | What to check |
|-------|--------------|
| `GATEWAY_SESSION_ALREADY_STARTED` — "A payment attempt is already in progress for this invoice." | A concurrent checkout attempt is running. Wait a moment and try again. |
| `GATEWAY_SESSION_NOT_FOUND` — "This invoice has no gateway payment session to verify." | The session expired or was never created. Ask the client to start the payment again. |
| `GATEWAY_CHARGE_UPSTREAM_FAILED` — "The payment gateway declined the charge." | The gateway refused the card. The client should try a different card. |
| `GATEWAY_TRANSACTION_MISSING_UPSTREAM_FAILED` — "The payment gateway reported success but returned no transaction reference." | The gateway responded with success but gave no reference. Check the gateway log for the raw response. |
| `GATEWAY_PAYMENT_MISMATCH` — "The payment gateway reported an amount or currency that does not match the invoice." | The gateway settled for a different amount or currency. Check the gateway log debug data. |
| `GATEWAY_REFUND_REFUSED` — "The payment gateway refused or failed the refund." | The gateway did not accept the refund request. Check the gateway log and retry manually through the gateway dashboard. |
| `PAYMENT_PLUGIN_NOT_CONFIGURED` — "No payment plugin is configured for that invoice's gateway." | The plugin for this gateway is not installed or the integration has no credentials. |
| `PAYMENT_GATEWAY_SERVICE_UNAVAILABLE` — "The payment gateway is temporarily unavailable." | The gateway is down or unreachable. Try again later. |
| `PAYMENT_METHOD_UNAVAILABLE` — "That payment method is not available right now." | The selected gateway is disabled or not configured. |
| `PAYMENT_INTENT_MISMATCH` — "This card payment does not match the invoice for this order." | The card payment token belongs to a different order. Do not reuse a checkout session for multiple orders. |
| `PAYMENT_INTENT_ALREADY_USED` — "This card payment has already been recorded." | The payment was already applied to the invoice. Reload the invoice. |
| `GATEWAY_CURRENCY_MISMATCH` — "This payment method does not charge in the currency this invoice bills in." | Configure a gateway that supports the invoice currency, or change the invoice currency. |
