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
import { stubRoutes } from '../helpers/routes'

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
    expect(wrapper.get('[data-testid="billing-invoice-view-1"]').attributes('href')).toBe('/billing/invoices/1/view')
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
    const wrapper = await mountList()
    await wrapper.get('[data-testid="billing-invoice-select-1"]').trigger('click')
    await wrapper.get('[data-testid="billing-invoices-filter-toggle"]').trigger('click')
    await wrapper.get('[data-testid="billing-invoices-search"]').trigger('click')
    await flushPromises()
    expect(wrapper.find('[data-testid="billing-invoices-bulk"]').exists()).toBe(false)
  })

  it('deletes an invoice only after the confirmation', async () => {
    const mock = stubRoutes({
      'GET /api/billing?': () => json({ items: [invoiceRowFixture()], pageNumber: 1, pageSize: 25, totalCount: 1, totalPages: 1 }),
      'DELETE /api/billing/1': () => new Response(null, { status: 204 }),
    })
    const wrapper = await mountList()

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

    await wrapper.get('[data-testid="billing-invoice-delete-1"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="billing-invoices-feedback"]').text()).toBe('Invoice deleted.')
  })

  it('shows the API sentence when the delete is refused, without the load error', async () => {
    stubRoutes({ 'GET /api/billing?': listPage, 'DELETE /api/billing/1': () => problem('The invoice has payments.', 409) })
    const wrapper = await mountList()

    await wrapper.get('[data-testid="billing-invoice-delete-1"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="billing-invoices-feedback"]').text()).toBe('The invoice has payments.')
    expect(wrapper.find('[data-testid="billing-invoices-error"]').exists()).toBe(false)
  })

  it('does not delete when the confirmation is cancelled', async () => {
    const mock = stubRoutes({ 'GET /api/billing?': listPage })
    const wrapper = await mountList()

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
