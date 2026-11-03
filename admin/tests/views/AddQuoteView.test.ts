/**
 * Mount tests for AddQuoteView: the client from the query is used, the quote is created through
 * the store, and a refusal shows the API sentence.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import AddQuoteView from '@/views/AddQuoteView.vue'
import { json, problem } from '../helpers/fetch'
import { mountRouted } from '../helpers/mount'
import { stubRoutes } from '../helpers/routes'

/** The clients endpoint answer: nobody (the client comes from the query). */
const noClients = () => json({ items: [], totalCount: 0 })

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('AddQuoteView', () => {
  it('creates the quote for the client named in the query and opens it', async () => {
    const mock = stubRoutes({ 'GET /api/clients/7': () => json({ id: 7, preferences: {} }), 'GET /api/clients': noClients, 'POST /api/billing/quotes': () => json(55) })
    const { wrapper, router } = await mountRouted(AddQuoteView, '/billing/quotes/add', '/billing/quotes/add?clientId=7')
    await flushPromises()

    await wrapper.get('[data-testid="billing-add-quote-subject"]').setValue('Renewal')
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    const post = mock.mock.calls.find(([, init]) => (init as RequestInit | undefined)?.method === 'POST')
    expect(JSON.parse(String((post?.[1] as RequestInit | undefined)?.body))).toMatchObject({
      clientId: 7,
      subject: 'Renewal',
      stage: 'Draft',
      items: [{ quantity: 1, description: '' }],
    })
    expect(router.currentRoute.value.path).toBe('/billing/quotes/55')
  })

  it('shows the API sentence when the quote is refused', async () => {
    stubRoutes({ 'GET /api/clients': noClients, 'POST /api/billing/quotes': () => problem('Subject is required.', 400) })
    const { wrapper } = await mountRouted(AddQuoteView, '/billing/quotes/add', '/billing/quotes/add')
    await flushPromises()

    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(wrapper.get('[data-testid="billing-add-quote-error"]').text()).toContain('Subject is required.')
  })

  it('prices the lines in the billing currency of the client named in the query', async () => {
    stubRoutes({ 'GET /api/clients/7': () => json({ id: 7, preferences: { currency: 'AMD' } }), 'GET /api/clients': noClients })
    const { wrapper } = await mountRouted(AddQuoteView, '/billing/quotes/add', '/billing/quotes/add?clientId=7')
    await flushPromises()

    expect(wrapper.get('[data-testid="billing-quote-total"]').text()).toBe('0.00 AMD')
  })
})
