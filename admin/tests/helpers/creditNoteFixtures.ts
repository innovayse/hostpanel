/**
 * Builders for the credit note tests: a note as the API answers it, with the fields a test overrides.
 */

import { CreditNoteMethod, CreditNoteStatus } from '@/types/creditnote'
import type { CreditNote, CreditNoteDetail } from '@/types/creditnote'

/**
 * Builds an Applied account credit note of 60.00 against invoice 5.
 *
 * @param overrides - Fields that differ from the default.
 * @returns The note.
 */
export const creditNoteFixture = (overrides: Partial<CreditNote> = {}): CreditNote => ({
  id: 3,
  number: 'CN-0003',
  status: CreditNoteStatus.Applied,
  method: CreditNoteMethod.AccountCredit,
  clientId: 9,
  clientName: 'Grace Hopper',
  invoiceId: 5,
  currency: 'USD',
  subTotal: 50,
  taxRate: 20,
  tax: 10,
  total: 60,
  reason: 'Service was down',
  refundReference: null,
  createdAt: '2026-10-01T09:00:00Z',
  issuedAt: '2026-10-01T09:00:00Z',
  appliedAt: '2026-10-01T09:00:00Z',
  voidedAt: null,
  voidReason: null,
  ...overrides,
})

/**
 * Builds the note with its one line, as the detail endpoint answers.
 *
 * @param overrides - Fields of the note that differ from the default.
 * @returns The note and its lines.
 */
export const creditNoteDetailFixture = (overrides: Partial<CreditNote> = {}): CreditNoteDetail => ({
  note: creditNoteFixture(overrides),
  items: [{ id: 1, description: 'Credit for hosting', unitPrice: 50, quantity: 1, amount: 50 }],
})

/**
 * Builds one page of notes as the list endpoint answers.
 *
 * @param notes - The notes on the page.
 * @returns The page body.
 */
export const creditNotePage = (notes: CreditNote[]) => ({
  items: notes,
  pageNumber: 1,
  pageSize: 20,
  totalCount: notes.length,
  totalPages: 1,
})
