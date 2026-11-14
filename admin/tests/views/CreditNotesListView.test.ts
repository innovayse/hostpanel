/**
 * Mount tests of CreditNotesListView: it lists the notes with their invoice, client, state, method and total, keeps the search,
 * the client, status and method filters and the page in the address and sends them to the API, says a credit note is not
 * invoice credit, shows the empty sentence, and on a refused load shows the API sentence with a Retry.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import CreditNotesListView from '@/views/CreditNotesListView.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import { CreditNoteMethod, CreditNoteStatus } from '@/types/creditnote'
import { creditNoteFixture, creditNotePage } from '../helpers/creditNoteFixtures'
import { json, problem } from '../helpers/fetch'
import { mountRouted, mountView } from '../helpers/mount'
import { stubRoutes } from '../helpers/routes'

/**
 * Lists the requests the mock has seen for the credit note list.
 *
 * @param mock - The stubbed fetch.
 * @returns The request URLs that went to the list endpoint.
 */
const listUrls = (mock: ReturnType<typeof vi.fn>): string[] =>
  mock.mock.calls.map(([url]) => url as string).filter((url) => url.startsWith('/api/billing/credit-notes?'))

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

  it('sends the status and the method picked in the filter dialog on Apply and shows them as chips', async () => {
    const mock = stubRoutes({ 'GET /api/billing/credit-notes': () => json(creditNotePage([])) })
    const { wrapper, router } = await mountRouted(CreditNotesListView, '/billing/credit-notes', '/billing/credit-notes')
    await flushPromises()

    await wrapper.get('[data-testid="billing-credit-notes-filter"]').trigger('click')
    await flushPromises()
    const selects = wrapper.findAllComponents(UiSelect)
    selects.find((select) => select.attributes('data-testid') === 'billing-credit-notes-filter-status')?.vm.$emit('update:modelValue', CreditNoteStatus.Voided)
    selects.find((select) => select.attributes('data-testid') === 'billing-credit-notes-filter-method')?.vm.$emit('update:modelValue', CreditNoteMethod.GatewayRefund)
    await flushPromises()
    await wrapper.get('[data-testid="filter-apply"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ status: 'Voided', method: 'GatewayRefund' })
    expect(listUrls(mock).at(-1)).toBe('/api/billing/credit-notes?status=Voided&method=GatewayRefund&page=1&pageSize=20')
    expect(wrapper.get('[data-testid="filter-chip-status"]').text()).toContain('Voided')
    expect(wrapper.get('[data-testid="filter-chip-method"]').text()).toContain('Gateway refund')
  })

  it('puts the typed search in the address and has the server narrow the list after the pause', async () => {
    const mock = stubRoutes({ 'GET /api/billing/credit-notes': () => json(creditNotePage([])) })
    const { wrapper, router } = await mountRouted(CreditNotesListView, '/billing/credit-notes', '/billing/credit-notes')
    await flushPromises()

    await wrapper.get('[data-testid="billing-credit-notes-search"]').setValue('CN-0004')
    await new Promise((resolve) => setTimeout(resolve, 450))
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ search: 'CN-0004' })
    expect(listUrls(mock).at(-1)).toBe('/api/billing/credit-notes?search=CN-0004&page=1&pageSize=20')
  })

  it('restores the search, the client, the filters and the page from the address and sends them together', async () => {
    const mock = stubRoutes({ 'GET /api/billing/credit-notes': () => json(creditNotePage([])) })
    const { wrapper } = await mountRouted(
      CreditNotesListView,
      '/billing/credit-notes',
      '/billing/credit-notes?search=refund&clientId=7&status=Applied&method=AccountCredit&page=2',
    )
    await flushPromises()

    expect(listUrls(mock)[0]).toBe('/api/billing/credit-notes?search=refund&clientId=7&status=Applied&method=AccountCredit&page=2&pageSize=20')
    expect(wrapper.get('[data-testid="filter-trigger-count"]').text()).toBe('3')
  })

  it('does not send a filter the address holds that is not a known value', async () => {
    const mock = stubRoutes({ 'GET /api/billing/credit-notes': () => json(creditNotePage([])) })
    await mountRouted(CreditNotesListView, '/billing/credit-notes', '/billing/credit-notes?clientId=abc&status=Gone&method=Sideways')
    await flushPromises()

    expect(listUrls(mock)[0]).toBe('/api/billing/credit-notes?page=1&pageSize=20')
  })

  it('removes a filter from the address and reloads when its chip is removed', async () => {
    const mock = stubRoutes({ 'GET /api/billing/credit-notes': () => json(creditNotePage([])) })
    const { wrapper, router } = await mountRouted(CreditNotesListView, '/billing/credit-notes', '/billing/credit-notes?status=Voided&method=GatewayRefund')
    await flushPromises()

    await wrapper.get('[data-testid="filter-chip-remove-status"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ method: 'GatewayRefund' })
    expect(listUrls(mock).at(-1)).toBe('/api/billing/credit-notes?method=GatewayRefund&page=1&pageSize=20')
  })

  it('keeps the chosen page in the address and loads it', async () => {
    const mock = stubRoutes({ 'GET /api/billing/credit-notes': () => json({ ...creditNotePage([creditNoteFixture()]), totalCount: 60 }) })
    const { wrapper, router } = await mountRouted(CreditNotesListView, '/billing/credit-notes', '/billing/credit-notes')
    await flushPromises()

    await wrapper.get('[data-testid="billing-credit-notes-pagination-next"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ page: '2' })
    expect(listUrls(mock).at(-1)).toBe('/api/billing/credit-notes?page=2&pageSize=20')
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
