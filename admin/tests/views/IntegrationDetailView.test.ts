/**
 * Mount tests for IntegrationDetailView: the config form and status sidebar of a built-in
 * integration, the API's refusal sentence, saving, and testing.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import IntegrationDetailView from '@/views/IntegrationDetailView.vue'
import { useToastStore } from '@/stores/toast'
import { stubRoutes } from '../helpers/routes'
import { json, problem } from '../helpers/fetch'
import { mountView } from '../helpers/mount'
import { integrationDetail } from '../helpers/integrationFixtures'
import { toastMessages } from '../helpers/toasts'

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

  it('draws the form card and the status card as placeholders while the integration loads', async () => {
    vi.stubGlobal('fetch', vi.fn(() => new Promise<Response>(() => undefined)))
    const wrapper = await mountDetail('bank-transfer')

    expect(wrapper.find('[data-testid="ui-detail-card-config"]').attributes('aria-busy')).toBe('true')
    expect(wrapper.find('[data-testid="ui-detail-identity"]').exists()).toBe(true)
    expect(wrapper.findAll('[data-testid="ui-field-skeleton"]')).toHaveLength(4)
    expect(wrapper.find('[data-testid="integration-save"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="ui-detail-card-status"]').attributes('aria-busy')).toBe('true')
    expect(wrapper.find('[data-testid="ui-detail-row"]').text()).toContain('Connection Status')
  })

  it('shows the form and the status sidebar, with no trail of its own', async () => {
    stubRoutes({ 'GET /api/admin/integrations/bank-transfer': () => json(integrationDetail()) })
    const wrapper = await mountDetail('bank-transfer')

    expect(wrapper.find('[data-testid="integration-breadcrumb"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="integration-config-form"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="integration-status"]').exists()).toBe(true)
  })

  it('names the page by the integration once it has loaded', async () => {
    stubRoutes({ 'GET /api/admin/integrations/bank-transfer': () => json(integrationDetail()) })
    const wrapper = await mountDetail('bank-transfer')

    expect(wrapper.get('h1').text()).toBe('Bank Transfer')
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
    expect(useToastStore().toasts.map((toast) => toast.message)).toEqual(['Settings saved.'])
  })

  it('shows the API sentence above the form when the save is refused and keeps the form', async () => {
    stubRoutes({
      'GET /api/admin/integrations/bank-transfer': () => json(integrationDetail()),
      'PUT /api/admin/integrations/bank-transfer': () => problem('Required field missing.', 422),
    })
    const wrapper = await mountDetail('bank-transfer')

    await wrapper.get('[data-testid="integration-save"]').trigger('click')
    await flushPromises()

    expect(toastMessages()).toContain('Required field missing.')
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

  it('draws a plugin slug through the same generic form as every other integration', async () => {
    stubRoutes({ 'GET /api/admin/integrations/example-panel': () => json(integrationDetail({ slug: 'example-panel', name: 'Plugin panel' })) })
    const wrapper = await mountDetail('example-panel')

    expect(wrapper.find('[data-testid="integration-config-form"]').exists()).toBe(true)
  })
})
