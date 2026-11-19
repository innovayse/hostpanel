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
import { PluginErrorCode, PluginType, RegistryInstanceState } from '@/types/plugin'
import type { RegistryInstanceDto } from '@/types/plugin'
import { flushPromises } from '@vue/test-utils'

/**
 * Builds the state of a server the plugin registry knows.
 *
 * @returns The registry instance, verified.
 */
const registryInstance = (): RegistryInstanceDto => ({
  state: RegistryInstanceState.Verified,
  instanceId: 'instance-1',
  baseUrl: 'https://panel.example.com',
  errorCode: null,
  message: null,
  verifiedAt: '2026-11-01T10:00:00Z',
  nextAttemptAt: null,
})

describe('IntegrationsView', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('shows only the integrations of the route category, with the active count', async () => {
    stubRoutes({
      'GET /api/admin/integrations': () => json([
        integrationDto({ slug: 'bank-transfer', name: 'Bank Transfer', isEnabled: true }),
        integrationDto({ slug: 'bank-transfer', name: 'Bank Transfer', isEnabled: false }),
        integrationDto({ slug: 'smtp', name: 'SMTP', category: 'email' }),
      ]),
    })
    const wrapper = await mountView(IntegrationsView, { path: '/integrations/payments', pattern: '/integrations/:category?' })

    expect(wrapper.find('[data-testid="integration-card-bank-transfer"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="integration-card-bank-transfer"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="integration-card-smtp"]').exists()).toBe(false)
    expect(wrapper.text()).toContain('Integrations: 2')
    expect(wrapper.text()).toContain('1 active')
  })

  it('shows a fulfilment plugin on the fulfilment page, without a badge for its type', async () => {
    stubRoutes({
      'GET /api/admin/integrations': () => json([
        integrationDto(),
        integrationDto({ slug: 'deliver', name: 'Deliver', category: 'payments', isPlugin: true, pluginType: PluginType.Fulfilment }),
      ]),
    })
    const wrapper = await mountView(IntegrationsView, { path: '/integrations/fulfilment', pattern: '/integrations/:category?' })

    expect(wrapper.find('[data-testid="integration-card-deliver"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="integration-card-bank-transfer"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="integration-type-deliver"]').exists()).toBe(false)
    expect(wrapper.text()).toContain('Integrations: 1')
  })

  it('defaults to payments on the bare section', async () => {
    stubRoutes({ 'GET /api/admin/integrations': () => json([integrationDto()]) })
    const wrapper = await mountView(IntegrationsView, { path: '/integrations', pattern: '/integrations/:category?' })

    expect(wrapper.find('[data-testid="integration-card-bank-transfer"]').exists()).toBe(true)
  })

  it('shows the empty state for a category with nothing in it', async () => {
    stubRoutes({ 'GET /api/admin/integrations': () => json([integrationDto()]) })
    const wrapper = await mountView(IntegrationsView, { path: '/integrations/fraud', pattern: '/integrations/:category?' })

    expect(wrapper.find('[data-testid="integrations-empty"]').exists()).toBe(true)
  })

  it('says what an empty category holds instead of counting zero, with the category icon', async () => {
    stubRoutes({
      'GET /api/admin/integrations': () => json([integrationDto()]),
      'GET /api/admin/plugins/registry-instance': () => json(registryInstance()),
    })
    const wrapper = await mountView(IntegrationsView, { path: '/integrations/identity', pattern: '/integrations/:category?' })
    await flushPromises()

    expect(wrapper.get('header').text()).toContain('Identity plugins decide which client a signed-in person acts for.')
    expect(wrapper.text()).not.toContain('Integrations: 0')
    expect(wrapper.find('[data-testid="ui-empty-state-icon"]').exists()).toBe(true)
  })

  it('offers the catalogue filtered by the registry category, and the upload, while the registry is on', async () => {
    stubRoutes({
      'GET /api/admin/integrations': () => json([integrationDto()]),
      'GET /api/admin/plugins/registry-instance': () => json(registryInstance()),
    })
    const wrapper = await mountView(IntegrationsView, { path: '/integrations/registrars', pattern: '/integrations/:category?' })
    await flushPromises()

    expect(wrapper.get('[data-testid="integrations-empty-browse"]').attributes('href')).toBe('/plugins?tab=browse&category=Registrar')
    expect(wrapper.get('[data-testid="integrations-empty-upload"]').attributes('href')).toBe('/plugins')
  })

  it('browses the whole catalogue for a category the registry has no category for', async () => {
    stubRoutes({
      'GET /api/admin/integrations': () => json([integrationDto()]),
      'GET /api/admin/plugins/registry-instance': () => json(registryInstance()),
    })
    const wrapper = await mountView(IntegrationsView, { path: '/integrations/identity', pattern: '/integrations/:category?' })
    await flushPromises()

    expect(wrapper.get('[data-testid="integrations-empty-browse"]').attributes('href')).toBe('/plugins?tab=browse')
  })

  it('offers only the upload while the registry is off', async () => {
    stubRoutes({
      'GET /api/admin/integrations': () => json([integrationDto()]),
      'GET /api/admin/plugins/registry-instance': () =>
        json({ type: PluginErrorCode.RegistryDisabled, title: 'Disabled', status: 404, detail: 'The registry is off.' }, 404),
    })
    const wrapper = await mountView(IntegrationsView, { path: '/integrations/hosted', pattern: '/integrations/:category?' })
    await flushPromises()

    expect(wrapper.find('[data-testid="integrations-empty-browse"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="integrations-empty-upload"]').exists()).toBe(true)
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

  it('keeps the grid and draws placeholder cards of the real card while the list loads', async () => {
    vi.stubGlobal('fetch', vi.fn(() => new Promise<Response>(() => undefined)))
    const wrapper = await mountView(IntegrationsView, { path: '/integrations/payments', pattern: '/integrations/:category?' })
    await flushPromises()

    expect(wrapper.get('[data-testid="integrations-grid"]').findAll('[data-testid="integration-card-loading"]')).toHaveLength(6)
    expect(wrapper.find('[data-testid="integrations-empty"]').exists()).toBe(false)
    expect(wrapper.find('header').text()).toContain('Payment')
    expect(wrapper.get('header').find('[data-testid="ui-page-header-subtitle-skeleton"]').exists()).toBe(true)
  })
})
