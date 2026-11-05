/**
 * Mount tests for IntegrationDetailView: the config form and status sidebar of a built-in
 * integration, the API's refusal sentence, saving, testing, and the CWP and CWP7 special pages.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import IntegrationDetailView from '@/views/IntegrationDetailView.vue'
import { stubRoutes } from '../helpers/routes'
import { json, problem } from '../helpers/fetch'
import { mountView } from '../helpers/mount'
import { integrationDetail } from '../helpers/integrationFixtures'

/**
 * Upper bound, in ms, for a lazily imported page to arrive. The first transform of a route chunk on
 * a cold, loaded machine takes longer than the 1 s default of `vi.waitFor`.
 */
const LAZY_PAGE_TIMEOUT = 20_000

/**
 * Mounts the detail screen for a slug.
 *
 * @param slug - The integration slug in the URL.
 * @returns The wrapper.
 */
const mountDetail = async (slug: string) => {
  const wrapper = await mountView(IntegrationDetailView, {
    path: `/integrations/${slug}/configure`,
    pattern: '/integrations/:slug/configure',
  })
  await flushPromises()
  return wrapper
}

describe('IntegrationDetailView', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('shows the breadcrumb, the form and the status sidebar', async () => {
    stubRoutes({ 'GET /api/admin/integrations/bank-transfer': () => json(integrationDetail()) })
    const wrapper = await mountDetail('bank-transfer')

    expect(wrapper.get('[data-testid="integration-breadcrumb"]').text()).toContain('Bank Transfer')
    expect(wrapper.find('[data-testid="integration-config-form"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="integration-status"]').exists()).toBe(true)
  })

  it('shows the API sentence when the integration cannot be loaded', async () => {
    stubRoutes({ 'GET /api/admin/integrations/bank-transfer': () => problem('Not available.', 503) })
    const wrapper = await mountDetail('bank-transfer')

    expect(wrapper.get('[data-testid="integration-error"]').text()).toContain('Not available.')
  })

  it('saves the edited config and reloads the detail', async () => {
    const mock = stubRoutes({
      'GET /api/admin/integrations/bank-transfer': () => json(integrationDetail()),
      'PUT /api/admin/integrations/bank-transfer': () => new Response(null, { status: 204 }),
    })
    const wrapper = await mountDetail('bank-transfer')

    await wrapper.get('[data-testid="integration-field-secretKey-replace"]').trigger('click')
    await wrapper.get('[data-testid="integration-field-secretKey-input"]').setValue('sk_new')
    await wrapper.get('[data-testid="integration-save"]').trigger('click')
    await flushPromises()

    const put = mock.mock.calls.find(([, init]) => init?.method === 'PUT')
    expect(JSON.parse(String(put?.[1]?.body)).config.secretKey).toBe('sk_new')
    expect(wrapper.get('[data-testid="integration-feedback"]').text()).toBe('Settings saved.')
  })

  it('shows the API sentence above the form when the save is refused and keeps the form', async () => {
    stubRoutes({
      'GET /api/admin/integrations/bank-transfer': () => json(integrationDetail()),
      'PUT /api/admin/integrations/bank-transfer': () => problem('Required field missing.', 422),
    })
    const wrapper = await mountDetail('bank-transfer')

    await wrapper.get('[data-testid="integration-save"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="integration-feedback"]').text()).toBe('Required field missing.')
    expect(wrapper.find('[data-testid="integration-error"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="integration-config-form"]').exists()).toBe(true)
  })

  it('reloads the integration from the Retry of a refused load', async () => {
    let refused = true
    stubRoutes({ 'GET /api/admin/integrations/bank-transfer': () => (refused ? problem('Not available.', 503) : json(integrationDetail())) })
    const wrapper = await mountDetail('bank-transfer')

    refused = false
    await wrapper.get('[data-testid="integration-error-retry"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="integration-error"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="integration-config-form"]').exists()).toBe(true)
  })

  it('shows the verdict after a connection test', async () => {
    stubRoutes({
      'GET /api/admin/integrations/bank-transfer': () => json(integrationDetail()),
      'POST /api/admin/integrations/bank-transfer/test': () => json({ success: true, message: 'All good.' }),
    })
    const wrapper = await mountDetail('bank-transfer')

    await wrapper.get('[data-testid="integration-test"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="integration-status-result"]').text()).toContain('Connection OK')
  })

  it('renders the CWP page for the cwp slug', { timeout: LAZY_PAGE_TIMEOUT + 5000 }, async () => {
    stubRoutes({
      'GET /api/admin/integrations/cwp/server-info': () => json({ connected: true, accountsCount: 3, version: '0.9', lastTestedAt: null, errorMessage: null }),
      'GET /api/admin/integrations/cwp': () => json(integrationDetail({ slug: 'cwp', name: 'CWP' })),
    })
    const wrapper = await mountDetail('cwp')

    // The page is an async component, so it arrives after the dynamic import settles.
    await vi.waitFor(() => expect(wrapper.find('[data-testid="cwp-brand-header"]').exists()).toBe(true), { timeout: LAZY_PAGE_TIMEOUT })
  })

  it('renders the CWP7 page for the cwp7 slug', { timeout: LAZY_PAGE_TIMEOUT + 5000 }, async () => {
    stubRoutes({ 'GET /api/admin/servers': () => json([]) })
    const wrapper = await mountDetail('cwp7')

    await vi.waitFor(() => expect(wrapper.find('[data-testid="cwp7-header"]').exists()).toBe(true), { timeout: LAZY_PAGE_TIMEOUT })
  })
})
