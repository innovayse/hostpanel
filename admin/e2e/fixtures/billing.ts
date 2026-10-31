/**
 * Fixtures for creating an invoice from a client's invoices tab.
 */
import type { Invoice, InvoiceDetail } from '../../src/types/invoice'
import type { PaginatedList } from '../../src/types/paginatedlist'

/** Identifier the stubbed `POST /api/billing` hands out. */
export const newInvoiceId = 501

/** The invoice that exists once the create call has been answered. */
export const createdInvoice: InvoiceDetail = {
  id: newInvoiceId,
  clientId: 7,
  clientName: 'Ada Lovelace',
  status: 'Draft',
  currency: 'USD',
  dates: {
    invoiceDate: '2026-10-14T00:00:00Z',
    dueDate: '2026-12-31T00:00:00Z',
    createdAt: '2026-10-14T09:00:00Z',
    paidAt: null,
  },
  amounts: { subTotal: 240, credit: 0, tax: 0, taxRate: 0, total: 240 },
  amountDue: 240,
  payment: { method: null, gatewayTransactionId: null },
  notes: null,
  items: [{ id: 1, description: 'Managed hosting', unitPrice: 120, quantity: 2, amount: 240, sku: null }],
  transactions: [],
}

/** `GET /api/billing/client/7` before anything was created. */
export const emptyClientInvoices: PaginatedList<Invoice> = { items: [], pageNumber: 1, pageSize: 25, totalCount: 0, totalPages: 0 }
