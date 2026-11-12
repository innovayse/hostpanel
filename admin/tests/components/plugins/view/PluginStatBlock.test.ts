/**
 * Unit tests for PluginStatBlock: a written value, a money value read from a route, a badge value and
 * the API sentence of a failed read in place of the value.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { PluginBlockKind, PluginValueFormat } from '@/types/pluginview'
import { json, problem } from '../../../helpers/fetch'
import { ext, mountPluginView } from '../../../helpers/pluginView'

afterEach(() => {
  vi.unstubAllGlobals()
})

/** The route the stat reads. */
const usage = { route: { method: 'get', path: '/usage' }, field: 'spent' }

describe('PluginStatBlock', () => {
  it('shows the label and the written value', async () => {
    const { wrapper } = await mountPluginView([{ kind: PluginBlockKind.Stat, label: 'Sites', value: 3 }])

    const stat = wrapper.get('[data-testid="plugin-stat"]')
    expect(stat.text()).toContain('Sites')
    expect(stat.text()).toContain('3')
  })

  it('draws the stat as a tile', async () => {
    const { wrapper } = await mountPluginView([{ kind: PluginBlockKind.Stat, label: 'Sites', value: 3 }])

    expect(wrapper.get('[data-testid="plugin-stat"] p.font-display').text()).toBe('3')
  })

  it('formats a money value read from the route', async () => {
    const { wrapper } = await mountPluginView([{ kind: PluginBlockKind.Stat, label: 'Spent', source: usage, format: PluginValueFormat.Money }], {
      routes: { [`GET ${ext('usage')}`]: () => json({ spent: { amount: 12.5, currency: 'USD' } }) },
    })

    expect(wrapper.get('[data-testid="plugin-stat"]').text()).toContain('$12.50')
  })

  it('draws a badge value as a badge', async () => {
    const { wrapper } = await mountPluginView([{ kind: PluginBlockKind.Stat, label: 'State', value: 'Live', format: PluginValueFormat.Badge }])

    expect(wrapper.get('[data-testid="plugin-stat-badge"]').text()).toBe('Live')
  })

  it('shows the API sentence when the read fails', async () => {
    const { wrapper } = await mountPluginView([{ kind: PluginBlockKind.Stat, label: 'Spent', source: usage }], {
      routes: { [`GET ${ext('usage')}`]: () => problem('Usage is not available.', 502) },
    })

    expect(wrapper.get('[data-testid="plugin-stat"]').text()).toContain('Usage is not available.')
  })
})
