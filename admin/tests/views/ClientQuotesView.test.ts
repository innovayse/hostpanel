/**
 * Mount tests for ClientQuotesView: it renders against the real kit and settles on an API refusal
 * with a Retry, and a delete is confirmed first and reports its outcome next to the list.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import ClientQuotesView from '@/views/ClientQuotesView.vue'
import { quoteRowFixture } from '../helpers/billingFixtures'
import { mountClientView } from '../helpers/clientViews'
import { json, problem } from '../helpers/fetch'
import { mountRouted } from '../helpers/mount'
import { stubRoutes } from '../helpers/routes'

/** The one-quote page of client 7. */
const listPage = () => json({ items: [quoteRowFixture()], pageNumber: 1, pageSize: 25, totalCount: 1, totalPages: 1 })

/**
 * Mounts the screen for client 7 and waits for its first load.
 *
 * @returns The wrapper.
 */
const mountQuotes = async () => {
  const { wrapper } = await mountRouted(ClientQuotesView, '/clients/:id/quotes', '/clients/7/quotes')
  await flushPromises()
  return wrapper
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('ClientQuotesView', () => {
  it('mounts without throwing', async () => {
    const wrapper = await mountClientView(ClientQuotesView)

    expect(wrapper.exists()).toBe(true)
  })

  it('renders its main action', async () => {
    const wrapper = await mountClientView(ClientQuotesView)

    expect(wrapper.find('[data-testid="client-quotes-create"]').exists()).toBe(true)
  })

  it('shows the refusal sentence from the API', async () => {
    const wrapper = await mountClientView(ClientQuotesView, 'Server said no')

    expect(wrapper.text()).toContain('Server said no')
  })

  it('reports a deleted quote next to the list', async () => {
    stubRoutes({ 'GET /api/billing/quotes/client/7': listPage, 'DELETE /api/quotes/3': () => new Response(null, { status: 204 }) })
    const wrapper = await mountQuotes()

    // Two or more row actions sit behind the kebab menu, so it is opened first.
    await wrapper.get('[data-testid="client-quotes-actions-3"]').trigger('click')
    await wrapper.get('[data-testid="client-quotes-delete-3"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="client-quotes-feedback"]').text()).toBe('Quote deleted.')
  })

  it('shows the API sentence when the delete is refused, without the load error', async () => {
    stubRoutes({ 'GET /api/billing/quotes/client/7': listPage, 'DELETE /api/quotes/3': () => problem('The quote was accepted.', 409) })
    const wrapper = await mountQuotes()

    // Two or more row actions sit behind the kebab menu, so it is opened first.
    await wrapper.get('[data-testid="client-quotes-actions-3"]').trigger('click')
    await wrapper.get('[data-testid="client-quotes-delete-3"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="client-quotes-feedback"]').text()).toBe('The quote was accepted.')
    expect(wrapper.find('[data-testid="client-quotes-error"]').exists()).toBe(false)
  })

  it('does not delete when the confirmation is cancelled', async () => {
    const mock = stubRoutes({ 'GET /api/billing/quotes/client/7': listPage })
    const wrapper = await mountQuotes()

    // Two or more row actions sit behind the kebab menu, so it is opened first.
    await wrapper.get('[data-testid="client-quotes-actions-3"]').trigger('click')
    await wrapper.get('[data-testid="client-quotes-delete-3"]').trigger('click')
    await wrapper.get('[data-testid="dialog-cancel"]').trigger('click')
    await flushPromises()

    expect(mock.mock.calls.some(([, init]) => (init as RequestInit | undefined)?.method === 'DELETE')).toBe(false)
  })

  it('reloads the list from the Retry of a refused load', async () => {
    let refused = true
    stubRoutes({ 'GET /api/billing/quotes/client/7': () => (refused ? problem('Quotes are disabled.', 403) : listPage()) })
    const wrapper = await mountQuotes()

    refused = false
    await wrapper.get('[data-testid="client-quotes-error-retry"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="client-quotes-error"]').exists()).toBe(false)
  })
})
