---
title: Invoices and paying
description: "View and pay Hostpanel invoices, read invoice statuses, download a PDF and find out why a service was suspended."
keywords: "client invoices, pay invoice, invoice pdf, service suspended"
---

# Invoices and paying

## Your invoices

<DemoLink clientPath="/client/invoices" />

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
| Partially Refunded | Part of the payment has been refunded |
| Collections | The invoice has been sent to collections |

Draft invoices are not shown to customers.

## Pay an invoice

<DemoLink clientPath="/client/invoices" />

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

## Your credit

Go to **Credit** to see your balance and every movement of it: when it moved, why, which invoice it concerned, how much it changed and what the balance was afterwards. Movements from before the history was kept are not listed; your balance is right regardless.

### Add funds

Click **Add funds**, enter an amount between the two limits shown and continue to payment. Adding funds creates an invoice for that amount, which you pay like any other invoice (by card through an enabled gateway, or by bank transfer). The funds are added to your balance when the payment is confirmed, not before, and you are emailed your new balance. A top-up invoice takes no credit, and it can be refunded by the hosting provider only by hand.

### Pay an invoice with credit

On an unpaid invoice, **Pay with credit** lets you apply part or all of your balance to it. Credit that covers everything the invoice owes pays it. Credit is applied automatically to renewal invoices when the provider has switched that on.

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

