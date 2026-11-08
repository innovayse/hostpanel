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

Open the order and click **Accept**. The moment the order is accepted every line is claimed and shows **Running**, and the order is shown as **Incomplete** while any line is not finished; it returns to Active when the last line is fulfilled. If some lines could not be fulfilled automatically, the panel shows: "Order accepted, but these lines could not be fulfilled: {lines}." Retry the failed lines as described below.

If the server stops in the middle of an accept, the lines that had not finished stay **Running** on an Incomplete order. They can be retried once their claim has run out (ten minutes by default, see below), so no line is forgotten as "Not run yet" on an Active order.

## Retry a failed line

Open an Incomplete order. The **Fulfilment** column shows the state of every line: Not run yet, Running, Fulfilled, Failed, Refunding, Partly refunded or Refunded. A failed line shows the code the last attempt ended with, how many attempts were made and when the last one was.

Click **Retry** on a failed line to run its fulfilment again. If it succeeds the line becomes Fulfilled, and when no failed line is left the order returns to Active. If it fails again the line stays Failed with the new code.

- A line that is already fulfilled is never fulfilled again (`ORDER_LINE_ALREADY_FULFILLED`).
- A service line is tied to the service it created. When a first attempt created the service but could not finish (a digital product that could not be activated, or a run that died before it recorded the line), the retry finishes that same service. It never orders a second one, so nothing is provisioned or billed twice.
- A domain registration or transfer that an earlier attempt of the same order already created under that name (the domain is stored with the order), active or still pending at the registrar, is counted as done, so a domain is never registered twice. A domain the client holds that no order created, and an expired, cancelled or transferred out domain, does not count.
- A domain renewal that an earlier attempt of the same line already sent to the registrar is counted as done, so the domain is never renewed twice for one payment. The line keeps this record for good: a renewal made another way afterwards (by staff, by the renewal job or on another order line) does not erase it.
- If the service stored for a line belongs to another client or product, the line fails with `ORDER_LINE_SERVICE_CONFLICT` instead of reusing it. Compare the line and the service by hand.
- An attempt that finishes after its claim was taken over by a newer attempt cannot overwrite the newer result: its outcome is refused with `ORDER_LINE_CHANGED` and the line keeps what the newer attempt recorded.
- An outcome that cannot be saved does not stop the other lines of the order. That line stays **Running** and can be retried once its claim has run out.
- A retry cancelled right after it started leaves the line Failed with `ORDER_LINE_INTERRUPTED`, so it can be retried at once.
- A registrar that times out, or any other unexpected error in one line, fails only that line with the code `HOSTPANEL_UNEXPECTED`. The lines before it stay recorded, the lines after it still run, and the line can be retried at once. Because the registrar may have registered the domain, such a line is not refunded automatically: retry it (a domain that was registered is then found and counted as done) or refund it by hand.
- A run that is cancelled part way (the request is aborted or the host stops) records the lines it had not finished as Failed with the code `ORDER_LINE_INTERRUPTED`, so they can be retried at once.
- Lines finishing at the same moment cannot leave the order wrong: the order's status is always worked out from the lines as they are saved, so an order whose lines are all fulfilled is Active.
- Only one retry of a line runs at a time; a second one is refused with `ORDER_LINE_NOT_RETRYABLE`. A retry that died without finishing can be started again once its claim has run out, ten minutes by default (see below).
- Every retry is written to the client's activity log.

## Refunded lines

When the registrar rejects a domain in a paid order, the amount of that line is given back automatically, to the client's credit and to the gateway that took the payment.

- While the money goes back the line shows **Refunding** and cannot be retried (`ORDER_LINE_NOT_RETRYABLE`), so a line is never both delivered and refunded.
- When all of it is back the line is **Refunded**: closed for good, and no longer keeping the order Incomplete.
- When nothing came back (the gateway refused the refund) the line returns to **Failed**, so you can retry it or refund it by hand.
- When only part came back (for example the credit share was returned and the gateway refused the rest) the line shows **Partly refunded**. It cannot be retried. Finish the refund by hand from the invoice, and the order stays Incomplete until you do. The log line starting with `REFUND FAILED` says how much is left.
- When you have given back the rest, click **Refund finished** on the line and confirm. The line becomes **Refunded**, the order returns to Active when no other line is open, and the action is written to the client's activity log. The button is offered only on a Partly refunded line; any other line is refused with `ORDER_LINE_NOT_REFUNDABLE`.

## Lines accepted before this release

Orders that were accepted before lines kept their own state have all their lines at **Not run yet** and stay Active. Those lines cannot be retried: a retry is only offered for a failed line of an Incomplete order, and nothing marks an old line as failed. If one of them was never fulfilled, fix it by hand (create the service or domain from the client's page). Only orders accepted from now on are tracked line by line.

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
| `Orders__RetryLeaseMinutes` | 10 | Minutes a claim on an order line (the accept's, or a retry's) is respected before a retry may take the line over (1 to 1440). Set it longer than the slowest registrar or provisioning call. |

See the [Configuration reference](/operators/configuration) for how to change these.
