/**
 * Tests for PluginSplitBlock: both panes draw their blocks in order, in the classes that stack them on a phone.
 *
 * @module tests/components/plugin/SplitBlock.test
 */

import { describe, expect, it } from 'vitest'
import { mountBlocks } from '~/tests/helpers/mountPlugin'
import { PluginBlockKind } from '~/types/pluginblockkind'

describe('PluginSplitBlock', () => {
  it('draws each pane with its own blocks, stacked below the md width and bounded beside it', () => {
    const wrapper = mountBlocks([{
      kind: PluginBlockKind.Split,
      panes: [
        { blocks: [{ kind: PluginBlockKind.Text, text: 'Left' }] },
        { blocks: [{ kind: PluginBlockKind.Text, text: 'Right' }] },
      ],
    }])

    const split = wrapper.get('[data-testid="plugin-split"]')
    expect(split.classes()).toContain('grid-cols-1')
    expect(split.classes()).toContain('md:grid-cols-[minmax(340px,420px)_minmax(0,1fr)]')
    expect(wrapper.get('[data-testid="plugin-split-pane-0"]').text()).toBe('Left')
    expect(wrapper.get('[data-testid="plugin-split-pane-1"]').text()).toBe('Right')
  })
})
