/**
 * Tests for PluginHeaderBlock: the title as text and its actions beside it.
 *
 * @module tests/components/plugin/HeaderBlock.test
 */

import { describe, expect, it } from 'vitest'
import { mountBlocks, TEST_ROUTES } from '~/tests/helpers/mountPlugin'
import { PluginBlockKind } from '~/types/pluginblockkind'

describe('PluginHeaderBlock', () => {
  it('shows the title as text and draws each action', () => {
    const wrapper = mountBlocks([{
      kind: PluginBlockKind.Header,
      title: '<i>Site</i>',
      actions: [
        { kind: PluginBlockKind.Action, label: 'Publish', route: TEST_ROUTES.publish },
        { kind: PluginBlockKind.Action, label: 'Publish again', route: TEST_ROUTES.publish },
      ],
    }])

    const header = wrapper.get('[data-testid="plugin-header"]')
    expect(header.text()).toContain('<i>Site</i>')
    expect(header.find('i').exists()).toBe(false)
    expect(wrapper.findAll('[data-testid="plugin-action-button"]').map(b => b.text())).toEqual(['Publish', 'Publish again'])
  })
})
