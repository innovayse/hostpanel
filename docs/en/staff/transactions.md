---
title: Transactions, billable items and quotes
description: "Review transactions in Hostpanel and manage billable items, client quotes, cancellation requests and the client statement."
keywords: "transactions, billable items, quotes, cancellation requests, client statement"
---

# Transactions, billable items and quotes <Label type="admin" />

## Transactions

Go to **Transactions** to see every financial event recorded in the system.

The list is paged by the server, so every entry is reachable however many there are. Type in the search box to find an entry by its description, its reference, its payment method or its id; a `%` or `_` you type is an ordinary character. Client names cannot be typed into the search box, so choose the client from the list instead. The **Filters** button opens these fields, each shown as a removable chip once applied:

| Filter | Meaning |
|--------|---------|
| Client | Only the entries of one client, chosen from the client picker |
| Show | All Activity, Payments Received (money in) or Payments Sent (money out) |
| Payment Method | Only the entries of exactly that payment method |
| Date Range | The first and the last day; the last day is included to its end |
| Amount | Only the entries whose amount in or amount out is exactly that amount |

The top of the screen shows **summary totals** of the entries the search and the filters keep, not of the whole ledger: total received, total sent, fees, and net, in the base currency. Each currency is totalled on its own before it is converted. The totals and the count agree with the rows you can page through.

The search, the filters and the page stay in the address, so a reload or a shared link shows the same list. A page size over 100, a search longer than the limit, an unknown direction, an amount that is not above zero or a client id that is not positive is refused instead of cut. The **Transactions** tab of a client has the same search, direction, payment method, date range and amount filters.

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

Go to **Billable Items** to see items that have been queued for invoicing. The list is paged by the server, so every item is reachable however many there are.

Type in the search box to find an item by its description or its id. The **Filters** button opens three fields, each shown as a removable chip once applied:

| Filter | Meaning |
|--------|---------|
| Client | Only the items of one client, chosen from the client picker. Client names cannot be typed into the search box |
| Type | One Time or Recurring |
| Invoiced | Yes for items already placed on an invoice, No for items still waiting for the daily job |

Type and Invoiced combine: Recurring with Invoiced No shows the recurring items that are active, Recurring with Invoiced Yes the ones whose cycle has ended.

The **Billable Items** tab of a client has the same search and the Type filter, and they narrow both the uninvoiced and the invoiced list; only the invoiced list is paged. The uninvoiced total follows the filter.

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

The list is paged by the server. Type in the search box to find a quote by its subject or its quote number; a `%` or `_` you type is an ordinary character. Client names cannot be typed into the search box, so choose the client from the list instead. The **Filters** button opens these fields, each shown as a removable chip once applied:

| Filter | Meaning |
|--------|---------|
| Client | Only the quotes of one client, chosen from the client picker |
| Stage | Only the quotes in that stage |
| Validity Period | Valid for quotes whose Valid Until date has not passed, Expired for the ones whose date has passed. This looks at the date alone, so a quote can be in the Expired stage with a date still ahead |

The search, the filters and the page stay in the address, and the server applies them before it pages. The **Quotes** tab of a client has the same search, stage and validity filters.

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

- **Duplicate**: creates a copy of the quote in Draft stage.
- **Convert to Invoice**: creates an invoice from the quote's line items. The quote itself stays unchanged.

## Cancellation requests

Go to **Cancellation Requests** to see requests submitted by clients.

Each request shows the service, the client, and the type: **Immediate** (cancel now) or **End of Billing Period** (cancel at the end of the current term).

Type in the search box to find a request by its reason or its id. The **Filters** button opens the Type and Status (Open or Closed) fields, shown as removable chips once applied. The product and the client name cannot be searched.

Staff act on the service record itself; this screen only lists and deletes requests.

## Client statement

Go to **Reports → Client Account Register Balance** to see a full transaction history and running balance for a client.
