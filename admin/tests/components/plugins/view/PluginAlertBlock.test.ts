/**
 * Unit tests for PluginAlertBlock: the title and sentence as text, the tone mapped to the kit's alert.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { PluginBlockKind, PluginBlockTone } from '@/types/pluginview'
import { mountPluginView } from '../../../helpers/pluginView'

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('PluginAlertBlock', () => {
  it('shows the title and the sentence in the danger tone', async () => {
    const { wrapper } = await mountPluginView([{ kind: PluginBlockKind.Alert, title: 'Heads up', text: 'Quota reached.', tone: PluginBlockTone.Danger }])

    const alert = wrapper.get('[data-testid="plugin-alert"]')
    expect(alert.get('[data-testid="plugin-alert-title"]').text()).toBe('Heads up')
    expect(alert.text()).toContain('Quota reached.')
    expect(alert.attributes('role')).toBe('alert')
  })

  it('reads a neutral notice as information, announced politely', async () => {
    const { wrapper } = await mountPluginView([{ kind: PluginBlockKind.Alert, text: 'Note.' }])

    expect(wrapper.get('[data-testid="plugin-alert"]').attributes('role')).toBe('status')
    expect(wrapper.find('[data-testid="plugin-alert-title"]').exists()).toBe(false)
  })
})
