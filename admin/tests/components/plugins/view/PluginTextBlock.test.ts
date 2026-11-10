/**
 * Unit tests for PluginTextBlock: a plugin's text is shown as the characters it is, never as markup.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { PluginBlockKind } from '@/types/pluginview'
import { mountPluginView } from '../../../helpers/pluginView'

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('PluginTextBlock', () => {
  it('shows markup a plugin writes as text', async () => {
    const { wrapper } = await mountPluginView([{ kind: PluginBlockKind.Text, text: '<b>bold</b> line' }])

    const text = wrapper.get('[data-testid="plugin-text"]')
    expect(text.text()).toBe('<b>bold</b> line')
    expect(text.find('b').exists()).toBe(false)
  })
})
