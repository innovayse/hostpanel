/**
 * Tests for PluginEmptyBlock: title and sentence, both as text.
 *
 * @module tests/components/plugin/EmptyBlock.test
 */

import { describe, expect, it } from 'vitest'
import { mountBlocks } from '~/tests/helpers/mountPlugin'
import { PluginBlockKind } from '~/types/pluginblockkind'

describe('PluginEmptyBlock', () => {
  it('shows the title and sentence as text', () => {
    const wrapper = mountBlocks([{ kind: PluginBlockKind.Empty, title: '<b>No sites</b>', text: 'Create one.' }])

    const empty = wrapper.get('[data-testid="plugin-empty"]')
    expect(empty.text()).toContain('<b>No sites</b>')
    expect(empty.text()).toContain('Create one.')
    expect(empty.find('b').exists()).toBe(false)
  })
})
