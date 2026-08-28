/** One invoice on the client's account, as `GET /api/portal/client/invoices` returns it. */
export interface ClientInvoice {
  /** Invoice primary key. */
  id: number
  /** FK to the client the invoice is raised against. */
  userid: number
  /** Issue date. */
  date: string
  /** Payment due date. */
  duedate: string
  /** Date payment was received, empty while unpaid. */
  datepaid: string
  /** Line-item total before credit and tax, as a decimal string. */
  subtotal: string
  /** Account credit applied, as a decimal string. */
  credit: string
  /** First tax level charged, as a decimal string. */
  tax: string
  /** Second tax level charged, as a decimal string. */
  tax2: string
  /** Invoice total, as a decimal string. */
  total: string
  /** Amount still outstanding, as a decimal string. */
  balance: string
  /** Current lifecycle status of the invoice. */
  status: 'Paid' | 'Unpaid' | 'Cancelled' | 'Refunded' | 'Collections' | 'Draft'
  /** ISO 4217 currency code the amounts are quoted in. */
  currencycode: string
  /** Symbol printed before an amount in that currency. */
  currencyprefix: string
  /** Symbol printed after an amount in that currency. */
  currencysuffix: string
}
