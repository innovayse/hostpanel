/**
 * Mount tests for ClientInvoicesView: it renders against the real kit and settles on an API
 * refusal with a Retry, a delete is confirmed first and reports its outcome next to the list, and the search, the
 * status and period filters and the page are kept in the address, sent to the API and shown as removable chips.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import UiDateRangePicker from '@/components/ui/UiDateRangePicker.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import ClientInvoicesView from '@/views/ClientInvoicesView.vue'
import schema from '@/views/ClientInvoicesView.schema'
import { renderedCaptions, schemaCaptions, stubPendingFetch } from '../helpers/schemaTable'
import { invoiceRowFixture } from '../helpers/billingFixtures'
import { mountClientView } from '../helpers/clientViews'
import { json, problem } from '../helpers/fetch'
import { mountRouted } from '../helpers/mount'
import { stubRoutes } from '../helpers/routes'
import { useToastStore } from '@/stores/toast'

/** The one-invoice page of client 7. */
const listPage = () => json({ items: [invoiceRowFixture()], pageNumber: 1, pageSize: 25, totalCount: 1, totalPages: 1 })

/**
 * Mounts the screen for client 7 and waits for its first load.
 *
 * @returns The wrapper.
 */
const mountInvoices = async () => {
  const { wrapper } = await mountRouted(ClientInvoicesView, '/clients/:id/invoices', '/clients/7/invoices')
  await flushPromises()
  return wrapper
}

/**
 * Lists the requests the mock has seen for the client's invoice list.
 *
 * @param mock - The stubbed fetch.
 * @returns The request URLs that went to the list endpoint.
 */
