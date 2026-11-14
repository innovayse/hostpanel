/**
 * Mount tests for PluginSettingsTab: the config panel only when the plugin has a config form, the slot widget
 * bindings when it declares widgets, the retryable load error, and a note when there is nothing to configure.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import PluginSettingsTab from '@/components/plugins/details/PluginSettingsTab.vue'
import type { Product } from '@/types/product'
import { integrationDetail } from '../../../helpers/integrationFixtures'
import { json } from '../../../helpers/fetch'
import { mountScreen } from '../../../helpers/mount'
import { makeInstalled } from '../../../helpers/pluginDetails'
import { stubRoutes } from '../../../helpers/routes'

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

  it('shows the slot widget bindings of a plugin that declares widgets', async () => {
    const wrapper = await mountTab({ widgets })

    expect(wrapper.find('[data-testid="plugin-slot-widgets-example-pay"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="integration-config-panel"]').exists()).toBe(false)
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
