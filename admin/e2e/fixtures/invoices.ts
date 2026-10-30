/**
 * Fixtures for the invoices list: one unpaid and one paid invoice on a single page, and the detail
 * of the unpaid one.
 */
import { FulfilmentKind, FulfilmentStatus } from '../../src/types/invoice'
import type { FulfilmentRecord, Invoice, InvoiceDetail } from '../../src/types/invoice'
import type { PaginatedList } from '../../src/types/paginatedlist'

/** An invoice the operator can mark paid. */
export const unpaidInvoice: Invoice = {
  id: 601,
  clientId: 7,
  clientName: 'Ada Lovelace',
  status: 'Unpaid',
  dueDate: '2026-11-30T00:00:00Z',
  createdAt: '2026-10-01T09:00:00Z',
  invoiceDate: '2026-10-01T00:00:00Z',
  total: 120,
  subTotal: 120,
  tax: 0,
}

/** An invoice that is already settled. */
export const paidInvoice: Invoice = {
  ...unpaidInvoice,
  id: 602,
  clientId: 8,
  status: 'Paid',
  clientName: 'Grace Hopper',
  total: 60,
  subTotal: 60,
}

/** `GET /api/billing/601`: the unpaid invoice with its lines, as the detail screen loads it. */
export const unpaidInvoiceDetail: InvoiceDetail = {
  id: unpaidInvoice.id,
  clientId: unpaidInvoice.clientId,
  clientName: 'Ada Lovelace',
  status: unpaidInvoice.status,
  currency: 'USD',
  dates: {
    invoiceDate: unpaidInvoice.invoiceDate,
    dueDate: unpaidInvoice.dueDate,
    createdAt: unpaidInvoice.createdAt,
    paidAt: null,
  },
  amounts: { subTotal: 120, credit: 0, tax: 0, taxRate: 0, total: 120 },
  amountDue: 120,
  payment: { method: null, gatewayTransactionId: null },
  notes: null,
  items: [{ id: 1, description: 'Hosting', unitPrice: 120, quantity: 1, amount: 120 }],
  transactions: [],
}

/** `GET /api/billing`: one page holding both invoices. */
export const invoicesPage: PaginatedList<Invoice> = {
  items: [unpaidInvoice, paidInvoice],
  pageNumber: 1,
  pageSize: 25,
  totalCount: 2,
  totalPages: 1,
}

/** `GET /api/billing/601/fulfilment`: one failed run the operator may retry. */
export const failedFulfilment: FulfilmentRecord = {
  id: 91,
  invoiceId: unpaidInvoice.id,
  pluginId: 'provisioning-demo',
  kind: FulfilmentKind.Paid,
  status: FulfilmentStatus.Failed,
  attempts: 3,
  nextAttemptAt: null,
  lastError: 'The upstream account is locked.',
  completedAt: null,
  createdAt: '2026-10-02T08:00:00Z',
  canRetry: true,
}
