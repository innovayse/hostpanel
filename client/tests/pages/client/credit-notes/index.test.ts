/**
 * Tests for `pages/client/credit-notes/index.vue` against the real credit notes store, the real
 * billing API composable and the real transport, with a stub BFF answering on a real socket
 * (nothing of ours is mocked): the explanation that a credit note is not invoice credit, the table
 * of the client's own notes, the empty state, the failure state and the PDF download.
 *
 * @module tests/pages/client/credit-notes/index.test
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { captureDownloads, PDF_REPLY } from '~/tests/helpers/captureDownloads'
import { stubClientRuntime } from '~/tests/helpers/mountClient'
import { mountRenderedClientPage } from '~/tests/helpers/renderedPage'
import { requestsTo, routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import { PortalErrorCode } from '~/constants/portalErrorCode'
import CreditNotesPage from '~/pages/client/credit-notes/index.vue'

/** The BFF paths the page reads. */
const PATHS = {
  notes: '/api/portal/client/credit-notes',
  pdf: '/api/portal/client/credit-notes/3/pdf',
} as const

/** The `data-testid` of the page's root, on screen once its read has ended. */
const PAGE_ROOT = 'client-credit-notes'

/** The stub playing the BFF. */
let stub: StubServer

/** The replies of the current test; a test fills the ones it needs before it mounts. */
let table: StubRouteTable

/**
 * Builds a note.
 *
 * @param id - The note id.
 * @returns The note.
 */
const note = (id: number) => ({
  id,
  number: `CN-000${id}`,
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
})

beforeEach(async () => {
  stubClientRuntime()
  table = {}
  stub = await startTransport(routeTable(table))
})

afterEach(async () => {
  vi.restoreAllMocks()
  await stub.close()
})

describe('credit notes page', () => {
  it('reads the notes and lists them, saying a credit note is not the credit balance', async () => {
    table[`GET ${PATHS.notes}`] = { body: [note(4), note(3)] }

    const wrapper = await mountRenderedClientPage(CreditNotesPage, PAGE_ROOT)

    expect(requestsTo(stub.requests, PATHS.notes)).toHaveLength(1)
    expect(wrapper.get('[data-testid="credit-notes-explain"]').text()).toBe('client.creditNotes.explain')
    expect(wrapper.find('[data-testid="credit-note-row-4"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="credit-note-row-3"]').exists()).toBe(true)
  })

  it('shows the empty state when the client has no note', async () => {
    table[`GET ${PATHS.notes}`] = { body: [] }

    const wrapper = await mountRenderedClientPage(CreditNotesPage, PAGE_ROOT)

    expect(wrapper.find('[data-testid="credit-notes-empty"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="credit-notes-table"]').exists()).toBe(false)
  })

  it('shows the failure and no table when the read is refused', async () => {
    table[`GET ${PATHS.notes}`] = { status: 404, body: { type: PortalErrorCode.ClientProfileNotFound, detail: 'No client profile.' } }

    const wrapper = await mountRenderedClientPage(CreditNotesPage, PAGE_ROOT)

    expect(wrapper.find('[data-testid="credit-notes-error"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="credit-notes-table"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="credit-notes-empty"]').exists()).toBe(false)
  })

  it('downloads the PDF of the note whose button is pressed', async () => {
    table[`GET ${PATHS.notes}`] = { body: [note(3)] }
    table[`GET ${PATHS.pdf}`] = PDF_REPLY
    const downloads = captureDownloads()
    const wrapper = await mountRenderedClientPage(CreditNotesPage, PAGE_ROOT)

    await wrapper.get('[data-testid="credit-note-download-3"]').trigger('click')
    await vi.waitFor(() => expect(downloads).toHaveLength(1))

    expect(requestsTo(stub.requests, PATHS.pdf)).toHaveLength(1)
    expect(downloads[0]?.fileName).toBe('credit-note-CN-0003.pdf')
    expect(await downloads[0]?.blob.text()).toBe('%PDF-')
  })

  it('shows the sentence of a refused download above the table', async () => {
    table[`GET ${PATHS.notes}`] = { body: [note(3)] }
    table[`GET ${PATHS.pdf}`] = { status: 404, body: { type: 'CREDIT_NOTE_NOT_FOUND', detail: 'The credit note was not found.' } }
    const downloads = captureDownloads()
    const wrapper = await mountRenderedClientPage(CreditNotesPage, PAGE_ROOT)

    await wrapper.get('[data-testid="credit-note-download-3"]').trigger('click')
    await vi.waitFor(() => expect(wrapper.find('[data-testid="credit-notes-download-error"]').exists()).toBe(true))

    expect(wrapper.get('[data-testid="credit-notes-download-error"]').text()).toBe('The credit note was not found.')
    expect(wrapper.find('[data-testid="credit-notes-table"]').exists()).toBe(true)
    expect(downloads).toHaveLength(0)
  })
})
