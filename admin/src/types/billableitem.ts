/**
 * A billable item and the paged envelope it arrives in.
 */

import type { Invoice } from './invoice'
import type { PagedResult } from './pagedresult'

/**
 * A billable item that can be invoiced to a client.
 *
 * Mirrors the API's `BillableItemDto` (`GET /billing/billable-items`) and its client-scoped
 * twin `ClientBillableItemDto` (`GET /clients/{id}/billable-items`), which differ only in
 * that the client-scoped one omits `clientName`.
 *
 * This interface previously described a different record altogether — `serviceId`,
 * `serviceName`, `hoursQty`, `isHours`, `invoiceAction`, `dueDate`, `invoiceCount` and three
 * `recurrence*` fields, none of which either endpoint has ever returned — while omitting
 * `clientName`, `currency`, `type` and `isInvoiced`, which they all return. See the note on
 * the three retained fields below.
 */
export interface BillableItem {
  /** Unique billable item identifier. */
  id: number
  /** Associated client identifier. */
  clientId: number
  /**
   * Display name of the owning client.
   *
   * Only the admin-wide list (`/billing/billable-items`) carries it; the client-scoped
   * endpoint does not, which is why it is optional.
   */
  clientName?: string
  /** Charge description. */
  description: string
  /** Charge amount. */
  amount: number
  /** ISO 4217 currency code. */
  currency: string
  /** Item type — `OneTime` or `Recurring`. */
  type: string
  /** Recurrence period; null for one-time items. */
  recurringPeriod: string | null
  /** Whether the item has already been invoiced. */
  isInvoiced: boolean
  /** FK to the invoice; null while uninvoiced. */
  invoiceId: number | null
  /** ISO 8601 next due date for recurring items; null otherwise. */
  nextDueDate: string | null
  /** ISO 8601 creation timestamp. */
  createdAt: string

  /**
   * BUG — these three are read by `ClientBillableItemsView` but **no endpoint sends them**,
   * so that screen's "Hours/Qty" and "Invoice Action" columns render blank at runtime.
   *
   * They are kept here, optional, so the type states the truth (`undefined` at runtime)
   * without silently deleting the columns from the screen. Fixing it properly means either
   * adding the fields to `ClientBillableItemDto` or dropping the columns — a product
   * decision, not a typing one.
   */
  hoursQty?: number
  /** See the note on {@link BillableItem.hoursQty} — never sent by the API. */
  isHours?: boolean
  /** See the note on {@link BillableItem.hoursQty} — never sent by the API. */
  invoiceAction?: string
}

/** Response from the billable items list endpoint. */
export interface BillableItemsResult {
  /** All uninvoiced items for the client. */
  uninvoicedItems: BillableItem[]
  /** Sum of all uninvoiced item amounts. */
  uninvoicedTotal: number
  /** Paginated invoiced items. */
  invoicedItems: PagedResult<BillableItem>
}
