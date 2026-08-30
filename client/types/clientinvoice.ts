/**
 * One invoice on the client's account.
 *
 * ## One vocabulary, and it is the backend's
 *
 * This file used to declare WHMCS-era names — `date`, `duedate`, `datepaid`, `subtotal`,
 * `currencycode`, `currencyprefix`, `currencysuffix` — none of which the API has ever sent.
 * `server/api/portal/client/invoices/index.get.ts` and `[id].get.ts` are bare passthroughs of
 * `GET /api/me/invoices` and `/api/me/invoices/{id}`, both of which answer with
 * `Innovayse.Application.Billing.Common.InvoiceDto` serialised camelCase. So every field named
 * here was `undefined` in production, and invoice dates rendered blank on
 * `pages/client/invoices/index.vue` and on the dashboard.
 *
 * The names below are `InvoiceDto`'s own. The backend's are the correct ones and the DTO was
 * deliberately not renamed to match the portal: this type is the seam, and it is the half that
 * was wrong.
 *
 * ## Both endpoints return this same shape
 *
 * `MyBillingController.GetMineAsync` returns `IReadOnlyList<InvoiceDto>`, not
 * `InvoiceListItemDto` — the list carries `items` and `transactions` too. One interface
 * therefore covers the list page, the detail page and the payment page, and there is no
 * lighter list variant to keep in step with it.
 *
 * ## There is no currency on an invoice
 *
 * `InvoiceDto` has no currency field of any kind. The account's currency is the authority —
 * `ClientDto.Currency`, an ISO 4217 code, reaching the portal as `ClientUser.currency` through
 * `server/api/portal/client/me.get.ts`. Amounts here are plain `number`s in that currency; see
 * `utils/formatCurrency.ts` for how one is rendered without inventing the other.
 *
 * @module types/clientinvoice
 */

/**
 * Lifecycle status of an invoice, as `Innovayse.Domain.Billing.InvoiceStatus` serialises.
 *
 * `Program.cs` registers a `JsonStringEnumConverter`, so these arrive as the member names
 * verbatim. All eight members are listed: the previous union omitted `Overdue` and
 * `PaymentPending`, which are states the backend really does write, so a row in either one
 * type-checked as impossible while rendering in production.
 */
export type ClientInvoiceStatus =
  | 'Draft'
  | 'Unpaid'
  | 'Paid'
  | 'Overdue'
  | 'Cancelled'
  | 'Refunded'
  | 'Collections'
  | 'PaymentPending'

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
}

/** One financial transaction recorded against an invoice, as `InvoiceTransactionDto` sends it. */
export interface ClientInvoiceTransaction {
  /** Transaction primary key. */
  id: number
  /** UTC timestamp of the transaction, ISO 8601. */
  date: string
  /** Payment gateway name. */
  gateway: string
  /** External transaction reference. */
  transactionId: string
  /** Transaction amount. */
  amount: number
  /** Fees charged by the gateway. */
  fees: number
  /** Transaction type, as `InvoiceTransactionType` serialises. */
  type: string
  /** Optional notes; null when not provided. */
  notes: string | null
}

/**
 * One invoice, as `GET /api/portal/client/invoices` and `.../invoices/:id` return it.
 *
 * Field names and types are `InvoiceDto`'s. Every date is an ISO 8601 string carrying an
 * offset (a serialised `DateTimeOffset`); every amount is a `number`, not the decimal string a
 * WHMCS-shaped endpoint would have sent.
 */
export interface ClientInvoice {
  /** Invoice primary key. */
  id: number
  /** FK to the owning client. */
  clientId: number
  /** Display name of the owning client. */
  clientName: string
  /** Current lifecycle status. */
  status: ClientInvoiceStatus
  /** Issue date (UTC, ISO 8601). */
  invoiceDate: string
  /** Payment due date (UTC, ISO 8601). */
  dueDate: string
  /** Creation timestamp (UTC, ISO 8601). */
  createdAt: string
  /** Payment timestamp (UTC, ISO 8601); null while unpaid. */
  paidAt: string | null
  /** Final total after tax and credit. */
  total: number
  /** Sum of all line item amounts before tax and credit. */
  subTotal: number
  /** Computed tax amount. */
  tax: number
  /** Tax rate percentage. */
  taxRate: number
  /** Total credit applied. */
  credit: number
  /** Payment gateway reference; null when unpaid. */
  gatewayTransactionId: string | null
  /** Optional invoice notes. */
  notes: string | null
  /** Preferred payment method; null when not specified. */
  paymentMethod: string | null
  /** Line items on the invoice. */
  items: ClientInvoiceItem[]
  /** Financial transactions recorded against the invoice. */
  transactions: ClientInvoiceTransaction[]
}
