/**
 * Unit tests for PluginSlotWidgetRow: chosen products as chips, adding from the kit select, removing,
 * Save enabled only after a change and emitting the ids, and a refusal sentence.
 */
import { describe, expect, it } from 'vitest'
import PluginSlotWidgetRow from '@/components/plugins/PluginSlotWidgetRow.vue'
import type { Product } from '@/types/product'
import { mountScreen } from '../../helpers/mount'

/** Two products the operator can choose from; only the fields the row reads. */
const products = [{ id: 3, name: 'Shared' }, { id: 4, name: 'Reseller' }] as Product[]

/** A widget bound to the first product. */
const widget = { slotId: 'admin.service.panels', widgetId: 'usage', title: 'Usage', manifestProductIds: [], productIds: [3], isBound: true }

/**
 * Mounts the row.
 *
 * @param overrides - Props to change.
 * @returns The wrapper.
 */
const mountRow = async (overrides: Record<string, unknown> = {}) =>
  (await mountScreen(PluginSlotWidgetRow, { pluginId: 'acme', widget, products, ...overrides })).wrapper

describe('PluginSlotWidgetRow', () => {
  it('shows the widget title, its slot and the bound products', async () => {
    const wrapper = await mountRow()

    expect(wrapper.text()).toContain('Usage')
    expect(wrapper.text()).toContain('admin.service.panels')
    expect(wrapper.get('[data-testid="plugin-widget-chosen"]').text()).toContain('Shared')
  })

  it('says the plugin list applies while no product is chosen', async () => {
    const wrapper = await mountRow({ widget: { ...widget, productIds: [] } })

    expect(wrapper.get('[data-testid="plugin-widget-chosen"]').text()).toContain('No products chosen')
  })

  it('warns that the widget shows nowhere when it is bound but every product is gone', async () => {
    const wrapper = await mountRow({ widget: { ...widget, productIds: [] } })

    expect(wrapper.get('[data-testid="plugin-widget-orphaned"]').text()).toContain('no longer exist')
  })

  it('shows no warning for a widget that was never bound', async () => {
    const wrapper = await mountRow({ widget: { ...widget, productIds: [], isBound: false } })

    expect(wrapper.find('[data-testid="plugin-widget-orphaned"]').exists()).toBe(false)
  })

  it('keeps Save disabled until the choice changes', async () => {
    const wrapper = await mountRow()

    expect(wrapper.get('[data-testid="plugin-widget-save"]').attributes('disabled')).toBeDefined()
  })

  it('adds a product from the select and emits the ids on Save', async () => {
    const wrapper = await mountRow()

    await wrapper.get('[data-testid="ui-select-trigger"]').trigger('click')
    const options = wrapper.findAll('[data-testid="ui-select-option"]')
    expect(options).toHaveLength(1)
    await options[0]!.trigger('mousedown')
    await wrapper.get('[data-testid="plugin-widget-save"]').trigger('click')

    expect(wrapper.emitted('save')).toEqual([[[3, 4]]])
  })

  it('removes a chip and emits the remaining ids', async () => {
    const wrapper = await mountRow({ widget: { ...widget, productIds: [3, 4] } })

    await wrapper.get('[data-testid="plugin-widget-remove-3"]').trigger('click')
    await wrapper.get('[data-testid="plugin-widget-save"]').trigger('click')

    expect(wrapper.emitted('save')).toEqual([[[4]]])
  })

  it('shows the refusal sentence', async () => {
    const wrapper = await mountRow({ error: 'Too many products.' })

    expect(wrapper.get('[data-testid="plugin-widget-error"]').text()).toBe('Too many products.')
  })
})