const listUrls = (mock: ReturnType<typeof vi.fn>): string[] =>
  mock.mock.calls.map(([url]) => url as string).filter((url) => url.startsWith('/api/billing/client/7?'))

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('ClientInvoicesView', () => {
  it('mounts without throwing', async () => {
    const wrapper = await mountClientView(ClientInvoicesView)

    expect(wrapper.exists()).toBe(true)
  })

  it('renders its main action', async () => {
    const wrapper = await mountClientView(ClientInvoicesView)

    expect(wrapper.find('[data-testid="invoice-create-toggle"]').exists()).toBe(true)
  })

  it('shows the refusal sentence from the API', async () => {
    const wrapper = await mountClientView(ClientInvoicesView, 'Server said no')

    expect(wrapper.text()).toContain('Server said no')
  })

  it('reports a deleted invoice next to the list', async () => {
    stubRoutes({ 'GET /api/billing/client/7': listPage, 'DELETE /api/billing/1': () => new Response(null, { status: 204 }) })
    const wrapper = await mountInvoices()

    // Two or more row actions sit behind the kebab menu, so it is opened first.
    await wrapper.get('[data-testid="client-invoices-actions-1"]').trigger('click')
    await wrapper.get('[data-testid="client-invoices-delete-1"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(useToastStore().toasts.map((toast) => toast.message)).toEqual(['Invoice deleted.'])
  })

  it('shows the API sentence when the delete is refused, without the load error', async () => {
    stubRoutes({ 'GET /api/billing/client/7': listPage, 'DELETE /api/billing/1': () => problem('The invoice has payments.', 409) })
    const wrapper = await mountInvoices()

    // Two or more row actions sit behind the kebab menu, so it is opened first.
    await wrapper.get('[data-testid="client-invoices-actions-1"]').trigger('click')
    await wrapper.get('[data-testid="client-invoices-delete-1"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="client-invoices-feedback"]').text()).toBe('The invoice has payments.')
    expect(wrapper.find('[data-testid="client-invoices-error"]').exists()).toBe(false)
  })

  it('does not delete when the confirmation is cancelled', async () => {
    const mock = stubRoutes({ 'GET /api/billing/client/7': listPage })
    const wrapper = await mountInvoices()

    // Two or more row actions sit behind the kebab menu, so it is opened first.
    await wrapper.get('[data-testid="client-invoices-actions-1"]').trigger('click')
    await wrapper.get('[data-testid="client-invoices-delete-1"]').trigger('click')
    await wrapper.get('[data-testid="dialog-cancel"]').trigger('click')
    await flushPromises()

    expect(mock.mock.calls.some(([, init]) => (init as RequestInit | undefined)?.method === 'DELETE')).toBe(false)
  })

  it('reloads the list from the Retry of a refused load', async () => {
    let refused = true
    stubRoutes({ 'GET /api/billing/client/7': () => (refused ? problem('Billing is disabled.', 403) : listPage()) })
    const wrapper = await mountInvoices()

    refused = false
    await wrapper.get('[data-testid="client-invoices-error-retry"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="client-invoices-error"]').exists()).toBe(false)
  })

  it('prints each invoice total with the code of the currency the invoice is in, like the main list', async () => {
    stubRoutes({
      'GET /api/billing/client/7': () =>
        json({ items: [invoiceRowFixture({ total: 24000, currency: 'AMD' })], pageNumber: 1, pageSize: 25, totalCount: 1, totalPages: 1 }),
    })
    const wrapper = await mountInvoices()

    const text = wrapper.text()
    expect(text).toContain('24000.00 AMD')
    expect(text).not.toContain('$')
  })

  it('restores the search, the filters and the page from the address and sends them together', async () => {
    const mock = stubRoutes({ 'GET /api/billing/client/7': listPage })
    const { wrapper } = await mountRouted(
      ClientInvoicesView,
      '/clients/:id/invoices',
      '/clients/7/invoices?search=wire&status=Paid&from=2026-10-01&to=2026-10-31&page=2',
    )
    await flushPromises()

    expect(listUrls(mock)[0]).toBe('/api/billing/client/7?page=2&pageSize=25&status=Paid&from=2026-10-01&to=2026-10-31&search=wire')
    expect(wrapper.get('[data-testid="filter-trigger-count"]').text()).toBe('3')
    expect(wrapper.get('[data-testid="filter-chip-status"]').text()).toContain('Paid')
  })

  it('puts the typed search in the address and has the server narrow the list after the pause', async () => {
    const mock = stubRoutes({ 'GET /api/billing/client/7': listPage })
    const { wrapper, router } = await mountRouted(ClientInvoicesView, '/clients/:id/invoices', '/clients/7/invoices')
    await flushPromises()

    await wrapper.get('[data-testid="client-invoices-search"]').setValue('card')
    await new Promise((resolve) => setTimeout(resolve, 450))
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ search: 'card' })
    expect(listUrls(mock).at(-1)).toBe('/api/billing/client/7?page=1&pageSize=25&search=card')
  })

  it('sends the status and the period picked in the filter dialog on Apply', async () => {
    const mock = stubRoutes({ 'GET /api/billing/client/7': listPage })
    const { wrapper, router } = await mountRouted(ClientInvoicesView, '/clients/:id/invoices', '/clients/7/invoices')
    await flushPromises()

    await wrapper.get('[data-testid="client-invoices-filter"]').trigger('click')
    await flushPromises()
    wrapper.findComponent(UiSelect).vm.$emit('update:modelValue', 'Overdue')
    wrapper.findComponent(UiDateRangePicker).vm.$emit('update:modelValue', ['2026-10-01', '2026-10-31'])
    await flushPromises()
    await wrapper.get('[data-testid="filter-apply"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ status: 'Overdue', from: '2026-10-01', to: '2026-10-31' })
    expect(listUrls(mock).at(-1)).toBe('/api/billing/client/7?page=1&pageSize=25&status=Overdue&from=2026-10-01&to=2026-10-31')
  })

  it('does not send a status or a date the address holds that is not a known value', async () => {
    const mock = stubRoutes({ 'GET /api/billing/client/7': listPage })
    await mountRouted(ClientInvoicesView, '/clients/:id/invoices', '/clients/7/invoices?status=Gone&from=yesterday')
    await flushPromises()

    expect(listUrls(mock)[0]).toBe('/api/billing/client/7?page=1&pageSize=25')
  })

  it('removes a filter from the address and reloads when its chip is removed', async () => {
    const mock = stubRoutes({ 'GET /api/billing/client/7': listPage })
    const { wrapper, router } = await mountRouted(ClientInvoicesView, '/clients/:id/invoices', '/clients/7/invoices?status=Paid&from=2026-10-01')
    await flushPromises()

    await wrapper.get('[data-testid="filter-chip-remove-status"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ from: '2026-10-01' })
    expect(listUrls(mock).at(-1)).toBe('/api/billing/client/7?page=1&pageSize=25&from=2026-10-01')
  })

  it('keeps the chosen page in the address and loads it', async () => {
    const mock = stubRoutes({ 'GET /api/billing/client/7': () => json({ items: [invoiceRowFixture()], pageNumber: 1, pageSize: 25, totalCount: 60, totalPages: 3 }) })
    const { wrapper, router } = await mountRouted(ClientInvoicesView, '/clients/:id/invoices', '/clients/7/invoices')
    await flushPromises()

    await wrapper.get('[data-testid="client-invoices-pagination-next"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ page: '2' })
    expect(listUrls(mock).at(-1)).toBe('/api/billing/client/7?page=2&pageSize=25')
  })

  it('draws the table head from the schema columns', async () => {
    stubRoutes({ 'GET /api/billing/client/7': () => listPage() })
    const wrapper = await mountInvoices()

    expect(renderedCaptions(wrapper)).toEqual(schemaCaptions(schema.table.columns))
  })

  it('draws the table skeleton while the first load is in flight', async () => {
    stubPendingFetch()
    const { wrapper } = await mountRouted(ClientInvoicesView, '/clients/:id/invoices', '/clients/7/invoices')
    await flushPromises()

    expect(wrapper.find('[data-testid="ui-table-skeleton"]').exists()).toBe(true)
    expect(renderedCaptions(wrapper)).toEqual(schemaCaptions(schema.table.columns))
  })
})
