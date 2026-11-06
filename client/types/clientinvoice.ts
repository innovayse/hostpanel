/**
 * One invoice on the client's account.
 *
 * ## One vocabulary, and it is the backend's
 *
 * `server/api/portal/client/invoices/index.get.ts` and `[id].get.ts` are bare passthroughs of
 * `GET /api/me/invoices` and `/api/me/invoices/{id}`, both of which answer with
 * `Innovayse.Hostpanel.Application.Billing.Common.MyInvoiceDto` serialised camelCase, so the
 * names below are `MyInvoiceDto`'s own. A name the DTO does not send would be `undefined` at
 * runtime and render blank; this type follows the DTO, never the other way round.
 *
 * ## Both endpoints return this same shape
 *
 * `MyBillingController.GetMineAsync` returns `IReadOnlyList<MyInvoiceDto>`, not
 * `InvoiceListItemDto`: the list carries `items` and `transactions` too. One interface
 * therefore covers the list page, the detail page and the payment page, and there is no
 * lighter list variant to keep in step with it.
 *
 * ## The invoice carries its own currency
 *
 * `MyInvoiceDto.Currency` is an ISO 4217 code set once, at creation, from the payer's currency at the time, and never changed. An
 * invoice's own currency is the authority for formatting it, never the account's *current*
 * currency (`ClientUser.currency`), which can differ if the account's currency were ever
 * changed, and never the page's language. Amounts here are plain `number`s in {@link
 * ClientInvoice.currency}; format them via `stores/currency.ts` and `utils/formatMoney.ts`
 * when the currency is a configured one, falling back to `utils/formatCurrency.ts`'s bare ISO
 * code path otherwise.
 *
 * @module types/clientinvoice
 */

import type { ClientInvoiceStatus } from '~/types/clientinvoicestatus'
import type { ClientInvoiceItem } from '~/types/clientinvoiceitem'
import type { ClientInvoiceTransaction } from '~/types/clientinvoicetransaction'

/**
 * One invoice, as `GET /api/portal/client/invoices` and `GET /api/portal/client/invoices/:id` return it.
 *
 * Field names and types are `MyInvoiceDto`'s. The client's own invoice carries no gateway
 * reference, staff note or owner field. Every date is an ISO 8601 string carrying an
 * offset (a serialised `DateTimeOffset`); every amount is a `number`, not the decimal string a
 * legacy-shaped endpoint would have sent.
 */
export interface ClientInvoice {
  /** Invoice primary key. */
  id: number
  /** Current lifecycle status. */
  status: ClientInvoiceStatus
  /** ISO 4217 code this invoice was raised and is payable in: fixed at creation, never changed. */
  currency: string
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
  /**
   * What is still owed: the total less the payments recorded against it, `0` once the invoice is
   * closed. This is what a payment started from the portal charges.
   */
  amountDue: number
  /** Sum of all line item amounts before tax and credit. */
  subTotal: number
  /** Computed tax amount. */
  tax: number
  /** Tax rate percentage. */
  taxRate: number
  /** Total credit applied. */
  credit: number
  /**
   * Whether this is a credit top-up invoice: one fixed line that puts its amount on the client's balance when paid. Credit
   * cannot be applied to it.
   */
  isCreditTopUp: boolean
  /** Preferred payment method; null when not specified. */
  paymentMethod: string | null
  /** Line items on the invoice. */
  items: ClientInvoiceItem[]
  /** Financial transactions recorded against the invoice. */
  transactions: ClientInvoiceTransaction[]
}
