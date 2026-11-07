---
title: Transactions, billable items and quotes
description: "Review transactions in Hostpanel and manage billable items, client quotes, cancellation requests and the client statement."
keywords: "transactions, billable items, quotes, cancellation requests, client statement"
---

# Transactions, billable items and quotes <Label type="admin" />

## Transactions

Go to **Transactions** to see every financial event recorded in the system.

**Show filter:** All Activity, Payments Received, Payments Sent.

The top of the screen shows **summary totals** for the selected period: total received, total sent, and net.

A **Manual** badge marks transactions that were recorded by staff rather than processed through a gateway.

### Add a transaction

Click **Add Transaction** to record a payment or payout that happened outside of an invoice workflow.

| Field | Notes |
|-------|-------|
| Related Client | The client this transaction belongs to |
| Amount In | Money received |
| Amount Out | Money sent |
| Fees | Gateway or processing fees |
| Transaction ID | Reference from your bank or gateway |
| Payment Method | How the money moved |
| Invoice IDs (Comma Separated) | Link the transaction to one or more invoices |
| Add to Client's Credit Balance | Tick to add any net amount as credit |

A transaction must have an amount in or an amount out (`TRANSACTION_AMOUNT_REQUIRED`).

A transaction ticked **Add to Client's Credit Balance** is also a line of the client's credit history (see the **Credit** tab of the client). It can no longer be edited or deleted (`TRANSACTION_CREDIT_LOCKED`): correct the balance with an adjustment on the Credit tab. Entries from before the credit history existed stay editable and deletable, and deleting one undoes its credit move. This list shows entries that moved money; credit applied to an invoice or adjusted by staff moves none and appears on the Credit tab only.

## Billable items

Go to **Billable Items** to see items that have been queued for invoicing.

**Statuses:**

| Status | Meaning |
|--------|---------|
| Uninvoiced | Waiting to be placed on an invoice by the daily job |
| Invoiced | Already placed on an invoice |
| Recurring | Set to recur; not yet active |
| Active Recurring | Currently generating recurring charges |
| Completed Recurring | Recurring cycle has ended |

### Add a billable item

From a client's page, click **Add Billable Item**.

| Field | Notes |
|-------|-------|
| Client | The client to charge |
| Description | Line item text that appears on the invoice |
| Amount | Charge amount |
| Currency | Invoice currency |
| Type | One Time or Recurring |
| Invoice on | Date to invoice the item |
| Recurring Period | Weekly, Monthly, Quarterly, or Annually (Recurring only) |

Billable items are placed on invoices by the daily billable items job. An item that has already been placed on an invoice cannot be changed (`BILLABLE_ITEM_INVOICED`).

## Quotes

Go to **Quotes** to see all quotes. Quotes let you send a priced proposal to a client and convert it to an invoice when accepted.

**Stages:** Draft, Delivered, On Hold, Accepted, Lost, Expired, Dead.

### Create a quote

Click **Create Quote**.

<Steps>
<li>

Fill in **Subject**, **Stage**, **Client**, and **Valid Until**.

</li>
<li>

Add **Proposal Text** (shown to the client), **Customer Notes**, and **Admin Only Notes**.

</li>
<li>

Add line items: **Qty**, **Description**, **Unit Price**, **Discount %**, **Taxed**.

</li>
<li>

Click **Save**.

</li>
</Steps>

A quote can only be edited while it is in a stage that permits editing (`QUOTE_NOT_EDITABLE`). Only a quote in an appropriate stage can be accepted (`QUOTE_NOT_ACCEPTABLE`).

### Duplicate and convert

- **Duplicate** — creates a copy of the quote in Draft stage.
- **Convert to Invoice** — creates an invoice from the quote's line items. The quote itself stays unchanged.

## Cancellation requests

Go to **Cancellation Requests** to see requests submitted by clients.

Each request shows the service, the client, and the type: **Immediate** (cancel now) or **End of Billing Period** (cancel at the end of the current term).

Staff act on the service record itself; this screen only lists and deletes requests.

## Client statement

Go to **Reports → Client Account Register Balance** to see a full transaction history and running balance for a client.
