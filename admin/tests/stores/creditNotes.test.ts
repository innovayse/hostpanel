/**
 * Tests for the credit notes store: a load fills the list or the note, issuing returns the new id, voiding reloads the note, the
 * PDF returns its file, and a refusal lands in `error` as the API sentence (and per field) while the earlier data stays.
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useCreditNotesStore } from '@/stores/creditNotes'
import { CreditNoteMethod, CreditNoteStatus } from '@/types/creditnote'
import { creditNoteDetailFixture, creditNoteFixture, creditNotePage } from '../helpers/creditNoteFixtures'
import { callOf, invalid, json, problem, stubFetch, stubOffline } from '../helpers/fetch'

/** Sentence the client writes when nothing answered. */
const NO_ANSWER = 'Could not reach the server. Check your connection and try again.'

beforeEach(() => {
  setActivePinia(createPinia())
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('creditNotesStore', () => {
  it('fetchPage fills the page and the total and clears loading', async () => {
    const mock = stubFetch(json(creditNotePage([creditNoteFixture(), creditNoteFixture({ id: 4, number: 'CN-0004' })])))
    const store = useCreditNotesStore()

    const ok = await store.fetchPage({}, 1, 20)

    expect(ok).toBe(true)
    expect(store.notes.map((n) => n.number)).toEqual(['CN-0003', 'CN-0004'])
    expect(store.totalCount).toBe(2)
    expect(store.loading).toBe(false)
    expect(callOf(mock).url).toBe('/api/billing/credit-notes?page=1&pageSize=20')
  })

  it('fetchPage shows the API sentence on refusal and keeps the earlier page', async () => {
    const store = useCreditNotesStore()
    stubFetch(json(creditNotePage([creditNoteFixture()])))
    await store.fetchPage({}, 1, 20)

    stubFetch(problem('Not allowed.', 403))
    const ok = await store.fetchPage({}, 1, 20)

    expect(ok).toBe(false)
    expect(store.error).toBe('Not allowed.')
    expect(store.notes).toHaveLength(1)
  })

  it('fetchDetail fills the note and clears it on a refused load', async () => {
    const store = useCreditNotesStore()
    stubFetch(json(creditNoteDetailFixture()))
    expect(await store.fetchDetail(3)).toBe(true)
    expect(store.detail?.note.number).toBe('CN-0003')

    stubFetch(problem('The credit note was not found.', 404))
    expect(await store.fetchDetail(99)).toBe(false)
    expect(store.detail).toBeNull()
    expect(store.error).toBe('The credit note was not found.')
  })

  it('issue returns the new id', async () => {
    const store = useCreditNotesStore()
    const mock = stubFetch(json(7, 201))

    const id = await store.issue(5, { items: [{ description: 'Credit', unitPrice: 10, quantity: 1 }], method: CreditNoteMethod.AccountCredit, reason: 'Outage' })

    expect(id).toBe(7)
    expect(callOf(mock).url).toBe('/api/billing/5/credit-notes')
  })

  it('issue returns null with the API sentence and the field messages on a refusal', async () => {
    const store = useCreditNotesStore()
    stubFetch(invalid('Fix the form.', { reason: ['Give a reason for the credit note.'] }))

    const id = await store.issue(5, { items: [{ description: 'Credit', unitPrice: 10, quantity: 1 }], method: CreditNoteMethod.AccountCredit, reason: '' })

    expect(id).toBeNull()
    expect(store.error).toBe('Fix the form.')
    expect(store.fieldErrors.reason).toBe('Give a reason for the credit note.')
  })

  it('issue says nothing answered when the server cannot be reached', async () => {
    const store = useCreditNotesStore()
    stubOffline()

    expect(await store.issue(5, { items: [], method: CreditNoteMethod.AccountCredit, reason: 'x' })).toBeNull()
    expect(store.error).toBe(NO_ANSWER)
  })

  it('voidNote posts the reason and reloads the note', async () => {
    const store = useCreditNotesStore()
    const mock = stubFetch(new Response(null, { status: 204 }), json(creditNoteDetailFixture({ status: CreditNoteStatus.Voided, voidReason: 'Mistake' })))

    const ok = await store.voidNote(3, 'Mistake')

    expect(ok).toBe(true)
    expect(callOf(mock, 0).url).toBe('/api/billing/credit-notes/3/void')
    expect(callOf(mock, 1).url).toBe('/api/billing/credit-notes/3')
    expect(store.detail?.note.status).toBe(CreditNoteStatus.Voided)
  })

  it('voidNote reports a refusal and does not reload', async () => {
    const store = useCreditNotesStore()
    const mock = stubFetch(problem('The client has already spent the credit.', 400))

    const ok = await store.voidNote(3, 'Mistake')

    expect(ok).toBe(false)
    expect(store.error).toBe('The client has already spent the credit.')
    expect(mock).toHaveBeenCalledTimes(1)
  })

  it('downloadPdf returns the file, or null with the sentence', async () => {
    const store = useCreditNotesStore()
    stubFetch(new Response('%PDF-', { status: 200, headers: { 'Content-Disposition': 'attachment; filename="credit-note-CN-0003.pdf"' } }))
    expect((await store.downloadPdf(3))?.fileName).toBe('credit-note-CN-0003.pdf')

    stubFetch(problem('The credit note was not found.', 404))
    expect(await store.downloadPdf(99)).toBeNull()
    expect(store.error).toBe('The credit note was not found.')
  })
})
