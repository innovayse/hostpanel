/**
 * Mount tests for IntegrationConfigPanel: it loads the integration its `slug` names, draws the form beside the status
 * sidebar, shows the API's sentence with a Retry when the load is refused, and loads the next integration when the slug changes.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import IntegrationConfigPanel from '@/components/integrations/IntegrationConfigPanel.vue'
import { integrationDetail } from '../../helpers/integrationFixtures'
import { json, problem } from '../../helpers/fetch'
import { mountScreen } from '../../helpers/mount'
import { stubRoutes } from '../../helpers/routes'

describe('IntegrationConfigPanel', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('loads the integration of the slug and shows the form and the status sidebar', async () => {
    const mock = stubRoutes({ 'GET /api/admin/integrations/example-pay': () => json(integrationDetail({ slug: 'example-pay' })) })
    const { wrapper } = await mountScreen(IntegrationConfigPanel, { slug: 'example-pay' })

    expect(mock).toHaveBeenCalledTimes(1)
    expect(wrapper.find('[data-testid="integration-config-form"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="integration-status"]').exists()).toBe(true)
  })

  it('shows the API sentence when the load is refused', async () => {
    stubRoutes({ 'GET /api/admin/integrations/example-pay': () => problem('Not available.', 503) })
    const { wrapper } = await mountScreen(IntegrationConfigPanel, { slug: 'example-pay' })

    expect(wrapper.get('[data-testid="integration-error"]').text()).toContain('Not available.')
    expect(wrapper.find('[data-testid="integration-config-form"]').exists()).toBe(false)
  })

  it('loads the next integration when the slug changes', async () => {
    const mock = stubRoutes({
      'GET /api/admin/integrations/example-pay': () => json(integrationDetail({ slug: 'example-pay' })),
      'GET /api/admin/integrations/other-pay': () => json(integrationDetail({ slug: 'other-pay' })),
    })
    const { wrapper } = await mountScreen(IntegrationConfigPanel, { slug: 'example-pay' })

    await wrapper.setProps({ slug: 'other-pay' })
    await flushPromises()

    expect(String(mock.mock.calls[1]?.[0])).toBe('/api/admin/integrations/other-pay')
  })
})
