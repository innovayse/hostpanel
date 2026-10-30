/**
 * Lifecycle status of an invoice, as `Innovayse.Hostpanel.Domain.Billing.InvoiceStatus` serialises.
 *
 * @module types/clientinvoicestatus
 */

import type { InvoiceStatus } from '~/types/invoicestatus'

/**
 * Lifecycle status of an invoice, as `Innovayse.Hostpanel.Domain.Billing.InvoiceStatus` serialises.
 *
 * `Program.cs` registers a `JsonStringEnumConverter`, so these arrive as the member names
 * verbatim. All nine members are listed, `Overdue` and `PaymentPending` included: the
 * backend writes both, and a missing member would type-check a real row as impossible.
 */
export type ClientInvoiceStatus = `${InvoiceStatus}`
