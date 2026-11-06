/**
 * Tests for the credit notes API composable: each endpoint through the real `useApi` transport with `fetch` stubbed.
 */

import { afterEach, describe, expect, it, vi } from 'vitest'
import { useCreditNotesApi } from '@/composables/apis/useCreditNotesApi'
import { CreditNoteMethod, CreditNoteStatus } from '@/types/creditnote'
import { creditNoteDetailFixture, creditNotePage, creditNoteFixture } from '../../helpers/creditNoteFixtures'
import { callOf, json, stubFetch } from '../../helpers/fetch'

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('useCreditNotesApi', () => {
  it('list reads one page with the filters in the query', async () => {
    const mock = stubFetch(json(creditNotePage([creditNoteFixture()])))

    const page = await useCreditNotesApi().list({ invoiceId: 5, status: CreditNoteStatus.Applied }, 2, 20)

    expect(callOf(mock).url).toBe('/api/billing/credit-notes?invoiceId=5&status=Applied&page=2&pageSize=20')
    expect(page.items[0]?.number).toBe('CN-0003')
  })

  it('get reads one note with its lines', async () => {
    const mock = stubFetch(json(creditNoteDetailFixture()))

    const detail = await useCreditNotesApi().get(3)

    expect(callOf(mock).url).toBe('/api/billing/credit-notes/3')
    expect(detail.items).toHaveLength(1)
  })

  it('issue posts the lines, the method and the reason to the invoice route', async () => {
    const mock = stubFetch(json(7, 201))

    const id = await useCreditNotesApi().issue(5, {
      items: [{ description: 'Credit', unitPrice: 10, quantity: 2 }],
      method: CreditNoteMethod.GatewayRefund,
      reason: 'Outage',
    })

    const call = callOf(mock)
    expect(call.url).toBe('/api/billing/5/credit-notes')
    expect(call.init?.method).toBe('POST')
    expect(JSON.parse(String(call.init?.body))).toEqual({
      items: [{ description: 'Credit', unitPrice: 10, quantity: 2 }],
      method: 'GatewayRefund',
      reason: 'Outage',
    })
    expect(id).toBe(7)
  })

  it('voidNote posts the reason to the void route', async () => {
    const mock = stubFetch(new Response(null, { status: 204 }))

    await useCreditNotesApi().voidNote(3, 'Mistake')

    const call = callOf(mock)
    expect(call.url).toBe('/api/billing/credit-notes/3/void')
    expect(call.init?.method).toBe('POST')
    expect(JSON.parse(String(call.init?.body))).toEqual({ reason: 'Mistake' })
  })

  it('pdf downloads the file with the name the API gave it', async () => {
    const mock = stubFetch(new Response('%PDF-', { status: 200, headers: { 'Content-Disposition': 'attachment; filename="credit-note-CN-0003.pdf"' } }))

    const file = await useCreditNotesApi().pdf(3)

    expect(callOf(mock).url).toBe('/api/billing/credit-notes/3/pdf')
    expect(file.fileName).toBe('credit-note-CN-0003.pdf')
  })
})
