/**
 * Tests for PluginTextBlock: text shown as written, markup escaped, line breaks kept.
 *
 * @module tests/components/plugin/TextBlock.test
 */

import { describe, expect, it } from 'vitest'
import { mountBlocks } from '~/tests/helpers/mountPlugin'
import { PluginBlockKind } from '~/types/pluginblockkind'

describe('PluginTextBlock', () => {
  it('shows markup a plugin writes as the characters it is', () => {
    const wrapper = mountBlocks([{ kind: PluginBlockKind.Text, text: '<img src=x onerror=alert(1)>Hello' }])

    const text = wrapper.get('[data-testid="plugin-text"]')
    expect(text.text()).toBe('<img src=x onerror=alert(1)>Hello')
    expect(text.find('img').exists()).toBe(false)
  })

  it('keeps line breaks', () => {
    const wrapper = mountBlocks([{ kind: PluginBlockKind.Text, text: 'one\ntwo' }])

    expect(wrapper.get('[data-testid="plugin-text"]').classes()).toContain('whitespace-pre-line')
  })
})
