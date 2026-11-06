/**
 * Unit tests for CreditNotesTable: one row per note with its number, invoice link, where the value went, total and state, a download
 * button per row that asks the parent for the PDF, and the busy state of the row being downloaded.
 *
 * @module tests/components/client/creditnotes/Table.test
 */

import { describe, expect, it } from 'vitest'
import { mountClient } from '~/tests/helpers/mountClient'
import CreditNotesTable from '~/components/client/creditnotes/Table.vue'
import type { ClientCreditNote } from '~/types/clientcreditnote'

/**
 * Builds a note.
 *
 * @param overrides - Fields that differ from the default.
 * @returns The note.
 */
const note = (overrides: Partial<ClientCreditNote> = {}): ClientCreditNote => ({
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
  voidedAt: null,
  ...overrides
})

describe('CreditNotesTable', () => {
  it('lists one row per note in the order the backend sent them', () => {
    const wrapper = mountClient(CreditNotesTable, { props: { notes: [note({ id: 4, number: 'CN-0004' }), note()], downloadingId: null } })

    const html = wrapper.html()
    expect(html.indexOf('credit-note-row-4')).toBeLessThan(html.indexOf('credit-note-row-3'))
    expect(wrapper.get('[data-testid="credit-note-number-3"]').text()).toBe('CN-0003')
  })

  it('links the invoice the note credits and names where the value went and the state', () => {
    const wrapper = mountClient(CreditNotesTable, { props: { notes: [note({ method: 'GatewayRefund', status: 'Voided' })], downloadingId: null } })

    expect(wrapper.get('[data-testid="credit-note-invoice-3"]').attributes('href')).toBe('/client/invoices/12')
    expect(wrapper.get('[data-testid="credit-note-method-3"]').text()).toBe('client.creditNotes.method.GatewayRefund')
    expect(wrapper.get('[data-testid="credit-note-status-3"]').text()).toBe('client.creditNotes.status.Voided')
  })

  it('shows the total in the currency of the note', () => {
    const wrapper = mountClient(CreditNotesTable, { props: { notes: [note({ currency: 'USD', total: 60 })], downloadingId: null } })

    expect(wrapper.get('[data-testid="credit-note-total-3"]').text()).toContain('60.00')
  })

  it('asks for the PDF of the row whose button is pressed', async () => {
    const wrapper = mountClient(CreditNotesTable, { props: { notes: [note({ id: 4, number: 'CN-0004' }), note()], downloadingId: null } })

    await wrapper.get('[data-testid="credit-note-download-4"]').trigger('click')

    expect(wrapper.emitted('download')?.[0]).toEqual([expect.objectContaining({ id: 4, number: 'CN-0004' })])
  })

  it('marks only the row being downloaded as busy', () => {
    const wrapper = mountClient(CreditNotesTable, { props: { notes: [note({ id: 4, number: 'CN-0004' }), note()], downloadingId: 4 }, })

    expect(wrapper.get('[data-testid="credit-note-download-4"]').attributes('disabled')).toBeDefined()
    expect(wrapper.get('[data-testid="credit-note-download-3"]').attributes('disabled')).toBeUndefined()
  })
})
