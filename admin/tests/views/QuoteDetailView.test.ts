/**
 * Mount tests for QuoteDetailView: an existing quote fills the form and saves through the store,
 * delete asks first, and a new quote without a client names the missing client.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import QuoteDetailView from '@/views/QuoteDetailView.vue'
import { json, problem } from '../helpers/fetch'
import { mountRouted } from '../helpers/mount'
import { stubRoutes } from '../helpers/routes'

/** A stored quote as `GET /api/quotes/3` returns it. */
const quote = {
  id: 3,
  clientId: 7,
  subject: 'Hosting renewal',
  stage: 'Draft',
  dateCreated: '2026-10-01T00:00:00Z',
  validUntil: '2099-12-31T00:00:00Z',
  subTotal: 20,
  total: 20,
  proposalText: 'Proposal',
  customerNotes: '',
  adminNotes: '',
  items: [{ id: 31, quantity: 2, description: 'Hosting', unitPrice: 10, discountPercent: 0, taxed: false, amount: 20 }],
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('QuoteDetailView', () => {
  it('fills the form from the quote and saves the edited subject', async () => {
    const mock = stubRoutes({
      'GET /api/quotes/3': () => json(quote),
      'PUT /api/quotes/3': () => new Response(null, { status: 204 }),
    })
    const { wrapper } = await mountRouted(QuoteDetailView, '/billing/quotes/:id', '/billing/quotes/3')
    await flushPromises()

    const subject = wrapper.get('[data-testid="billing-quote-subject"]')
    expect((subject.element as HTMLInputElement).value).toBe('Hosting renewal')
    expect(wrapper.get('[data-testid="billing-quote-total"]').text()).toBe('20.00 USD')

    await subject.setValue('Renewal 2027')
    await wrapper.get('[data-testid="billing-quote-save"]').trigger('click')
    await flushPromises()

    const put = mock.mock.calls.find(([, init]) => (init as RequestInit | undefined)?.method === 'PUT')
    expect(JSON.parse(String((put?.[1] as RequestInit | undefined)?.body))).toMatchObject({
      subject: 'Renewal 2027',
      stage: 'Draft',
      items: [{ id: 31, quantity: 2 }],
    })
    expect(wrapper.get('[data-testid="billing-quote-success"]').text()).toBe('Quote #3 saved.')
  })

  it('shows the API sentence when the save is refused', async () => {
    stubRoutes({
      'GET /api/quotes/3': () => json(quote),
      'PUT /api/quotes/3': () => problem('Quote is accepted.', 409),
    })
    const { wrapper } = await mountRouted(QuoteDetailView, '/billing/quotes/:id', '/billing/quotes/3')
    await flushPromises()

    await wrapper.get('[data-testid="billing-quote-save"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="billing-quote-error"]').text()).toBe('Quote is accepted.')
  })

  it('stays on the quote with the API sentence when the delete is refused', async () => {
    stubRoutes({
      'GET /api/quotes/3': () => json(quote),
      'DELETE /api/quotes/3': () => problem('Quote is accepted.', 409),
    })
    const { wrapper, router } = await mountRouted(QuoteDetailView, '/billing/quotes/:id', '/billing/quotes/3')
    await flushPromises()

    await wrapper.get('[data-testid="billing-quote-delete"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.path).toBe('/billing/quotes/3')
    expect(wrapper.get('[data-testid="billing-quote-error"]').text()).toBe('Quote is accepted.')
  })

  it('offers a retry when the quote cannot be loaded', async () => {
    let calls = 0
    stubRoutes({
      'GET /api/quotes/3': () => {
        calls += 1
        return calls === 1 ? problem('No such quote.', 404) : json(quote)
      },
    })
    const { wrapper } = await mountRouted(QuoteDetailView, '/billing/quotes/:id', '/billing/quotes/3')
    await flushPromises()

    expect(wrapper.get('[data-testid="billing-quote-load-error"]').text()).toBe('No such quote.')
    await wrapper.get('[data-testid="billing-quote-load-error-retry"]').trigger('click')
    await flushPromises()

    expect((wrapper.get('[data-testid="billing-quote-subject"]').element as HTMLInputElement).value).toBe('Hosting renewal')
  })

  it('deletes the quote only after the confirmation', async () => {
    const mock = stubRoutes({
      'GET /api/quotes/3': () => json(quote),
      'DELETE /api/quotes/3': () => new Response(null, { status: 204 }),
    })
    const { wrapper } = await mountRouted(QuoteDetailView, '/billing/quotes/:id', '/billing/quotes/3')
    await flushPromises()

    await wrapper.get('[data-testid="billing-quote-delete"]').trigger('click')
    expect(mock.mock.calls.some(([, init]) => (init as RequestInit | undefined)?.method === 'DELETE')).toBe(false)
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(mock.mock.calls.some(([, init]) => (init as RequestInit | undefined)?.method === 'DELETE')).toBe(true)
  })

  it('refuses to create a quote without a client', async () => {
    const { wrapper } = await mountRouted(QuoteDetailView, '/billing/quotes/new', '/billing/quotes/new')
    await flushPromises()

    expect(wrapper.find('[data-testid="billing-quote-duplicate"]').exists()).toBe(false)
    await wrapper.get('[data-testid="billing-quote-save"]').trigger('click')

    expect(wrapper.get('[data-testid="billing-quote-error"]').text()).toContain('Client ID is required.')
  })

  it('prices the lines in the currency the stored quote carries', async () => {
    stubRoutes({ 'GET /api/quotes/3': () => json({ ...quote, currency: 'AMD' }) })
    const { wrapper } = await mountRouted(QuoteDetailView, '/billing/quotes/:id', '/billing/quotes/3')
    await flushPromises()

    expect(wrapper.get('[data-testid="billing-quote-total"]').text()).toBe('20.00 AMD')
  })
})
