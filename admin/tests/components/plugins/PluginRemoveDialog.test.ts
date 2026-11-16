/**
 * Unit tests for PluginRemoveDialog: the confirmation names the plugin and says its data is kept, a confirm and a
 * close are reported to the screen, and nothing shows while it is closed.
 */
import { describe, expect, it } from 'vitest'
import PluginRemoveDialog from '@/components/plugins/PluginRemoveDialog.vue'
import { mountScreen } from '../../helpers/mount'

describe('PluginRemoveDialog', () => {
  it('names the plugin and says its settings and data are kept', async () => {
    const { wrapper } = await mountScreen(PluginRemoveDialog, { open: true, pluginId: 'acme', loading: false })

    const text = wrapper.get('[data-testid="plugin-remove-dialog"]').text()
    expect(text).toContain('acme')
    expect(text).toContain('stored data are kept')
  })

  it('shows nothing while it is closed', async () => {
    const { wrapper } = await mountScreen(PluginRemoveDialog, { open: false, pluginId: 'acme', loading: false })

    expect(wrapper.find('[data-testid="plugin-remove-dialog"]').exists()).toBe(false)
  })

  it('reports the confirm and the close', async () => {
    const { wrapper } = await mountScreen(PluginRemoveDialog, { open: true, pluginId: 'acme', loading: false })

    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await wrapper.get('[data-testid="dialog-cancel"]').trigger('click')

    expect(wrapper.emitted('confirm')).toHaveLength(1)
    expect(wrapper.emitted('close')).toHaveLength(1)
  })
})
