/**
 * Unit tests for UiStatRow: schema stats drawn as a row of tiles, with figures from values and a loading state.
 */
import { describe, expect, it } from 'vitest'
import UiStatRow from '@/components/ui/UiStatRow.vue'
import { mountWithApp } from '../../helpers/mount'
import type { SkeletonStat } from '@/utils/ui/skeletonSchema'

/** Two tiles. */
const stats: readonly SkeletonStat[] = [
  { key: 'clients', label: 'shared.clients' },
  { key: 'active', label: 'shared.status' },
]

/**
 * Mounts the row with the real catalogue.
 *
 * @param props - Props of the row.
 * @returns The mounted wrapper.
 */
const mountRow = (props: Record<string, unknown>) => mountWithApp(UiStatRow, { props })

describe('UiStatRow', () => {
  it('draws a tile per stat, with its caption from the locale key', () => {
    const wrapper = mountRow({ stats })

    expect(wrapper.findAll('[data-testid^="ui-stat-row-"]')).toHaveLength(2)
    expect(wrapper.get('[data-testid="ui-stat-row-clients"]').text()).toContain('Clients')
  })

  it('shows the figure of each tile from figures by its key', () => {
    const wrapper = mountRow({ stats, figures: { clients: { value: 42 }, active: { value: '7' } } })

    expect(wrapper.get('[data-testid="ui-stat-row-clients"]').text()).toContain('42')
    expect(wrapper.get('[data-testid="ui-stat-row-active"]').text()).toContain('7')
  })

  it('shows an empty figure for a key with no value yet', () => {
    const wrapper = mountRow({ stats })

    expect(wrapper.get('[data-testid="ui-stat-row-clients"]').text()).toBe('Clients')
  })

  it('keeps the captions and draws bars in place of the figures while loading', () => {
    const wrapper = mountRow({ stats, figures: { clients: { value: 42 } }, loading: true })

    expect(wrapper.findAll('[data-testid="ui-stat-tile-skeleton"]')).toHaveLength(2)
    expect(wrapper.text()).toContain('Clients')
    expect(wrapper.text()).not.toContain('42')
  })

  it('lays the tiles out two across on a phone and four across from the large breakpoint', () => {
    const classes = mountRow({ stats }).get('[data-testid="ui-stat-row"]').classes()

    expect(classes).toContain('grid-cols-2')
    expect(classes).toContain('lg:grid-cols-4')
    expect(classes).not.toContain('grid-cols-1')
  })

  it('takes the column count, and the test id prefix, from its props', () => {
    const wrapper = mountRow({ stats, columns: 6, testidPrefix: 'probe' })

    expect(wrapper.get('[data-testid="ui-stat-row"]').classes()).toContain('lg:grid-cols-6')
    expect(wrapper.find('[data-testid="probe-clients"]').exists()).toBe(true)
  })

  it('gives a tile its own caption, tone, change and loading state', () => {
    const wrapper = mountRow({
      stats,
      figures: {
        clients: { value: 5, label: 'Revenue, 90 days', tone: 'success', delta: '+3%', deltaTone: 'success' },
        active: { value: 1, loading: true },
      },
    })

    expect(wrapper.get('[data-testid="ui-stat-row-clients"]').text()).toContain('Revenue, 90 days')
    expect(wrapper.get('[data-testid="ui-stat-row-clients"] [data-testid="ui-stat-tile-delta"]').text()).toBe('+3%')
    expect(wrapper.find('[data-testid="ui-stat-row-active"] [data-testid="ui-stat-tile-skeleton"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="ui-stat-row-clients"] [data-testid="ui-stat-tile-skeleton"]').exists()).toBe(false)
  })

  it('draws plain captions over values, with a bar until the value is known, in the inline look', () => {
    const wrapper = mountRow({ stats, variant: 'inline', figures: { clients: { value: 42 } } })

    expect(wrapper.find('[data-testid="ui-stat-tile-skeleton"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="ui-stat-row-clients"]').text()).toContain('42')
    expect(wrapper.get('[data-testid="ui-stat-row-active"]').text()).toContain('Status')
    expect(wrapper.find('[data-testid="ui-stat-row-active"] [data-testid="ui-skeleton-bar"]').exists()).toBe(true)
  })
})
