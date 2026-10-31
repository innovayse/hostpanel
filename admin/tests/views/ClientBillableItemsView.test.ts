/**
 * Mount tests for ClientBillableItemsView: it renders against the real kit and settles on an API
 * refusal with a Retry, and a delete is confirmed first and reports its outcome next to the lists.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import ClientBillableItemsView from '@/views/ClientBillableItemsView.vue'
import { mountClientView } from '../helpers/clientViews'
import { json, problem } from '../helpers/fetch'
import { mountRouted } from '../helpers/mount'
import { stubRoutes } from '../helpers/routes'

/** The items of client 7, in the API's `ClientBillableItemDto` shape: one uninvoiced charge and optionally invoiced ones, in the given currency. */
const items = (currency = 'USD', invoiced: object[] = []) =>
  json({
    uninvoicedItems: [{
      id: 5,
      clientId: 7,
      description: 'Domain renewal',
      amount: 12,
      currency,
      type: 'OneTime',
      recurringPeriod: null,
      isInvoiced: false,
      invoiceId: null,
      nextDueDate: null,
      createdAt: '2026-10-01T00:00:00Z',
    }],
    uninvoicedTotal: [{ amount: 12, currency }],
    invoicedItems: { items: invoiced, pageNumber: 1, pageSize: 25, totalCount: invoiced.length, totalPages: 1 },
  })

/**
 * Mounts the screen for client 7 and waits for its first load.
 *
 * @returns The wrapper.
 */
const mountItems = async () => {
  const { wrapper } = await mountRouted(ClientBillableItemsView, '/clients/:id/billable-items', '/clients/7/billable-items')
  await flushPromises()
  return wrapper
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('ClientBillableItemsView', () => {
  it('mounts without throwing', async () => {
    const wrapper = await mountClientView(ClientBillableItemsView)

    expect(wrapper.exists()).toBe(true)
  })

  it('renders its main action', async () => {
    const wrapper = await mountClientView(ClientBillableItemsView)

    expect(wrapper.find('[data-testid="client-billable-add"]').exists()).toBe(true)
  })

  it('shows the refusal sentence from the API', async () => {
    const wrapper = await mountClientView(ClientBillableItemsView, 'Server said no')

    expect(wrapper.text()).toContain('Server said no')
  })

  it('shows only the columns the API sends for an uninvoiced item', async () => {
    stubRoutes({ 'GET /api/clients/7/billable-items': items })
    const wrapper = await mountItems()

    const table = wrapper.get('[data-testid="client-billable-uninvoiced-table"]')
    const headers = table.findAll('th').map((th) => th.text())
    expect(headers).toEqual(['', 'ID', 'Description', 'Amount', 'Actions'])
    expect(table.text()).toContain('Domain renewal')
  })

  it('writes amounts in the currency the items carry, not in dollars', async () => {
    const invoiced = { id: 6, clientId: 7, description: 'Hosting', amount: 5000, currency: 'AMD', type: 'OneTime', recurringPeriod: null, isInvoiced: true, invoiceId: 9, nextDueDate: null, createdAt: '2026-09-01T00:00:00Z' }
    stubRoutes({ 'GET /api/clients/7/billable-items': () => items('AMD', [invoiced]) })
    const wrapper = await mountItems()

    expect(wrapper.get('[data-testid="client-billable-uninvoiced-table"]').text()).toContain('12.00 AMD')
    expect(wrapper.get('[data-testid="client-billable-invoiced-table"]').text()).toContain('5000.00 AMD')
    expect(wrapper.text()).toContain('Uninvoiced Items — 12.00 AMD (1)')
    expect(wrapper.text()).not.toContain('$')
    expect(wrapper.text()).not.toContain('USD')
  })

  it('shows one total per currency when the items are in two currencies', async () => {
    stubRoutes({
      'GET /api/clients/7/billable-items': () => json({
        uninvoicedItems: [],
        uninvoicedTotal: [{ amount: 12, currency: 'USD' }, { amount: 5000, currency: 'AMD' }],
        invoicedItems: { items: [], pageNumber: 1, pageSize: 25, totalCount: 0, totalPages: 1 },
      }),
    })
    const wrapper = await mountItems()

    expect(wrapper.text()).toContain('12.00 USD, 5000.00 AMD')
  })

  it('shows the count without an amount when there are no uninvoiced items', async () => {
    stubRoutes({
      'GET /api/clients/7/billable-items': () => json({
        uninvoicedItems: [],
        uninvoicedTotal: [],
        invoicedItems: { items: [], pageNumber: 1, pageSize: 25, totalCount: 0, totalPages: 1 },
      }),
    })
    const wrapper = await mountItems()

    expect(wrapper.text()).toContain('Uninvoiced Items (0)')
    expect(wrapper.text()).not.toContain('0.00')
  })

  it('reports a deleted item next to the lists', async () => {
    stubRoutes({ 'DELETE /api/clients/7/billable-items/5': () => new Response(null, { status: 204 }), 'GET /api/clients/7/billable-items': items })
    const wrapper = await mountItems()

    await wrapper.get('[data-testid="client-billable-delete-5"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="client-billable-feedback"]').text()).toBe('Billable items deleted.')
  })

  it('shows the API sentence when the delete is refused and keeps the lists', async () => {
    stubRoutes({ 'DELETE /api/clients/7/billable-items/5': () => problem('The item is already invoiced.', 409), 'GET /api/clients/7/billable-items': items })
    const wrapper = await mountItems()

    await wrapper.get('[data-testid="client-billable-delete-5"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="client-billable-feedback"]').text()).toBe('The item is already invoiced.')
    expect(wrapper.find('[data-testid="client-billable-error"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="client-billable-uninvoiced-table"]').exists()).toBe(true)
  })

  it('does not delete when the confirmation is cancelled', async () => {
    const mock = stubRoutes({ 'GET /api/clients/7/billable-items': items })
    const wrapper = await mountItems()

    await wrapper.get('[data-testid="client-billable-delete-5"]').trigger('click')
    await wrapper.get('[data-testid="dialog-cancel"]').trigger('click')
    await flushPromises()

    expect(mock.mock.calls.some(([, init]) => (init as RequestInit | undefined)?.method === 'DELETE')).toBe(false)
  })

  it('reloads the items from the Retry of a refused load', async () => {
    let refused = true
    stubRoutes({ 'GET /api/clients/7/billable-items': () => (refused ? problem('Items are unavailable.', 503) : items()) })
    const wrapper = await mountItems()

    refused = false
    await wrapper.get('[data-testid="client-billable-error-retry"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="client-billable-error"]').exists()).toBe(false)
  })
})
