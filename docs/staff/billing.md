---
title: Invoices and payments
description: "Create and edit Hostpanel invoices, record payments, mark paid or unpaid, apply credit, refund, cancel and download PDFs."
keywords: "invoices, record payment, credit, refund, invoice pdf, invoice statuses"
---

# Invoices and payments <Label type="admin" />

<DemoLink path="/billing/invoices" />

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
Credit is applied automatically to renewal invoices when the <code>billing.renewal.auto_apply_credit</code> setting is enabled. It is applied to renewal invoices only; every other invoice takes credit when staff or the client apply it.
</Callout>

A client can also apply credit to their own unpaid invoice from the invoice page. Credit that covers everything the invoice owes settles it, and its order is fulfilled as for any other payment.

### The Credit tab

Every client has a **Credit** tab on their profile. It shows the balance and the credit history, newest first. Each line says why the balance moved, by how much (positive adds, negative takes) and what the balance was left at, and links the invoice it concerned:

- credit added or deducted by staff, with the reason staff gave;
- credit applied to an invoice, by staff, by the client or by the renewal sweep, and credit returned from an invoice when it is removed, cancelled or lowered;
- a refund paid back as credit;
- a paid top-up, and a refund of one;
- a payment that took more than the invoice owed (or could not be recorded on its invoice) and went to the balance;
- a legacy ledger entry that was deleted and its credit move undone.
- a credit note given as account credit, and a credit note voided (see Credit notes below).

The history is the client ledger itself. An entry that holds the money (a payment, a refund, a transaction added to credit) carries the movement. A movement that moved no cash (credit applied to an invoice, a staff adjustment) is a ledger line with no money in or out; those lines stay out of the transaction list, the transaction reports and the data export. Movements from before the history existed are not itemised; the balance is right regardless.

To correct the balance, use **Add credit** or **Deduct credit** on the tab. A reason is required and is kept on the history line (the client does not see it); the client is emailed the change and the new balance. A deduction never takes more than the balance holds (`CLIENT_CREDIT_INSUFFICIENT`). A ledger entry that is part of the history cannot be edited or deleted (`TRANSACTION_CREDIT_LOCKED`): correct it with a new adjustment.

### Adding funds (top-ups)

A client adds funds from **Credit, Add funds** in the client area. They choose an amount between two limits you set in System Settings, in their billing currency:

- `billing.credit.topup_min`: the least one top-up may add (default 5);
- `billing.credit.topup_max`: the most one top-up may add (default 1000).

A top-up is an invoice with one fixed line for the amount and no tax, which the client pays through the ordinary payment page: a bank transfer you confirm, or an installed payment gateway. Its amount is added to the balance in the same step that marks the invoice paid, once, however many times the payment is confirmed or retried. The client is emailed the new balance.

A top-up is not an order: paying it fulfils nothing and renews nothing. Its cash counts as income once, when it is paid; credit later spent on another invoice adds no income of its own. Credit cannot be applied to a top-up invoice (`INVOICE_TOP_UP_NO_CREDIT`), and its line, tax rate and amount cannot be edited or duplicated (`INVOICE_TOP_UP_FIXED`). An unpaid top-up never turns overdue and no reminder is sent for it; you can cancel it. A paid top-up cannot be cancelled.

A paid top-up is refunded by hand only, as a **Manual** refund (`INVOICE_TOP_UP_REFUND_MANUAL_ONLY` for a gateway refund or a refund to credit). The refunded amount is taken off the client's balance in the same step. If the client has already spent the credit and the balance is short, the refund is refused whole (`CLIENT_CREDIT_INSUFFICIENT`) and nothing is recorded; refund what is left, or recover the rest outside the system.

## Refund

Open the **Refund** tab on a paid or partially refunded invoice.

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

## Credit notes

A credit note reverses part or all of an invoice that was already paid. It is a numbered document the client receives, and it is **not the same as invoice credit**: invoice credit is balance a client spends on an invoice (the Credit tab above), a credit note is issued against a paid invoice and gives value back.

