/**
 * Mount tests for CwpIntegrationView: the header, the form and the live status card from the
 * server-info endpoint, the refusal a failed test returns, the toast of a save's outcome and a refused load
 * with its Retry.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import CwpIntegrationView from '@/views/CwpIntegrationView.vue'
import { stubRoutes } from '../helpers/routes'
import { json, problem } from '../helpers/fetch'
import { mountView } from '../helpers/mount'
import { integrationDetail } from '../helpers/integrationFixtures'
import { toastsOnScreen } from '../helpers/toasts'
import { UiTone } from '@/utils/ui/tone'

/** Live server info of a connected CWP server. */
const connectedInfo = { connected: true, accountsCount: 21, version: '0.9.8', lastTestedAt: null, errorMessage: null }

describe('CwpIntegrationView', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('shows the header with the version and the status card with the accounts', async () => {
    stubRoutes({
      'GET /api/admin/integrations/cwp/server-info': () => json(connectedInfo),
      'GET /api/admin/integrations/cwp': () => json(integrationDetail({ slug: 'cwp', name: 'CWP' })),
    })
    const wrapper = await mountView(CwpIntegrationView)
    await flushPromises()

    expect(wrapper.get('[data-testid="cwp-version"]').text()).toBe('v0.9.8')
    expect(wrapper.get('[data-testid="cwp-status"]').text()).toBe('Connected')
    expect(wrapper.get('[data-testid="provider-status"]').text()).toContain('21')
    expect(wrapper.find('[data-testid="integration-config-form"]').exists()).toBe(true)
  })

  it('puts the brand header, the form and the status in the three cards of the schema', async () => {
    stubRoutes({
      'GET /api/admin/integrations/cwp/server-info': () => json(connectedInfo),
      'GET /api/admin/integrations/cwp': () => json(integrationDetail({ slug: 'cwp', name: 'CWP' })),
    })
    const wrapper = await mountView(CwpIntegrationView)
    await flushPromises()

    expect(wrapper.find('[data-testid="ui-detail-card-brand"] [data-testid="cwp-brand-header"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="ui-detail-card-config"] [data-testid="integration-config-form"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="ui-detail-card-status"] [data-testid="provider-status"]').exists()).toBe(true)
  })

  it('shows the message of a failed test under the form', async () => {
    stubRoutes({
      'GET /api/admin/integrations/cwp/server-info': () => json(connectedInfo),
      'GET /api/admin/integrations/cwp': () => json(integrationDetail({ slug: 'cwp', name: 'CWP' })),
      'POST /api/admin/integrations/cwp/test': () => json({ success: false, message: 'Bad token.' }),
    })
    const wrapper = await mountView(CwpIntegrationView)
    await flushPromises()

    await wrapper.get('[data-testid="provider-status-test"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="cwp-test-error"]').text()).toContain('Bad token.')
  })

  it('toasts a saved config', async () => {
    stubRoutes({
      'GET /api/admin/integrations/cwp/server-info': () => json(connectedInfo),
      'PUT /api/admin/integrations/cwp': () => new Response(null, { status: 204 }),
      'GET /api/admin/integrations/cwp': () => json(integrationDetail({ slug: 'cwp', name: 'CWP' })),
    })
    const wrapper = await mountView(CwpIntegrationView)
    await flushPromises()

    await wrapper.get('[data-testid="integration-save"]').trigger('click')
    await flushPromises()

    expect(toastsOnScreen()).toEqual([['Settings saved.', UiTone.Success]])
  })

  it('toasts the API sentence in the danger tone when the save is refused', async () => {
    stubRoutes({
      'GET /api/admin/integrations/cwp/server-info': () => json(connectedInfo),
      'PUT /api/admin/integrations/cwp': () => problem('Host is required.', 422),
      'GET /api/admin/integrations/cwp': () => json(integrationDetail({ slug: 'cwp', name: 'CWP' })),
    })
    const wrapper = await mountView(CwpIntegrationView)
    await flushPromises()

    await wrapper.get('[data-testid="integration-save"]').trigger('click')
    await flushPromises()

    expect(toastsOnScreen()).toEqual([['Host is required.', UiTone.Danger]])
  })

  it('shows a refused load with a Retry and loads again from it', async () => {
    let refused = true
    stubRoutes({
      'GET /api/admin/integrations/cwp/server-info': () => json(connectedInfo),
      'GET /api/admin/integrations/cwp': () => (refused ? problem('Not available.', 503) : json(integrationDetail({ slug: 'cwp', name: 'CWP' }))),
    })
    const wrapper = await mountView(CwpIntegrationView)
    await flushPromises()

    expect(wrapper.get('[data-testid="cwp-load-error"]').text()).toBe('Not available.')
    refused = false
    await wrapper.get('[data-testid="cwp-load-error-retry"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="cwp-load-error"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="integration-config-form"]').exists()).toBe(true)
  })
})
