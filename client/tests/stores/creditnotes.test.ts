/**
 * Tests for `stores/creditnotes.ts`, through the real billing API composable and transport against a stub server.
 *
 * The store, not the credit notes page, owns the loaded notes, the in-flight flags and the refusal sentences, so these assertions pin
 * the contract the page renders from. Nothing of our own is replaced: the stub plays the BFF, and the file a download hands to the
 * browser is read at the browser's own primitives.
 *
 * @module tests/stores/creditnotes.test
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { captureDownloads, PDF_REPLY } from '../helpers/captureDownloads'
import { heldReply, routeTable } from '../helpers/stubRoutes'
import type { StubRouteTable } from '../helpers/stubRoutes'
import type { StubServer } from '../helpers/stubServer'
import { startTransport } from '../helpers/stubTransport'
import { useCreditNotesStore } from '~/stores/creditnotes'
import type { ClientCreditNote } from '~/types/clientcreditnote'
import { waitUntil } from '~/tests/helpers/waitUntil'

/** The stub playing the BFF. */
let stub: StubServer

/** The replies the stub gives, by `METHOD /path`; each test fills the ones it needs. */
let table: StubRouteTable

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

/** The read of the notes. */
const NOTES_ROUTE = 'GET /api/portal/client/credit-notes'

/** The read of the PDF of {@link note}. */
const PDF_ROUTE = 'GET /api/portal/client/credit-notes/3/pdf'

beforeEach(async () => {
  setActivePinia(createPinia())
  table = {}
  stub = await startTransport(routeTable(table))
})

afterEach(async () => {
  vi.restoreAllMocks()
  await stub.close()
})

describe('useCreditNotesStore.load', () => {
  it('reads the notes and exposes them', async () => {
    table[NOTES_ROUTE] = { body: [note] }
    const store = useCreditNotesStore()

    const loaded = await store.load()

    expect(loaded).toEqual([note])
    expect(store.notes).toEqual([note])
    expect(store.loading).toBe(false)
    expect(store.error).toBe('')
  })

  it('leaves no notes and the sentence of the refusal when the read fails', async () => {
    table[NOTES_ROUTE] = { status: 404, body: { data: { code: 'CLIENT_PROFILE_NOT_FOUND', message: 'No client profile.' }, statusMessage: 'No client profile.' } }
    const store = useCreditNotesStore()
    store.notes = [note]

    await store.load()

    expect(store.notes).toEqual([])
    expect(store.error).not.toBe('')
    expect(store.loading).toBe(false)
  })
})

describe('useCreditNotesStore.downloadPdf', () => {
  it('saves the PDF under the number of the note', async () => {
    table[PDF_ROUTE] = PDF_REPLY
    const downloads = captureDownloads()
    const store = useCreditNotesStore()

    await store.downloadPdf(note)

    expect(stub.requests.map(request => `${request.method} ${request.url}`)).toEqual([PDF_ROUTE])
    expect(downloads).toHaveLength(1)
    expect(downloads[0]?.fileName).toBe('credit-note-CN-0003.pdf')
    expect(await downloads[0]?.blob.text()).toBe('%PDF-')
    expect(store.downloadingId).toBeNull()
    expect(store.downloadError).toBe('')
  })

  it('keeps the sentence of a refusal and saves nothing', async () => {
    table[PDF_ROUTE] = { status: 404, body: { data: { code: 'CREDIT_NOTE_NOT_FOUND', message: 'The credit note was not found.' }, statusMessage: 'The credit note was not found.' } }
    const downloads = captureDownloads()
    const store = useCreditNotesStore()

    await store.downloadPdf(note)

    expect(downloads).toHaveLength(0)
    expect(store.downloadError).not.toBe('')
    expect(store.downloadingId).toBeNull()
  })

  it('marks the note being downloaded while the request is in flight', async () => {
    const held = heldReply()
    table[PDF_ROUTE] = held.reply
    captureDownloads()
    const store = useCreditNotesStore()

    const pending = store.downloadPdf(note)
    expect(store.downloadingId).toBe(3)
    await waitUntil(() => {
      expect(held.waiting()).toBe(1)
    })
    held.release(PDF_REPLY)
    await pending

    expect(store.downloadingId).toBeNull()
  })
})
