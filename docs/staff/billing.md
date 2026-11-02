---
title: Invoices and payments
---

# Invoices and payments <Label type="admin" />

Opening an invoice that does not exist returns `INVOICE_NOT_FOUND`.

## Invoice statuses

An invoice moves through these statuses during its life:

| Status | What puts it here |
|--------|-------------------|
| Draft | Created with "Create as Draft" |
| Unpaid | Published; payment is expected |
| Overdue | Due date passed without full payment |
| Paid | Payments cover the invoice total |
| Partially Refunded | A partial refund has been applied; the remaining paid amount can still be refunded |
| Refunded | Refunds cover everything that was paid |
| Cancelled | Voided by staff |
| Collections | Sent to a collections agency |
| Payment Pending | An online payment is awaiting confirmation from the gateway |

<Callout type="note">
The invoice list status filter shows Draft, Unpaid, Overdue, Paid, Partially Refunded, Refunded, and Cancelled only. Collections and Payment Pending are not included in the filter dropdown.
</Callout>

## The invoice list

Go to **Invoices** to see all invoices. Use **Search/Filter** to narrow by client, status, date range, or amount.

**Bulk actions (With Selected:):** Mark Paid, Mark Unpaid, Mark Cancelled, Duplicate Invoice, Delete.

## Create an invoice

Open a client, go to their **Invoices** tab, and click **+ Create Invoice**.

<Steps>
<li>

Set the **Due Date**.

</li>
<li>

Tick **Create as Draft** if the invoice should not be sent to the client yet.

</li>
<li>

Add line items: description, quantity, unit price, tax rate.

</li>
<li>

Click **Create Invoice**.

</li>
<li>

If you created a draft, click **Publish** in the draft banner when it is ready to send. Only a draft invoice can be published (`INVOICE_NOT_DRAFT`).

</li>
</Steps>

## Edit lines

On an open invoice, find **Invoice Items**:

- **Add Item** — add a new line.
- **Remove line** — remove a single line.
- **With Selected: Delete** — remove multiple lines at once.
- **Save Changes** / **Cancel Changes** — confirm or discard edits.

An invoice cannot be edited if it has been paid, cancelled, or refunded (`INVOICE_NOT_EDITABLE`). Reducing the total below what has already been paid is also refused (`INVOICE_TOTAL_BELOW_PAID`).

## Options

On the **Options** tab you can change:

- **Invoice Date** and **Due Date**
- **Payment Method**
- **Tax Rate** — cannot be set on a tax-exempt client's invoice (`CLIENT_TAX_EXEMPT`)

## Record a payment

On an unpaid or overdue invoice, open the **Add Payment** tab.

<Steps>
<li>

Enter the **Date**, **Amount**, **Payment Method**, **Transaction Fees** (optional), and **Transaction ID** (optional).

</li>
<li>

Click **Add Payment**.

</li>
</Steps>

A partial payment (less than the amount due) leaves the invoice open with a lower balance. The invoice becomes Paid once payments cover the total in full.

Overpayment is refused (`INVOICE_PAYMENT_EXCEEDS_DUE`).

The Add Payment tab is blocked when:
- The invoice has not been published yet ("Publish the invoice before adding payments.")
- The invoice is cancelled or already paid (`INVOICE_NOT_PAYABLE`)

## Mark Paid

**Mark Paid** closes the invoice without recording a payment. It is only available when nothing is owed. If money is still due, the panel shows `INVOICE_AMOUNT_STILL_DUE`.

<Callout type="note">
Mark Paid does not record a payment transaction. Use Add Payment to keep an accurate payment record.
</Callout>

## Mark Unpaid

**Mark Unpaid** reopens a Paid invoice. It is only available when the invoice is Paid and has no recorded payments (`INVOICE_HAS_PAYMENTS` if a payment is on record — remove the payment first).

## Credit

The client's credit balance is shown on their Summary page.

- **Add Credit to Invoice** — applies credit from the client's balance toward this invoice.
- **Remove Credit from Invoice** — returns the credit to the client's balance.

Limits:
- Cannot apply more credit than the client's balance (`CLIENT_CREDIT_INSUFFICIENT`).
- Cannot apply more credit than the invoice still owes (`INVOICE_CREDIT_EXCEEDS_DUE`).
- Credit can only be applied to Draft, Unpaid, or Overdue invoices.

<Callout type="tip">
Credit is applied automatically to renewal invoices when the <code>billing.renewal.auto_apply_credit</code> setting is enabled.
</Callout>

## Refund

Open the **Refund** tab on a paid invoice.

<Steps>
<li>

Select the **transaction** to refund.

</li>
<li>

Enter the **amount** (partial refunds are allowed).

</li>
<li>

Choose a **Refund Type**:
- **Refund through Gateway** — sends the refund back through the original payment gateway.
- **Manual Refund Processed Externally** — records a refund that you have already processed outside the system.
- **Add to Client's Credit Balance** — credits the amount to the client's account.

</li>
<li>

Click **Refund**.

</li>
</Steps>

A partial refund moves the invoice to **Partially Refunded**. A Partially Refunded invoice can be refunded again up to the remaining paid amount. The invoice moves to Refunded once refunds cover everything that was paid.

Only one refund at a time per invoice. If another refund is still waiting on a gateway response, the panel shows `INVOICE_REFUND_CONFLICT`.

If the invoice was never paid through a gateway, "Refund through Gateway" is refused (`INVOICE_NOT_GATEWAY_PAID`). If the gateway declines the refund, the panel shows `GATEWAY_REFUND_REFUSED`.

If credit was applied toward the payment being refunded, a WARNING panel appears after the refund explaining how much credit was involved.

<Callout type="warning">
A refund through the gateway cannot be undone. Confirm the amount carefully before submitting.
</Callout>

Refund limits:
- Cannot refund more than what was paid and not yet refunded (`INVOICE_REFUND_EXCEEDS_PAID`).

## Cancel and delete

- **Mark Cancelled** — cancels an unpaid invoice. Cannot cancel a paid, refunded, or already-cancelled invoice (`INVOICE_CANCEL_NOT_ALLOWED`). Cannot cancel an invoice that has a payment; refund or delete the payment first (`INVOICE_HAS_PAYMENTS`).
- **Delete** — permanently removes the invoice. Only Draft or Cancelled invoices can be deleted (`INVOICE_NOT_DELETABLE`).

## PDF

Click **Download PDF** or **Print** on the invoice page to generate a PDF.

- Draft invoices are stamped **DRAFT** and are only accessible by staff.
- Invoices with more than 500 lines cannot be downloaded as a PDF (`INVOICE_PDF_EXCEEDS_LIMIT`). The limit is set by `InvoicePdf__MaxLines` in the configuration.

<Callout type="note">
The Pdf Batch screen is not available yet and is not documented here.
</Callout>

## Fulfilment

The **Fulfilment** tab on an invoice shows the fulfilment status of any digital product lines — whether the fulfilment plugin has delivered the product, is retrying, or has failed.

## Gateway payments

When a client pays online, the payment appears automatically on the invoice once the gateway confirms it. To see the raw gateway events, go to **Billing → Gateway Log**.

For details on configuring payment gateways, see [Payment gateways and plugins](/payments/gateways).
