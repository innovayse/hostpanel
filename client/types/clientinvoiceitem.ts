/**
 * One line item on an invoice, as `InvoiceItemDto` sends it.
 *
 * @module types/clientinvoiceitem
 */

/** One line item on an invoice, as `InvoiceItemDto` sends it. */
export interface ClientInvoiceItem {
  /** Line item primary key. */
  id: number
  /** Human-readable charge description. */
  description: string
  /** Price per unit. */
  unitPrice: number
  /** Number of units. */
  quantity: number
  /** Line total (`unitPrice` × `quantity`). */
  amount: number
  /** SKU of the product the line was sold from, from its product snapshot; `null` for a line without one (a domain, a manual line). */
  sku: string | null
}
