---
title: Invoices and paying
---

# Invoices and paying

## Your invoices

<!-- See it in the demo: link goes here (#615) -->

Go to **Invoices**. The page has three tabs: **All**, **Unpaid** and **Paid**. Click an invoice to open it.

An invoice page shows: **Issued**, **Due Date**, **Date Paid**, **Subtotal**, **Tax**, **Credit Applied** and **Total**.

### Invoice statuses

| Status | What it means |
|--------|--------------|
| Unpaid | Payment is due |
| Overdue | The due date has passed and the invoice is not paid |
| Payment Pending | A payment is being processed |
| Paid | Paid in full |
| Cancelled | The invoice was cancelled and no payment is needed |
| Refunded | A refund was issued |
| Collections | The invoice has been sent to collections |

Draft invoices are not shown to customers.

## Pay an invoice

<!-- See it in the demo: link goes here (#615) -->

<Steps>
<li>

Open the invoice and click **Pay Now**.

</li>
<li>

The **Make Payment** page opens. Choose a **Payment Method**.

</li>
<li>

Click **Submit Payment**. For card payments you are taken to your provider's secure payment page.

</li>
</Steps>

If the invoice is only partly paid, the page shows **Balance Due** (amount still owed) and **Payments to Date** (amount already paid).

## After paying

The payment result page shows one of the following:

| Result | What it means |
|--------|--------------|
| **Payment successful** | Payment confirmed. |
| **Payment is being confirmed** | The gateway has not responded yet. Click **Check again** to refresh. |
| **Payment was not completed** | No money was taken. Click **Try paying again**. |
| **We couldn't confirm your payment** | Do not pay again yet. Contact support to check what happened. |
| **No payment to check** | Nothing to show — you may have arrived here by mistake. |

## Download a PDF

Open an invoice and click **Download PDF** to save a copy.

## Why was my service suspended?

An unpaid renewal invoice can suspend your service after a few days. Paying it brings the service back.

The service page shows the reason for suspension:

- "Suspended by our staff."
- "Suspended because an invoice is overdue."
- "Suspended because the domain has expired."

<Callout type="tip">
Paying the overdue invoice brings a suspended service back automatically.
</Callout>

| Error | What it means |
|-------|--------------|
| `INVOICE_NOT_PAYABLE` | This invoice cannot be paid in its current status. |
| `CLIENT_CARD_PAYMENT_DISABLED` | Card payments are disabled for this account. Pay by another method or contact support. |
| `GATEWAY_SESSION_ALREADY_STARTED` | A payment attempt is already in progress for this invoice. Please wait or complete the existing payment. |
| `GATEWAY_CHARGE_UPSTREAM_FAILED` | The payment gateway declined the charge. |
| `RENEWAL_INVOICE_OPEN` | A renewal invoice for this domain cannot be released right now. It is overdue together with other items, or a payment on it is in progress. Settle that invoice first, then renew. |
