/**
 * Unit tests for PluginGlobalSettings: the signing card or its load error, the kept data list and the
 * purge prompt that reports a confirmed id only.
 */
import { describe, expect, it } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import PluginGlobalSettings from '@/components/plugins/PluginGlobalSettings.vue'
import { mountScreen } from '../../helpers/mount'
import { PluginSettingKey } from '@/types/setting'

/**
 * Mounts the tab.
 *
 * @param props - Props to change.
 * @returns The wrapper.
 */
const mountTab = async (props: Record<string, unknown> = {}) =>
  (await mountScreen(PluginGlobalSettings, {
    settings: [{ id: 9, key: PluginSettingKey.AllowUnsigned, value: 'false' }],
    error: null,
    loading: false,
    save: async () => ({ saved: true, error: null }),
    keptDataIds: [],
    purgingId: null,
    ...props,
  })).wrapper

describe('PluginGlobalSettings', () => {
  it('shows the signing switch and no kept data list when nothing is kept', async () => {
    const wrapper = await mountTab()

    expect(wrapper.find('[data-testid="plugin-allow-unsigned"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="plugins-kept-data"]').exists()).toBe(false)
  })

  it('shows the load error instead of the switch and emits retry', async () => {
    const wrapper = await mountTab({ error: 'Settings are unavailable.' })

    expect(wrapper.get('[data-testid="plugin-signing-error"]').text()).toContain('Settings are unavailable.')
    expect(wrapper.find('[data-testid="plugin-allow-unsigned"]').exists()).toBe(false)
    await wrapper.get('[data-testid="plugin-signing-error-retry"]').trigger('click')
    expect(wrapper.emitted('retry')).toHaveLength(1)
  })

  it('asks before purging and emits the id only after the confirmation', async () => {
    const wrapper = await mountTab({ keptDataIds: ['example-pay'] })

    await wrapper.get('[data-testid="plugin-purge-example-pay"]').trigger('click')
    await flushPromises()
    expect(wrapper.emitted('purge')).toBeUndefined()
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')

    expect(wrapper.emitted('purge')).toEqual([['example-pay']])
    expect(wrapper.find('[data-testid="plugins-purge-dialog"]').exists()).toBe(false)
  })

  it('emits nothing when the prompt is dismissed', async () => {
    const wrapper = await mountTab({ keptDataIds: ['example-pay'] })

    await wrapper.get('[data-testid="plugin-purge-example-pay"]').trigger('click')
    await wrapper.get('[data-testid="dialog-cancel"]').trigger('click')
    await flushPromises()

    expect(wrapper.emitted('purge')).toBeUndefined()
    expect(wrapper.find('[data-testid="plugins-purge-dialog"]').exists()).toBe(false)
  })
})
