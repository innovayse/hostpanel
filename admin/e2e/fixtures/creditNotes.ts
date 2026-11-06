/**
 * Fixtures for the credit note flows: a paid invoice that can be credited, the notes of the list and of one page, and the refusal
 * body of a note that is more than the invoice can take.
 */
import { CreditNoteMethod, CreditNoteStatus } from '../../src/types/creditnote'
import type { CreditNote, CreditNoteDetail } from '../../src/types/creditnote'
import type { InvoiceDetail } from '../../src/types/invoice'
import type { PaginatedList } from '../../src/types/paginatedlist'
import { unpaidInvoiceDetail } from './invoices'

/** `GET /api/billing/1`: a paid invoice (id 1) of 100 with 20% tax, which can still be credited 120. */
export const paidInvoiceDetail: InvoiceDetail = {
  ...unpaidInvoiceDetail,
  id: 1,
  status: 'Paid',
  amounts: { subTotal: 100, credit: 0, tax: 20, taxRate: 20, total: 120 },
  amountDue: 0,
  creditableAmount: 120,
  dates: { ...unpaidInvoiceDetail.dates, paidAt: '2026-10-01T09:00:00Z' },
  items: [{ id: 1, description: 'Hosting', unitPrice: 100, quantity: 1, amount: 100, sku: null }],
}

/** An Applied account credit note of 60 against the paid invoice. */
export const appliedNote: CreditNote = {
  id: 3,
  number: 'CN-0003',
  status: CreditNoteStatus.Applied,
  method: CreditNoteMethod.AccountCredit,
  clientId: unpaidInvoiceDetail.clientId,
  clientName: 'Ada Lovelace',
  invoiceId: 1,
  currency: 'USD',
  subTotal: 50,
  taxRate: 20,
  tax: 10,
  total: 60,
  reason: 'Service was down for two days',
  refundReference: null,
  createdAt: '2026-10-02T09:00:00Z',
  issuedAt: '2026-10-02T09:00:00Z',
  appliedAt: '2026-10-02T09:00:00Z',
  voidedAt: null,
  voidReason: null,
}

/** The same note after it was voided. */
export const voidedNote: CreditNote = {
  ...appliedNote,
  status: CreditNoteStatus.Voided,
  voidedAt: '2026-10-03T09:00:00Z',
  voidReason: 'Issued to the wrong invoice',
}

/**
 * Builds one page of notes as the list endpoint answers.
 *
 * @param notes - The notes on the page.
 * @returns The page body.
 */
export const notesPage = (notes: CreditNote[]): PaginatedList<CreditNote> => ({
  items: notes,
  pageNumber: 1,
  pageSize: 20,
  totalCount: notes.length,
  totalPages: notes.length === 0 ? 0 : 1,
})

/**
 * Builds the note with its one line, as the detail endpoint answers.
 *
 * @param note - The note.
 * @returns The note and its line.
 */
export const noteDetail = (note: CreditNote): CreditNoteDetail => ({
  note,
  items: [{ id: 1, description: 'Credit for hosting', unitPrice: 50, quantity: 1, amount: 50 }],
})

/** The sentence the backend refuses a note with when it is more than the invoice can still be credited. */
export const overCreditSentence = 'The credit note is more than this invoice can still be credited, once earlier credit notes and refunds are counted.'

/** The refusal body of that note. */
export const overCreditRefusal = { type: 'CREDIT_NOTE_EXCEEDS_CREDITABLE', title: 'Bad Request', status: 400, detail: overCreditSentence }
