/**
 * Mount tests for PluginSettingsTab: the config panel only when the plugin has a config form, the servers section only when
 * the plugin declares a server module, the slot widget bindings when it declares widgets, the retryable load errors, and a
 * note when there is nothing to configure.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import PluginSettingsTab from '@/components/plugins/details/PluginSettingsTab.vue'
import type { Product } from '@/types/product'
import { integrationDetail } from '../../../helpers/integrationFixtures'
import { json } from '../../../helpers/fetch'
import { mountScreen } from '../../../helpers/mount'
import { makeInstalled } from '../../../helpers/pluginDetails'
import { stubRoutes } from '../../../helpers/routes'
import { serverOptionDto } from '../../../helpers/serverFixtures'

/** Products to choose from; only the fields the row reads. */
const products = [{ id: 3, name: 'Shared' }] as Product[]

/** One widget of the plugin. */
const widgets = [
  { slotId: 'admin.service.panels', widgetId: 'usage', title: 'Usage', manifestProductIds: [], productIds: [3], isBound: true },
]

/**
 * Mounts the tab with defaults for a plugin that has neither config nor widgets.
 *
 * @param props - Props to change.
 * @returns The wrapper.
 */
const mountTab = async (props: Record<string, unknown> = {}) => {
  stubRoutes({ 'GET /api/admin/integrations/example-pay': () => json(integrationDetail({ slug: 'example-pay' })) })
  const { wrapper } = await mountScreen(PluginSettingsTab, {
    plugin: makeInstalled(),
    hasConfig: false,
    serverModules: [],
    servers: [],
    serversError: null,
    serversLoading: false,
    widgets: [],
    products,
    widgetsError: null,
    widgetsLoading: false,
    save: vi.fn(),
    bindingError: null,
    ...props,
  })
  return wrapper
}

describe('PluginSettingsTab', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('shows the config form of a plugin that has one', async () => {
    const wrapper = await mountTab({ hasConfig: true })

    expect(wrapper.find('[data-testid="integration-config-form"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="plugin-details-settings-empty"]').exists()).toBe(false)
  })

  it('lets the connection status of a panel plugin reflect its servers instead of "not tested yet"', async () => {
    const wrapper = await mountTab({
      hasConfig: true,
      serverModules: ['examplepanel'],
      servers: [serverOptionDto({ id: 7, isOnline: true }), serverOptionDto({ id: 8, isOnline: true })],
    })

    expect(wrapper.get('[data-testid="integration-status-servers"]').text()).toContain('2 of 2 servers online')
    expect(wrapper.find('[data-testid="integration-status-idle"]').exists()).toBe(false)
  })

  it('shows the slot widget bindings of a plugin that declares widgets', async () => {
    const wrapper = await mountTab({ widgets })

    expect(wrapper.find('[data-testid="plugin-slot-widgets-example-pay"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="integration-config-panel"]').exists()).toBe(false)
  })

  it('shows the servers of a plugin that declares a server module, beside its widgets', async () => {
    const wrapper = await mountTab({ serverModules: ['examplepanel'], servers: [serverOptionDto({ id: 7, name: 'Frankfurt 1' })], widgets })

    expect(wrapper.find('[data-testid="plugin-servers-card"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="plugin-server-7"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="plugin-slot-widgets-example-pay"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="plugin-details-settings-empty"]').exists()).toBe(false)
  })

  it('shows the servers section with its empty note when the plugin serves a module no server uses', async () => {
    const wrapper = await mountTab({ serverModules: ['examplepanel'] })

    expect(wrapper.find('[data-testid="plugin-servers-empty"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="plugin-details-settings-empty"]').exists()).toBe(false)
  })

  it('shows no servers section for a plugin that declares no server module, even when servers are passed', async () => {
    const wrapper = await mountTab({ servers: [serverOptionDto({ id: 7 })] })

    expect(wrapper.find('[data-testid="plugin-servers-card"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="plugin-server-7"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="plugin-details-settings-empty"]').exists()).toBe(true)
  })

  it('holds the place of the servers with a skeleton while they load, without the empty note', async () => {
    const wrapper = await mountTab({ serversLoading: true })

    expect(wrapper.find('[data-testid="plugin-servers-loading"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="plugin-details-settings-empty"]').exists()).toBe(false)
  })

  it('shows the servers load error with a retry instead of the section', async () => {
    const wrapper = await mountTab({ serverModules: ['examplepanel'], serversError: 'Servers unavailable.' })

    expect(wrapper.get('[data-testid="plugin-servers-error"]').text()).toContain('Servers unavailable.')
    expect(wrapper.find('[data-testid="plugin-servers-card"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="plugin-details-settings-empty"]').exists()).toBe(false)
    await wrapper.get('[data-testid="plugin-servers-error-retry"]').trigger('click')
    expect(wrapper.emitted('retryServers')).toHaveLength(1)
    expect(wrapper.emitted('retry')).toBeUndefined()
  })

  it('says there is nothing to configure when the plugin has neither', async () => {
    const wrapper = await mountTab()

    expect(wrapper.find('[data-testid="plugin-details-settings-empty"]').exists()).toBe(true)
  })

  it('holds the place of the widgets with a skeleton while they load, without the empty note', async () => {
    const wrapper = await mountTab({ widgetsLoading: true })

    expect(wrapper.find('[data-testid="plugin-slot-widgets-loading"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="plugin-details-settings-empty"]').exists()).toBe(false)
  })

  it('shows the load error with a retry instead of the widgets', async () => {
    const wrapper = await mountTab({ widgets, widgetsError: 'Widgets unavailable.' })

    expect(wrapper.get('[data-testid="plugin-slot-widgets-error"]').text()).toContain('Widgets unavailable.')
    expect(wrapper.find('[data-testid="plugin-slot-widgets-example-pay"]').exists()).toBe(false)
    await wrapper.get('[data-testid="plugin-slot-widgets-error-retry"]').trigger('click')
    expect(wrapper.emitted('retry')).toHaveLength(1)
  })
})
