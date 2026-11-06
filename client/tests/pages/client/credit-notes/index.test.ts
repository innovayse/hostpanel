/**
 * Tests for `pages/client/credit-notes/index.vue`: the explanation that a credit note is not invoice credit, the table of the
 * client's own notes, the empty state, the failure state and the PDF download.
 *
 * @module tests/pages/client/credit-notes/index.test
 */

import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { mountPage, stubClientRuntime } from '~/tests/helpers/mountClient'
import CreditNotesPage from '~/pages/client/credit-notes/index.vue'

/** The mocked billing endpoints the credit notes store reaches. */
const billingApi = vi.hoisted(() => ({
  fetchCreditNotes: vi.fn(),
  fetchCreditNotePdf: vi.fn()
}))

/** The mocked file save. */
const saveBlob = vi.hoisted(() => vi.fn())

vi.mock('~/composables/apis/useBillingApi', () => ({ useBillingApi: () => billingApi }))
vi.mock('~/utils/saveBlob', () => ({ saveBlob }))

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

describe('credit notes page', () => {
  beforeEach(() => {
    billingApi.fetchCreditNotes.mockReset()
    billingApi.fetchCreditNotePdf.mockReset()
    saveBlob.mockReset()
    stubClientRuntime()
  })

  it('reads the notes and lists them, saying a credit note is not the credit balance', async () => {
    billingApi.fetchCreditNotes.mockResolvedValue([note(4), note(3)])

    const wrapper = await mountPage(CreditNotesPage)

    expect(billingApi.fetchCreditNotes).toHaveBeenCalledTimes(1)
    expect(wrapper.get('[data-testid="credit-notes-explain"]').text()).toBe('client.creditNotes.explain')
    expect(wrapper.find('[data-testid="credit-note-row-4"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="credit-note-row-3"]').exists()).toBe(true)
  })

  it('shows the empty state when the client has no note', async () => {
    billingApi.fetchCreditNotes.mockResolvedValue([])

    const wrapper = await mountPage(CreditNotesPage)

    expect(wrapper.find('[data-testid="credit-notes-empty"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="credit-notes-table"]').exists()).toBe(false)
  })

  it('shows the failure and no table when the read is refused', async () => {
    billingApi.fetchCreditNotes.mockRejectedValue({ data: { code: 'CLIENT_PROFILE_NOT_FOUND' }, statusMessage: 'No client profile.' })

    const wrapper = await mountPage(CreditNotesPage)

    expect(wrapper.find('[data-testid="credit-notes-error"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="credit-notes-table"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="credit-notes-empty"]').exists()).toBe(false)
  })

  it('downloads the PDF of the note whose button is pressed', async () => {
    billingApi.fetchCreditNotes.mockResolvedValue([note(3)])
    const pdf = new Blob(['%PDF-'])
    billingApi.fetchCreditNotePdf.mockResolvedValue(pdf)
    const wrapper = await mountPage(CreditNotesPage)

    await wrapper.get('[data-testid="credit-note-download-3"]').trigger('click')
    await flushPromises()

    expect(billingApi.fetchCreditNotePdf).toHaveBeenCalledWith(3)
    expect(saveBlob).toHaveBeenCalledWith(pdf, 'credit-note-CN-0003.pdf')
  })

  it('shows the sentence of a refused download above the table', async () => {
    billingApi.fetchCreditNotes.mockResolvedValue([note(3)])
    billingApi.fetchCreditNotePdf.mockRejectedValue({ data: { code: 'CREDIT_NOTE_NOT_FOUND' }, statusMessage: 'The credit note was not found.' })
    const wrapper = await mountPage(CreditNotesPage)

    await wrapper.get('[data-testid="credit-note-download-3"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="credit-notes-download-error"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="credit-notes-table"]').exists()).toBe(true)
  })
})
