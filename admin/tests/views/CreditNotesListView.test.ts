/**
 * Mount tests of CreditNotesListView: it lists the notes with their invoice, client, state, method and total, filters by status,
 * says a credit note is not invoice credit, shows the empty sentence, and on a refused load shows the API sentence with a Retry.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import CreditNotesListView from '@/views/CreditNotesListView.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import { CreditNoteMethod, CreditNoteStatus } from '@/types/creditnote'
import { creditNoteFixture, creditNotePage } from '../helpers/creditNoteFixtures'
import { json, problem } from '../helpers/fetch'
import { mountView } from '../helpers/mount'
import { stubRoutes } from '../helpers/routes'

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('CreditNotesListView', () => {
  it('lists the notes with their invoice, client, state, method and total', async () => {
    stubRoutes({
      'GET /api/billing/credit-notes': () =>
        json(creditNotePage([creditNoteFixture(), creditNoteFixture({ id: 4, number: 'CN-0004', method: CreditNoteMethod.GatewayRefund, status: CreditNoteStatus.Voided })])),
    })
    const wrapper = await mountView(CreditNotesListView)

    const table = wrapper.get('[data-testid="billing-credit-notes-table"]').text()
    expect(table).toContain('CN-0003')
    expect(table).toContain('CN-0004')
    expect(table).toContain('Grace Hopper')
    expect(table).toContain('#5')
    expect(table).toContain('Account credit')
    expect(table).toContain('Gateway refund')
    expect(table).toContain('Voided')
    expect(table).toContain('60.00')
  })

  it('says a credit note is not invoice credit', async () => {
    stubRoutes({ 'GET /api/billing/credit-notes': () => json(creditNotePage([])) })
    const wrapper = await mountView(CreditNotesListView)

    expect(wrapper.get('[data-testid="billing-credit-notes-explain"]').text()).toContain('not invoice credit')
  })

  it('reloads with the status when the filter changes', async () => {
    const mock = stubRoutes({ 'GET /api/billing/credit-notes': () => json(creditNotePage([])) })
    const wrapper = await mountView(CreditNotesListView)

    wrapper.findComponent(UiSelect).vm.$emit('update:modelValue', CreditNoteStatus.Voided)
    await flushPromises()

    expect(mock.mock.calls.map(([url]) => String(url)).some((url) => url.includes('status=Voided'))).toBe(true)
  })

  it('shows the empty sentence when there is no note', async () => {
    stubRoutes({ 'GET /api/billing/credit-notes': () => json(creditNotePage([])) })
    const wrapper = await mountView(CreditNotesListView)

    expect(wrapper.get('[data-testid="billing-credit-notes-table"]').text()).toContain('No credit notes found.')
  })

  it('shows the API sentence with a Retry when the load is refused', async () => {
    stubRoutes({ 'GET /api/billing/credit-notes': () => problem('Not allowed.', 403) })
    const wrapper = await mountView(CreditNotesListView)

    expect(wrapper.get('[data-testid="billing-credit-notes-error"]').text()).toContain('Not allowed.')
  })
})
