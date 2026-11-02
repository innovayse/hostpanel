/**
 * Mount tests for ClientInvoicesView: it renders against the real kit and settles on an API
 * refusal with a Retry, and a delete is confirmed first and reports its outcome next to the list.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import ClientInvoicesView from '@/views/ClientInvoicesView.vue'
import { invoiceRowFixture } from '../helpers/billingFixtures'
import { mountClientView } from '../helpers/clientViews'
import { json, problem } from '../helpers/fetch'
import { mountRouted } from '../helpers/mount'
import { stubRoutes } from '../helpers/routes'

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

    await wrapper.get('[data-testid="client-invoices-delete-1"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="client-invoices-feedback"]').text()).toBe('Invoice deleted.')
  })

  it('shows the API sentence when the delete is refused, without the load error', async () => {
    stubRoutes({ 'GET /api/billing/client/7': listPage, 'DELETE /api/billing/1': () => problem('The invoice has payments.', 409) })
    const wrapper = await mountInvoices()

    await wrapper.get('[data-testid="client-invoices-delete-1"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="client-invoices-feedback"]').text()).toBe('The invoice has payments.')
    expect(wrapper.find('[data-testid="client-invoices-error"]').exists()).toBe(false)
  })

  it('does not delete when the confirmation is cancelled', async () => {
    const mock = stubRoutes({ 'GET /api/billing/client/7': listPage })
    const wrapper = await mountInvoices()

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
})
