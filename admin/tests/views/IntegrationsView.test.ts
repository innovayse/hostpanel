/**
 * Mount tests for IntegrationsView: the cards of the route's category, the active count, the
 * empty state and the API's refusal sentence.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import IntegrationsView from '@/views/IntegrationsView.vue'
import { stubRoutes } from '../helpers/routes'
import { json, problem, stubFetch } from '../helpers/fetch'
import { mountView } from '../helpers/mount'
import { integrationDto } from '../helpers/integrationFixtures'
import { PluginType } from '@/types/plugin'
import { flushPromises } from '@vue/test-utils'

describe('IntegrationsView', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('shows only the integrations of the route category, with the active count', async () => {
    stubRoutes({
      'GET /api/admin/integrations': () => json([
        integrationDto({ slug: 'stripe', name: 'Stripe', isEnabled: true }),
        integrationDto({ slug: 'paypal', name: 'PayPal', isEnabled: false }),
        integrationDto({ slug: 'smtp', name: 'SMTP', category: 'email' }),
      ]),
    })
    const wrapper = await mountView(IntegrationsView, { path: '/integrations/payments', pattern: '/integrations/:category?' })

    expect(wrapper.find('[data-testid="integration-card-stripe"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="integration-card-paypal"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="integration-card-smtp"]').exists()).toBe(false)
    expect(wrapper.text()).toContain('Integrations: 2')
    expect(wrapper.text()).toContain('1 active')
  })

  it('shows a fulfilment plugin on the fulfilment page, labelled with its type', async () => {
    stubRoutes({
      'GET /api/admin/integrations': () => json([
        integrationDto(),
        integrationDto({ slug: 'deliver', name: 'Deliver', category: 'payments', isPlugin: true, pluginType: PluginType.Fulfilment }),
      ]),
    })
    const wrapper = await mountView(IntegrationsView, { path: '/integrations/fulfilment', pattern: '/integrations/:category?' })

    expect(wrapper.find('[data-testid="integration-card-deliver"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="integration-card-stripe"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="integration-type-deliver"]').text()).toBe('Fulfilment')
    expect(wrapper.text()).toContain('Integrations: 1')
  })

  it('defaults to payments on the bare section', async () => {
    stubRoutes({ 'GET /api/admin/integrations': () => json([integrationDto()]) })
    const wrapper = await mountView(IntegrationsView, { path: '/integrations', pattern: '/integrations/:category?' })

    expect(wrapper.find('[data-testid="integration-card-stripe"]').exists()).toBe(true)
  })

  it('shows the empty state for a category with nothing in it', async () => {
    stubRoutes({ 'GET /api/admin/integrations': () => json([integrationDto()]) })
    const wrapper = await mountView(IntegrationsView, { path: '/integrations/fraud', pattern: '/integrations/:category?' })

    expect(wrapper.find('[data-testid="integrations-empty"]').exists()).toBe(true)
  })

  it('shows the API sentence when the load is refused', async () => {
    stubRoutes({ 'GET /api/admin/integrations': () => problem('Integrations are unavailable.', 503) })
    const wrapper = await mountView(IntegrationsView, { path: '/integrations/payments', pattern: '/integrations/:category?' })

    expect(wrapper.get('[data-testid="integrations-error"]').text()).toContain('Integrations are unavailable.')
  })

  it('loads again from the Retry of a refused load', async () => {
    stubFetch(problem('Not allowed.', 403))
    const wrapper = await mountView(IntegrationsView, { path: '/integrations/payments', pattern: '/integrations/:category?' })
    await flushPromises()
    const fetchMock = vi.mocked(fetch)
    const before = fetchMock.mock.calls.length

    await wrapper.get('[data-testid="integrations-error-retry"]').trigger('click')
    await flushPromises()

    expect(fetchMock.mock.calls.length).toBeGreaterThan(before)
  })
})
