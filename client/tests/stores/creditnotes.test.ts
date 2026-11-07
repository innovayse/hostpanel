/**
 * Tests for `stores/creditnotes.ts`.
 *
 * The store, not the credit notes page, owns the loaded notes, the in-flight flags and the refusal sentences, so these assertions pin
 * the contract the page renders from.
 *
 * @module tests/stores/creditnotes.test
 */

import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import type { ClientCreditNote } from '~/types/clientcreditnote'

/** The mocked credit notes read. */
const fetchNotesMock = vi.fn()

/** The mocked PDF read. */
const fetchPdfMock = vi.fn()

/** The mocked file save. */
const saveBlobMock = vi.fn()

vi.mock('~/composables/apis/useBillingApi', () => ({
  /**
   * Stands in for the real API composable so the store runs without transport.
   *
   * @returns Only the endpoints the credit notes store reaches for.
   */
  useBillingApi: () => ({ fetchCreditNotes: fetchNotesMock, fetchCreditNotePdf: fetchPdfMock })
}))

vi.mock('~/utils/saveBlob', () => ({ saveBlob: saveBlobMock }))

const { useCreditNotesStore } = await import('~/stores/creditnotes')

/** A note. */
const note: ClientCreditNote = {
  id: 3,
  number: 'CN-0003',
  status: 'Applied',
  method: 'AccountCredit',
  invoiceId: 12,
  currency: 'USD',
  subTotal: 50,
  taxRate: 20,
  tax: 10,
  total: 60,
  reason: 'Service was down',
  issuedAt: '2026-10-01T09:00:00Z',
  voidedAt: null
}

describe('useCreditNotesStore.load', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    fetchNotesMock.mockReset()
  })

  it('reads the notes and exposes them', async () => {
    fetchNotesMock.mockResolvedValue([note])
    const store = useCreditNotesStore()

    const loaded = await store.load()

    expect(loaded).toEqual([note])
    expect(store.notes).toEqual([note])
    expect(store.loading).toBe(false)
    expect(store.error).toBe('')
  })

  it('leaves no notes and the sentence of the refusal when the read fails', async () => {
    fetchNotesMock.mockRejectedValue({ data: { code: 'CLIENT_PROFILE_NOT_FOUND', message: 'No client profile.' }, statusMessage: 'No client profile.' })
    const store = useCreditNotesStore()
    store.notes = [note]

    await store.load()

    expect(store.notes).toEqual([])
    expect(store.error).not.toBe('')
    expect(store.loading).toBe(false)
  })
})

describe('useCreditNotesStore.downloadPdf', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    fetchPdfMock.mockReset()
    saveBlobMock.mockReset()
  })

  it('saves the PDF under the number of the note', async () => {
    const pdf = new Blob(['%PDF-'], { type: 'application/pdf' })
    fetchPdfMock.mockResolvedValue(pdf)
    const store = useCreditNotesStore()

    await store.downloadPdf(note)

    expect(fetchPdfMock).toHaveBeenCalledWith(3)
    expect(saveBlobMock).toHaveBeenCalledWith(pdf, 'credit-note-CN-0003.pdf')
    expect(store.downloadingId).toBeNull()
    expect(store.downloadError).toBe('')
  })

  it('keeps the sentence of a refusal and saves nothing', async () => {
    fetchPdfMock.mockRejectedValue({ data: { code: 'CREDIT_NOTE_NOT_FOUND', message: 'The credit note was not found.' }, statusMessage: 'The credit note was not found.' })
    const store = useCreditNotesStore()

    await store.downloadPdf(note)

    expect(saveBlobMock).not.toHaveBeenCalled()
    expect(store.downloadError).not.toBe('')
    expect(store.downloadingId).toBeNull()
  })

  it('marks the note being downloaded while the request is in flight', async () => {
    let release: (blob: Blob) => void = () => undefined
    fetchPdfMock.mockReturnValue(new Promise<Blob>((resolve) => { release = resolve }))
    const store = useCreditNotesStore()

    const pending = store.downloadPdf(note)
    expect(store.downloadingId).toBe(3)
    release(new Blob(['%PDF-']))
    await pending

    expect(store.downloadingId).toBeNull()
  })
})
