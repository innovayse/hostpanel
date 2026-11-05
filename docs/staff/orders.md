---
title: Orders
description: "View and process orders in Hostpanel: the order list, what happens when an order is paid, manual acceptance, details, cancelling and order numbers."
keywords: "orders, order list, accept order, order numbers"
---

# Orders <Label type="admin" />

## The order list

Go to **Orders** to see all orders across all clients.

**Columns:** Order number, Client, Date, Total, Status, and action buttons.

**Status filter:** Pending, Active, Cancelled, Fraud.

**Row actions:** **Accept** (Pending only), **Cancel** (Pending only), **View**. Attempting to accept or cancel an order that is not Pending returns `ORDER_NOT_PENDING`.

## What happens when an order is paid

When a client completes payment for an order:

1. The order moves from Pending to Active.
2. Each order line becomes a service in Pending status and, where applicable, a domain.
3. The provisioning system picks up Pending services and provisions them automatically. See [Provisioning](/servers/provisioning) for how hosting accounts are set up.

## Accept an order by hand

Open the order and click **Accept**. If some lines could not be fulfilled automatically, the panel shows: "Order accepted, but these lines could not be fulfilled: {lines}." Go to those service records and provision them manually.

## Order details

| Field | Description |
|-------|-------------|
| Order Info | Order number, client, date, status, total |
| Invoice | Link to the invoice for this order |
| Admin Notes | Staff-only notes |

**Line columns:** Product, Billing Cycle, Domain, First Payment, Recurring.

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
