---
title: Orders
description: "View and process orders in Hostpanel: the order list, what happens when an order is paid, manual acceptance, details, cancelling and order numbers."
keywords: "orders, order list, accept order, order numbers"
---

# Orders <Label type="admin" />

## The order list

Go to **Orders** to see all orders across all clients.

**Columns:** Order number, Client, Date, Total, Status, and action buttons.

**Status filter:** Pending, Active, Incomplete, Cancelled, Fraud. Incomplete is an accepted order with at least one line that failed to be fulfilled; the list shows how many lines failed, and filtering by Incomplete gives the orders that still need attention.

**Row actions:** **Accept** (Pending only), **Cancel** (Pending only), **View**. Attempting to accept or cancel an order that is not Pending returns `ORDER_NOT_PENDING`.

## What happens when an order is paid

When a client completes payment for an order:

1. The order moves from Pending to Active.
2. Each order line becomes a service in Pending status and, where applicable, a domain.
3. The provisioning system picks up Pending services and provisions them automatically. See [Provisioning](/servers/provisioning) for how hosting accounts are set up.

## Accept an order by hand

Open the order and click **Accept**. If some lines could not be fulfilled automatically, the panel shows: "Order accepted, but these lines could not be fulfilled: {lines}." The order is then shown as **Incomplete** until every line is fulfilled; retry the failed lines as described below.

## Retry a failed line

Open an Incomplete order. The **Fulfilment** column shows the state of every line: Not run yet, Running, Fulfilled, Failed or Refunded. A failed line shows the code the last attempt ended with, how many attempts were made and when the last one was.

Click **Retry** on a failed line to run its fulfilment again. If it succeeds the line becomes Fulfilled, and when no failed line is left the order returns to Active. If it fails again the line stays Failed with the new code.

- A line that is already fulfilled is never fulfilled again (`ORDER_LINE_ALREADY_FULFILLED`).
- A domain registration or transfer that the client already holds under that name is counted as done, so a domain is never registered twice.
- A line whose cost was refunded after the registrar rejected its domain is Refunded and cannot be retried.
- Only one retry of a line runs at a time; a second one is refused with `ORDER_LINE_NOT_RETRYABLE`. A retry that died without finishing can be started again after ten minutes.
- Every retry is written to the client's activity log.

## Order details

| Field | Description |
|-------|-------------|
| Order Info | Order number, client, date, status, total |
| Invoice | Link to the invoice for this order |
| Admin Notes | Staff-only notes |

**Line columns:** Product, Billing Cycle, Domain, First Payment, Recurring, Status, Fulfilment.

## Cancel and delete

- **Cancel this order?** — cancels a Pending order only (`ORDER_NOT_PENDING` if the order is not Pending). Cancelling also cancels the linked invoice. If the invoice already has a recorded payment, the cancel is refused (`INVOICE_HAS_PAYMENTS`) — refund or delete the payment first.
- **Delete Order** — available only for Pending or Cancelled orders (`ORDER_NOT_DELETABLE`). Deleting removes the order record permanently.

## Order numbers

Orders are numbered in the format `ORD-0042`. The prefix and digit count are set in the configuration:

| Setting | Default | Description |
|---------|---------|-------------|
| `Orders__NumberPrefix` | ORD- | The letters before the order number |
| `Orders__NumberDigits` | 4 | How many digits to pad the number to |
| `Orders__InvoicePaymentTermDays` | 7 | Days until the order invoice is due |

See the [Configuration reference](/operators/configuration) for how to change these.
