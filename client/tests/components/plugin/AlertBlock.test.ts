/**
 * Tests for PluginAlertBlock: the tone mapped to the kit's variants, static (no live region), text escaped.
 *
 * @module tests/components/plugin/AlertBlock.test
 */

import { describe, expect, it } from 'vitest'
import { mountBlocks } from '~/tests/helpers/mountPlugin'
import { PluginBlockKind } from '~/types/pluginblockkind'
import { PluginBlockTone } from '~/types/pluginblocktone'

describe('PluginAlertBlock', () => {
  it('shows title and text as text, without a live role', () => {
    const wrapper = mountBlocks([{ kind: PluginBlockKind.Alert, title: 'Heads up', text: '<script>x</script>', tone: PluginBlockTone.Danger }])

    const alert = wrapper.get('[data-testid="plugin-alert"]')
    expect(alert.text()).toContain('Heads up')
    expect(alert.text()).toContain('<script>x</script>')
    expect(alert.find('script').exists()).toBe(false)
    expect(alert.attributes('role')).toBeUndefined()
  })

  it.each([
    [PluginBlockTone.Danger, 'bg-red-50'],
    [PluginBlockTone.Success, 'bg-green-50'],
    [undefined, 'bg-blue-50'],
  ])('draws the %s tone with the kit\'s variant', (tone, cls) => {
    const wrapper = mountBlocks([{ kind: PluginBlockKind.Alert, text: 'Note', tone }])

    expect(wrapper.get('[data-testid="plugin-alert"]').classes().join(' ')).toContain(cls)
  })
})
