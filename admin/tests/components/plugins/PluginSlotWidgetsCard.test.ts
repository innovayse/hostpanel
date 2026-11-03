/**
 * Unit tests for PluginSlotWidgetsCard: the plugin heading, one row per widget, a save that goes to the
 * parent's callback with the widget and ids, and a refusal shown on the row that failed only.
 */
import { describe, expect, it, vi } from 'vitest'
import PluginSlotWidgetsCard from '@/components/plugins/PluginSlotWidgetsCard.vue'
import type { Product } from '@/types/product'
import { mountScreen } from '../../helpers/mount'

/** Products to choose from; only the fields the row reads. */
const products = [{ id: 3, name: 'Shared' }, { id: 4, name: 'Reseller' }] as Product[]

/** Two widgets of the plugin. */
const widgets = [
  { slotId: 'admin.service.panels', widgetId: 'usage', title: 'Usage', manifestProductIds: [], productIds: [3], isBound: true },
  { slotId: 'admin.service.panels', widgetId: 'logs', title: null, manifestProductIds: [1], productIds: [], isBound: false },
]

describe('PluginSlotWidgetsCard', () => {
  it('names the plugin in the heading', async () => {
    const { wrapper } = await mountScreen(PluginSlotWidgetsCard, {
      pluginId: 'acme', pluginName: 'Acme', widgets, products, save: vi.fn(),
    })

    expect(wrapper.get('[data-testid="plugin-slot-widgets-acme"]').text()).toContain('Slot widgets: Acme')
  })

  it('renders one row per widget', async () => {
    const { wrapper } = await mountScreen(PluginSlotWidgetsCard, { pluginId: 'acme', pluginName: 'Acme', widgets, products, save: vi.fn() })

    expect(wrapper.find('[data-testid="plugin-widget-acme-usage"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="plugin-widget-acme-logs"]').exists()).toBe(true)
  })

  it('saves the chosen ids through the callback', async () => {
    const save = vi.fn(async () => true)
    const { wrapper } = await mountScreen(PluginSlotWidgetsCard, { pluginId: 'acme', pluginName: 'Acme', widgets, products, save })

    await wrapper.get('[data-testid="plugin-widget-remove-3"]').trigger('click')
    await wrapper.get('[data-testid="plugin-widget-acme-usage"] [data-testid="plugin-widget-save"]').trigger('click')

    expect(save).toHaveBeenCalledWith(widgets[0], [])
  })

  it('shows the refusal on the row that failed only', async () => {
    const save = vi.fn(async () => false)
    const { wrapper } = await mountScreen(PluginSlotWidgetsCard, { pluginId: 'acme', pluginName: 'Acme', widgets, products, save, error: 'Too many products.' })

    await wrapper.get('[data-testid="plugin-widget-remove-3"]').trigger('click')
    await wrapper.get('[data-testid="plugin-widget-acme-usage"] [data-testid="plugin-widget-save"]').trigger('click')
    await wrapper.vm.$nextTick()

    expect(wrapper.get('[data-testid="plugin-widget-acme-usage"]').text()).toContain('Too many products.')
    expect(wrapper.get('[data-testid="plugin-widget-acme-logs"]').text()).not.toContain('Too many products.')
  })
})
