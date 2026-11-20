/**
 * Mount tests for Cwp7IntegrationView: the module header, the quick actions, and the CWP7
 * servers (other modules left out) or the empty state.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import Cwp7IntegrationView from '@/views/Cwp7IntegrationView.vue'
import { stubRoutes } from '../helpers/routes'
import { json } from '../helpers/fetch'
import { mountView } from '../helpers/mount'
import { serverOptionDto } from '../helpers/serverFixtures'

describe('Cwp7IntegrationView', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('lists the CWP7 servers the lookup answers for that module, with their usage', async () => {
    const mock = stubRoutes({
      'GET /api/admin/servers/options?module=Cwp7': () => json([serverOptionDto({ id: 1, name: 'cwp7-a' })]),
    })
    const wrapper = await mountView(Cwp7IntegrationView)
    await flushPromises()

    expect(wrapper.find('[data-testid="cwp7-server-1"]').exists()).toBe(true)
    expect(wrapper.get('[data-testid="cwp7-server-1"]').text()).toContain('12 / 100')
    expect(mock.mock.calls.map(([url]) => String(url))).toEqual(['/api/admin/servers/options?module=Cwp7'])
  })

  it('shows the empty state with a way to add the first server', async () => {
    stubRoutes({ 'GET /api/admin/servers/options?module=Cwp7': () => json([]) })
    const wrapper = await mountView(Cwp7IntegrationView)
    await flushPromises()

    expect(wrapper.find('[data-testid="cwp7-servers-empty"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="cwp7-add-first-server"]').exists()).toBe(true)
  })

  it('sends the quick actions to the servers and products pages', async () => {
    stubRoutes({ 'GET /api/admin/servers/options?module=Cwp7': () => json([]) })
    const wrapper = await mountView(Cwp7IntegrationView)
    await flushPromises()

    // Real links, so the pages open in a new tab too.
    expect(wrapper.get('[data-testid="cwp7-create-server"]').attributes('href')).toBe('/servers?create=Cwp7')
    expect(wrapper.get('[data-testid="cwp7-create-product"]').attributes('href')).toBe('/settings/products?create=true&module=Cwp7')
  })

  it('draws the placeholder boxes of the schema in the servers card while the servers load, the other cards staying', async () => {
    vi.stubGlobal('fetch', vi.fn(() => new Promise<Response>(() => undefined)))
    const wrapper = await mountView(Cwp7IntegrationView)
    await flushPromises()

    expect(wrapper.get('[data-testid="ui-detail-card-servers"]').attributes('aria-busy')).toBe('true')
    expect(wrapper.get('[data-testid="ui-detail-card-servers"]').findAll('[data-testid="ui-detail-tiles"] > *')).toHaveLength(3)
    expect(wrapper.find('[data-testid="cwp7-servers-empty"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="ui-detail-card-createServer"]').attributes('aria-busy')).toBeUndefined()
    expect(wrapper.find('[data-testid="cwp7-create-server"]').exists()).toBe(true)
  })
})
