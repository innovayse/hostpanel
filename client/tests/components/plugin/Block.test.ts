/**
 * Tests for PluginBlock: each known kind draws its component, an unknown kind draws nothing.
 *
 * @module tests/components/plugin/Block.test
 */

import { describe, expect, it } from 'vitest'
import { mountBlocks } from '~/tests/helpers/mountPlugin'
import type { PluginBlock } from '~/types/pluginblock'
import { PluginBlockKind } from '~/types/pluginblockkind'

describe('PluginBlock', () => {
  it('draws a known kind with its component', () => {
    const wrapper = mountBlocks([{ kind: PluginBlockKind.Text, text: 'Hello' }])

    expect(wrapper.find('[data-testid="plugin-text"]').exists()).toBe(true)
  })

  it.each(['iframe', 'script', '__proto__', 'toString'])('draws nothing for the unknown kind %s', (kind) => {
    const wrapper = mountBlocks([{ kind, text: 'x' } as unknown as PluginBlock, { kind: PluginBlockKind.Text, text: 'after' }])

    expect(wrapper.findAll('[data-testid="plugin-view"] > *')).toHaveLength(1)
    expect(wrapper.get('[data-testid="plugin-text"]').text()).toBe('after')
  })
})
