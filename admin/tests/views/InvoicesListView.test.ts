/**
 * Mount tests for InvoicesListView: the table renders what the API sent, ticking rows opens the
 * bulk bar, a bulk action goes through the store and reports its outcome, deleting and cancelling
 * are confirmed first, and a refused load shows the API sentence with a Retry.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import InvoicesListView from '@/views/InvoicesListView.vue'
import { invoiceRowFixture } from '../helpers/billingFixtures'
import { json, problem } from '../helpers/fetch'
import { mountRouted } from '../helpers/mount'
import { requestsOf, stubRoutes } from '../helpers/routes'

/**
 * Mounts the list and waits for its first load.
 *
 * @returns The wrapper.
 */
const mountList = async () => {
  const { wrapper } = await mountRouted(InvoicesListView, '/billing/invoices', '/billing/invoices')
  await flushPromises()
  return wrapper
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('InvoicesListView', () => {
  it('renders a row per invoice with its client, status and links', async () => {
    stubRoutes({ 'GET /api/billing?': () => json({ items: [invoiceRowFixture()], pageNumber: 1, pageSize: 25, totalCount: 1, totalPages: 1 }) })
    const wrapper = await mountList()

    const rows = wrapper.findAll('[data-testid="billing-invoices-table"] tbody tr')
    expect(rows).toHaveLength(1)
    expect(rows[0]?.text()).toContain('Ada Lovelace')
    expect(rows[0]?.text()).toContain('Unpaid')
  })

  it('opens the view and edit pages from the row actions menu', async () => {
    stubRoutes({ 'GET /api/billing?': () => json({ items: [invoiceRowFixture()], pageNumber: 1, pageSize: 25, totalCount: 1, totalPages: 1 }) })
    const { wrapper, router } = await mountRouted(InvoicesListView, '/billing/invoices', '/billing/invoices')
    await flushPromises()

    // Two or more row actions sit behind the kebab menu, so it is opened first.
    await wrapper.get('[data-testid="billing-invoice-actions-1"]').trigger('click')
    const view = wrapper.get('[data-testid="billing-invoice-view-1"]')
    // Real links, so a new tab and a middle click open the same pages.
    expect(view.element.tagName).toBe('A')
    expect(view.attributes('href')).toBe('/billing/invoices/1/view')
    expect(wrapper.get('[data-testid="billing-invoice-edit-1"]').attributes('href')).toBe('/billing/invoices/1/edit')
    await view.trigger('click')
    await flushPromises()
    expect(router.currentRoute.value.fullPath).toBe('/billing/invoices/1/view')

    await router.push('/billing/invoices')
    await flushPromises()
    await wrapper.get('[data-testid="billing-invoice-actions-1"]').trigger('click')
    await wrapper.get('[data-testid="billing-invoice-edit-1"]').trigger('click')
    await flushPromises()
    expect(router.currentRoute.value.fullPath).toBe('/billing/invoices/1/edit')
  })

  it('opens the bulk bar when a row is ticked and runs the action', async () => {
    const mock = stubRoutes({
      'GET /api/billing?': () => json({ items: [invoiceRowFixture()], pageNumber: 1, pageSize: 25, totalCount: 1, totalPages: 1 }),
      'POST /api/billing/bulk': () => new Response(null, { status: 204 }),
    })
    const wrapper = await mountList()
    expect(wrapper.find('[data-testid="billing-invoices-bulk"]').exists()).toBe(false)

    await wrapper.get('[data-testid="billing-invoice-select-1"]').trigger('click')
    await wrapper.get('[data-testid="billing-invoices-bulk-paid"]').trigger('click')
    await flushPromises()

    const post = mock.mock.calls.find(([url]) => String(url).endsWith('/billing/bulk'))
    expect(JSON.parse(String((post?.[1] as RequestInit | undefined)?.body))).toEqual({ invoiceIds: [1], action: 'MarkPaid' })
  })

  it('drops the ticked invoices when the list is loaded again', async () => {
    stubRoutes({ 'GET /api/billing?': () => json({ items: [invoiceRowFixture()], pageNumber: 1, pageSize: 25, totalCount: 1, totalPages: 1 }) })
    const { wrapper, router } = await mountRouted(InvoicesListView, '/billing/invoices', '/billing/invoices')
    await flushPromises()
    await wrapper.get('[data-testid="billing-invoice-select-1"]').trigger('click')
    expect(wrapper.find('[data-testid="billing-invoices-bulk"]').exists()).toBe(true)

    await router.push({ query: { status: 'Paid' } })
    await flushPromises()
    expect(wrapper.find('[data-testid="billing-invoices-bulk"]').exists()).toBe(false)
  })

  it('loads the list with the filters and the page held in the address and shows the filters as chips', async () => {
    const mock = stubRoutes({ 'GET /api/billing?': () => json({ items: [invoiceRowFixture()], pageNumber: 2, pageSize: 25, totalCount: 40, totalPages: 2 }) })
    const { wrapper } = await mountRouted(InvoicesListView, '/billing/invoices', '/billing/invoices?status=Paid&from=2026-10-01&page=2')
    await flushPromises()

    const request = requestsOf(mock).find(url => url.includes('/billing?')) ?? ''
    expect(request).toContain('status=Paid')
    expect(request).toContain('from=2026-10-01')
    expect(request).toContain('page=2')
    expect(wrapper.get('[data-testid="filter-chip-status"]').text()).toContain('Paid')
    expect(wrapper.get('[data-testid="filter-chip-from"]').text()).toContain('01/10/2026')
    expect(wrapper.get('[data-testid="filter-trigger-count"]').text()).toBe('2')
  })

  it('clears a filter from its chip and loads the list again without it', async () => {
    const mock = stubRoutes({ 'GET /api/billing?': () => json({ items: [invoiceRowFixture()], pageNumber: 1, pageSize: 25, totalCount: 1, totalPages: 1 }) })
    const { wrapper, router } = await mountRouted(InvoicesListView, '/billing/invoices', '/billing/invoices?status=Paid')
    await flushPromises()

    await wrapper.get('[data-testid="filter-chip-remove-status"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query.status).toBeUndefined()
    expect(requestsOf(mock).filter(url => url.includes('/billing?')).at(-1)).not.toContain('status=')
  })

  it('narrows the page by the search text without asking the server again', async () => {
    const mock = stubRoutes({
      'GET /api/billing?': () =>
        json({
          items: [invoiceRowFixture(), invoiceRowFixture({ id: 2, clientName: 'Grace Hopper' })],
          pageNumber: 1,
          pageSize: 25,
          totalCount: 2,
          totalPages: 1,
        }),
    })
    const wrapper = await mountList()

    await wrapper.get('[data-testid="billing-invoices-search"]').setValue('grace')

    expect(wrapper.findAll('[data-testid="billing-invoices-table"] tbody tr')).toHaveLength(1)
    expect(requestsOf(mock).filter(url => url.includes('/billing?'))).toHaveLength(1)
  })

  it('sums the page into paid, due and overdue tiles', async () => {
    stubRoutes({
      'GET /api/billing?': () =>
        json({
          items: [
            invoiceRowFixture({ id: 1, status: 'Paid', total: 40 }),
            invoiceRowFixture({ id: 2, status: 'Paid', total: 10 }),
            invoiceRowFixture({ id: 3, status: 'Unpaid', total: 25 }),
            invoiceRowFixture({ id: 4, status: 'Overdue', total: 7 }),
          ],
          pageNumber: 1,
          pageSize: 25,
          totalCount: 4,
          totalPages: 1,
        }),
    })
    const wrapper = await mountList()

    expect(wrapper.get('[data-testid="billing-invoices-summary-paid"]').text()).toContain('50.00 USD')
    expect(wrapper.get('[data-testid="billing-invoices-summary-paid"]').text()).toContain('2 invoices on this page')
    expect(wrapper.get('[data-testid="billing-invoices-summary-due"]').text()).toContain('25.00 USD')
    expect(wrapper.get('[data-testid="billing-invoices-summary-overdue"]').text()).toContain('7.00 USD')
  })

  it('deletes an invoice only after the confirmation', async () => {
    const mock = stubRoutes({
      'GET /api/billing?': () => json({ items: [invoiceRowFixture()], pageNumber: 1, pageSize: 25, totalCount: 1, totalPages: 1 }),
      'DELETE /api/billing/1': () => new Response(null, { status: 204 }),
    })
    const wrapper = await mountList()

    // Two or more row actions sit behind the kebab menu, so it is opened first.
    await wrapper.get('[data-testid="billing-invoice-actions-1"]').trigger('click')
    await wrapper.get('[data-testid="billing-invoice-delete-1"]').trigger('click')
    expect(mock.mock.calls.some(([, init]) => (init as RequestInit | undefined)?.method === 'DELETE')).toBe(false)
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(mock.mock.calls.some(([, init]) => (init as RequestInit | undefined)?.method === 'DELETE')).toBe(true)
  })

  it('shows the API sentence when the load is refused', async () => {
    stubRoutes({ 'GET /api/billing?': () => problem('Billing is disabled.', 403) })
    const wrapper = await mountList()

    expect(wrapper.get('[data-testid="billing-invoices-error"]').text()).toContain('Billing is disabled.')
  })

  it('prints each invoice total in the currency the invoice is issued in', async () => {
    stubRoutes({
      'GET /api/billing?': () =>
        json({
          items: [invoiceRowFixture({ id: 1, total: 12.5 }), invoiceRowFixture({ id: 2, total: 53160, currency: 'AMD' })],
          pageNumber: 1,
          pageSize: 25,
          totalCount: 2,
          totalPages: 1,
        }),
    })
    const wrapper = await mountList()

    const rows = wrapper.findAll('[data-testid="billing-invoices-table"] tbody tr')
    expect(rows[0]?.text()).toContain('12.50 USD')
    expect(rows[1]?.text()).toContain('53160.00 AMD')
    expect(rows[1]?.text()).not.toContain('USD')
  })

  /** The one-invoice page the list loads. */
  const listPage = () => json({ items: [invoiceRowFixture()], pageNumber: 1, pageSize: 25, totalCount: 1, totalPages: 1 })

  it('reports a deleted invoice above the table', async () => {
    stubRoutes({ 'GET /api/billing?': listPage, 'DELETE /api/billing/1': () => new Response(null, { status: 204 }) })
    const wrapper = await mountList()

    // Two or more row actions sit behind the kebab menu, so it is opened first.
    await wrapper.get('[data-testid="billing-invoice-actions-1"]').trigger('click')
    await wrapper.get('[data-testid="billing-invoice-delete-1"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="billing-invoices-feedback"]').text()).toBe('Invoice deleted.')
  })

  it('shows the API sentence when the delete is refused, without the load error', async () => {
    stubRoutes({ 'GET /api/billing?': listPage, 'DELETE /api/billing/1': () => problem('The invoice has payments.', 409) })
    const wrapper = await mountList()

    // Two or more row actions sit behind the kebab menu, so it is opened first.
    await wrapper.get('[data-testid="billing-invoice-actions-1"]').trigger('click')
    await wrapper.get('[data-testid="billing-invoice-delete-1"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="billing-invoices-feedback"]').text()).toBe('The invoice has payments.')
    expect(wrapper.find('[data-testid="billing-invoices-error"]').exists()).toBe(false)
  })

  it('does not delete when the confirmation is cancelled', async () => {
    const mock = stubRoutes({ 'GET /api/billing?': listPage })
    const wrapper = await mountList()

    // Two or more row actions sit behind the kebab menu, so it is opened first.
    await wrapper.get('[data-testid="billing-invoice-actions-1"]').trigger('click')
    await wrapper.get('[data-testid="billing-invoice-delete-1"]').trigger('click')
    await wrapper.get('[data-testid="dialog-cancel"]').trigger('click')
    await flushPromises()

    expect(mock.mock.calls.some(([, init]) => (init as RequestInit | undefined)?.method === 'DELETE')).toBe(false)
  })

  it('cancels the ticked invoices only after the confirmation', async () => {
    const mock = stubRoutes({ 'GET /api/billing?': listPage, 'POST /api/billing/bulk': () => new Response(null, { status: 204 }) })
    const wrapper = await mountList()

    await wrapper.get('[data-testid="billing-invoice-select-1"]').trigger('click')
    await wrapper.get('[data-testid="billing-invoices-bulk-cancelled"]').trigger('click')
    expect(mock.mock.calls.some(([url]) => String(url).endsWith('/billing/bulk'))).toBe(false)
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    const post = mock.mock.calls.find(([url]) => String(url).endsWith('/billing/bulk'))
    expect(JSON.parse(String((post?.[1] as RequestInit | undefined)?.body))).toEqual({ invoiceIds: [1], action: 'MarkCancelled' })
    expect(wrapper.get('[data-testid="billing-invoices-feedback"]').text()).toBe('Done for the selected invoices.')
  })

  it('shows the API sentence when a bulk action is refused', async () => {
    stubRoutes({ 'GET /api/billing?': listPage, 'POST /api/billing/bulk': () => problem('One invoice is locked.', 409) })
    const wrapper = await mountList()

    await wrapper.get('[data-testid="billing-invoice-select-1"]').trigger('click')
    await wrapper.get('[data-testid="billing-invoices-bulk-paid"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="billing-invoices-feedback"]').text()).toBe('One invoice is locked.')
  })

  it('reloads the list from the Retry of a refused load', async () => {
    let refused = true
    stubRoutes({ 'GET /api/billing?': () => (refused ? problem('Billing is disabled.', 403) : listPage()) })
    const wrapper = await mountList()

    refused = false
    await wrapper.get('[data-testid="billing-invoices-error-retry"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="billing-invoices-error"]').exists()).toBe(false)
  })
})
