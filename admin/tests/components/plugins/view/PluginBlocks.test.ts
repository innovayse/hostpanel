/**
 * Unit tests for PluginBlocks: consecutive stats share one tile grid, consecutive links one button row, every
 * other block stands alone, and a view and a tab lay a list out the same way.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import PluginBlocks from '@/components/plugins/view/PluginBlocks.vue'
import { PluginBlockKind } from '@/types/pluginview'
import type { PluginBlock } from '@/types/pluginview'
import { mountPluginView } from '../../../helpers/pluginView'

afterEach(() => {
  vi.unstubAllGlobals()
})

/** A stat block. */
const stat = (label: string): PluginBlock => ({ kind: PluginBlockKind.Stat, label, value: 1 })

/** A link block to a host-relative path. */
const link = (label: string): PluginBlock => ({ kind: PluginBlockKind.Link, label, href: '/clients' })

/** A text block. */
const text = (value: string): PluginBlock => ({ kind: PluginBlockKind.Text, text: value })

describe('PluginBlocks', () => {
  it('puts a run of stats in one tile grid and a run of links in one button row', async () => {
    const { wrapper } = await mountPluginView([stat('A'), stat('B'), text('between'), link('One'), link('Two')])

    expect(wrapper.findComponent(PluginBlocks).exists()).toBe(true)
    const grids = wrapper.findAll('[data-testid="plugin-stats"]')
    expect(grids).toHaveLength(1)
    expect(grids[0]?.findAll('[data-testid="plugin-stat"]')).toHaveLength(2)
    const rows = wrapper.findAll('[data-testid="plugin-links"]')
    expect(rows).toHaveLength(1)
    expect(rows[0]?.findAll('[data-testid="plugin-link"]').map((l) => l.text())).toEqual(['One', 'Two'])
    expect(rows[0]?.classes()).toContain('flex-wrap')
  })

  it('starts a new group when another block breaks the run, keeping document order', async () => {
    const { wrapper } = await mountPluginView([stat('A'), text('between'), stat('B')])

    expect(wrapper.findAll('[data-testid="plugin-stats"]')).toHaveLength(2)
    expect(wrapper.get('[data-testid="plugin-view"]').element.children[1]?.getAttribute('data-testid')).toBe('plugin-text')
  })

  it('lays out the blocks of a tab the same way', async () => {
    const { wrapper } = await mountPluginView([{
      kind: PluginBlockKind.Tabs,
      tabs: [{ id: 'one', title: 'One', blocks: [stat('A'), stat('B'), link('Go')] }],
    }])

    expect(wrapper.findAllComponents(PluginBlocks)).toHaveLength(2)
    expect(wrapper.get('[data-testid="plugin-stats"]').findAll('[data-testid="plugin-stat"]')).toHaveLength(2)
    expect(wrapper.get('[data-testid="plugin-links"]').text()).toBe('Go')
  })

  it('draws a lone link as a row of one', async () => {
    const { wrapper } = await mountPluginView([text('a'), link('Only')])

    expect(wrapper.find('[data-testid="plugin-stats"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="plugin-links"]').findAll('[data-testid="plugin-link"]')).toHaveLength(1)
  })
})