<Callout type="warning">
The numbering is a cautious default and has not been checked against any law. Credit notes are numbered `CN-0001`, `CN-0002` and so on from their own counter, and the PDF carries the seller details you set in System Settings. Before you rely on them, check with an accountant or your local authority what your jurisdiction requires of the numbering, the content, the retention and the delivery of such a document. The panel does not connect to any tax authority.
</Callout>

### Issue a credit note

Open the **Credit notes** tab of a paid or partially refunded invoice and click **Issue credit note**.

<Steps>
<li>

Enter the **lines** credited: a description, a price before tax and a quantity. **Use the invoice lines** copies the invoice's own lines, which credits the whole invoice.

</li>
<li>

Choose where the value goes:
- **Account credit** puts the total on the client's credit balance. It shows in the credit history as a credit note, and the client can spend it on invoices.
- **Gateway refund** pays the total back through the gateway that took the money. It is one refund of the invoice, made by the same operation as **Refund**, so the money moves once and the refund is listed once in the invoice, the ledger and the reports.

</li>
<li>

Enter the **reason**. It is printed on the note, so the client reads it.

</li>
<li>

Click **Issue**. The note gets its number, the value goes back, and the client is emailed the note.

</li>
</Steps>

The tax on the note follows the invoice at the invoice's own rate. The sub total, the tax and the total are shown as you type.

How much can be credited: the invoice's charge before credit (sub total plus tax) less every refund and every earlier credit note given as account credit. A note above that is refused (`CREDIT_NOTE_EXCEEDS_CREDITABLE`); the same limit holds when two staff members issue notes for one invoice at the same moment, and the second is refused or waits for the first. A gateway refund note is also held to the money the gateway took and can still return (`INVOICE_REFUND_EXCEEDS_PAID`), and the refund is refused as it is anywhere else when the gateway declines it (`GATEWAY_REFUND_REFUSED`); no note is created then.

A credit note given as account credit also lowers what the invoice can still be refunded in cash, so the same money cannot go back twice. A refund made on its own, with **Refund**, does not create a credit note; issue a gateway refund note instead when you need the document.

A credit top-up invoice takes no credit note (`CREDIT_NOTE_TOP_UP_INVOICE`): it is credit already, and a paid top-up is refunded by hand. An invoice that is not paid takes none either (`INVOICE_NOT_PAID`).

### Numbers, gaps and retention

A number is taken as the last step before the note is saved, once every check has passed, and a save that fails is retried under the same number. The counter is a database sequence, which hands a value out for good, so it **cannot promise there are no gaps**: a server that stops between taking a number and saving the note, or a note that is refused only after the invoice changed under it, leaves its number unused. If your jurisdiction requires gap-free numbering, check that against how you operate before relying on it.

An issued credit note is never deleted. It can only be voided, and a voided note keeps its number and stays in the list and in the client's list, stamped **VOIDED** on its PDF.

### List, open, void and PDF

**Billing, Credit Notes** lists every note, newest first, with a status filter. Open a note to see its lines, its totals, the refund reference of a gateway refund note and, for a voided one, why it was voided.

- **Void credit note** asks for a reason. For a note given as account credit, the value comes off the client's balance and off the invoice's credited amount, so the invoice can be credited or refunded again. If the client has already spent the credit and the balance is short, the void is refused whole (`CLIENT_CREDIT_INSUFFICIENT`).
- A note paid out as a gateway refund cannot be voided (`CREDIT_NOTE_REFUND_NOT_VOIDABLE`): the refund cannot be taken back.
- **Download PDF** prints the note in the client's language (English, Russian or Armenian), with the seller from System Settings. A note over the line limit (`InvoicePdf__MaxLines`) cannot be rendered (`INVOICE_PDF_EXCEEDS_LIMIT`).

### Reports

Credit notes add no income of their own. A note given as account credit leaves the income as the payment made it (the cash stays taken; the credit is a balance, and credit spent later on another invoice adds no income). A gateway refund note counts its refund once, as the refund. The credit-only history line a note writes stays out of the transaction list and the transaction reports like every other balance movement.

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
