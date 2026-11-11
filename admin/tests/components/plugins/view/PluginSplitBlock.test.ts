/**
 * Unit tests for PluginSplitBlock: two panes side by side, each drawing its own blocks in order.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { PluginBlockKind } from '@/types/pluginview'
import { mountPluginView } from '../../../helpers/pluginView'

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('PluginSplitBlock', () => {
  it('draws each pane with its own blocks', async () => {
    const { wrapper } = await mountPluginView([
      {
        kind: PluginBlockKind.Split,
        panes: [
          { blocks: [{ kind: PluginBlockKind.Text, text: 'Left' }] },
          { blocks: [{ kind: PluginBlockKind.Text, text: 'Right' }, { kind: PluginBlockKind.Text, text: 'Below' }] },
        ],
      },
    ])

    expect(wrapper.get('[data-testid="plugin-split"]').classes()).toContain('lg:grid-cols-2')
    expect(wrapper.get('[data-testid="plugin-split-pane-0"]').text()).toBe('Left')
    expect(wrapper.get('[data-testid="plugin-split-pane-1"]').findAll('[data-testid="plugin-text"]').map((text) => text.text())).toEqual(['Right', 'Below'])
  })
})
