/**
 * Mount tests for UiDetailCard: the title, the slot with the real body, and the placeholder of each part of a card
 * (identity, rows, fields, lines, tiles, table, chart, actions) while it loads, and the bare tab strip of a tabs card.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiDetailCard from '@/components/ui/UiDetailCard.vue'
import { ChartKind, UiChartHeight } from '@/utils/ui/chart'
import type { SkeletonDetailCard } from '@/utils/ui/skeletonSchema'
import { createTestI18n } from '../../helpers/i18n'

/**
 * Mounts a card of the schema with the real catalogue and a body in the default slot.
 *
 * @param card - The card of the schema.
 * @param loading - Whether the card shows its placeholder.
 * @returns The wrapper.
 */
const mountCard = (card: SkeletonDetailCard, loading = false) =>
  mount(UiDetailCard, {
    props: { card, loading },
    slots: { default: '<p data-testid="body">Real body</p>' },
    global: { plugins: [createTestI18n()] },
  })

describe('UiDetailCard', () => {
  it('draws the translated title over the real body from the slot', () => {
    const wrapper = mountCard({ key: 'profile', title: 'shared.name' })

    expect(wrapper.get('[data-testid="ui-detail-card-profile"]').find('h2').text()).toBe('Name')
    expect(wrapper.get('[data-testid="body"]').text()).toBe('Real body')
    expect(wrapper.attributes('aria-busy')).toBeUndefined()
  })

  it('draws no title when the schema names none', () => {
    const wrapper = mountCard({ key: 'profile' })

    expect(wrapper.find('h2').exists()).toBe(false)
  })

  it('replaces the body with the placeholder while loading, keeping the title and marking the card busy', () => {
    const wrapper = mountCard({ key: 'profile', title: 'shared.name', rows: ['shared.id'] }, true)

    expect(wrapper.find('[data-testid="body"]').exists()).toBe(false)
    expect(wrapper.find('h2').text()).toBe('Name')
    expect(wrapper.attributes('aria-busy')).toBe('true')
  })

  it('keeps the real caption of each row over a bar while loading', () => {
    const wrapper = mountCard({ key: 'profile', rows: ['shared.id', 'shared.name'] }, true)
    const rows = wrapper.findAll('[data-testid="ui-detail-row"]')

    expect(rows.map((row) => row.text())).toEqual(['ID', 'Name'])
    expect(rows[0]?.find('[data-testid="ui-skeleton-bar"]').exists()).toBe(true)
  })

  it('draws the identity head, the field boxes, the lines, the tiles and the buttons the schema asks for', () => {
    const wrapper = mountCard({ key: 'profile', identity: true, fields: 4, lines: 3, tiles: 2, actions: 2 }, true)

    expect(wrapper.find('[data-testid="ui-detail-identity"]').exists()).toBe(true)
    expect(wrapper.findAll('[data-testid="ui-detail-tiles"] > *')).toHaveLength(2)
    expect(wrapper.findAll('[data-testid="ui-form-actions"] [data-testid="ui-skeleton-bar"]')).toHaveLength(2)
  })

  it('draws the lines as full width bars with the last one shorter, as wrapped text is', () => {
    const bars = mountCard({ key: 'profile', lines: 3 }, true).findAll('[data-testid="ui-skeleton-bar"]')

    expect(bars.map((bar) => bar.classes().find((name) => name.startsWith('w-')))).toEqual(['w-full', 'w-full', 'w-2/3'])
  })

  it('draws a single line at full width', () => {
    const bars = mountCard({ key: 'profile', lines: 1 }, true).findAll('[data-testid="ui-skeleton-bar"]')

    expect(bars.map((bar) => bar.classes().find((name) => name.startsWith('w-')))).toEqual(['w-full'])
  })

  it('draws nothing of a part the schema does not have', () => {
    const wrapper = mountCard({ key: 'profile', title: 'shared.name' }, true)

    expect(wrapper.find('[data-testid="ui-detail-identity"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="ui-detail-row"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="ui-detail-tiles"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="ui-table-skeleton"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="chart-skeleton"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="ui-form-actions"]').exists()).toBe(false)
  })

  it('draws the table placeholder with its real captions', () => {
    const wrapper = mountCard({ key: 'items', table: { columns: [{ key: 'id', label: 'shared.id' }, { key: 'name', label: 'shared.name' }] } }, true)

    expect(wrapper.findAll('[role="columnheader"]').map((header) => header.text())).toEqual(['ID', 'Name'])
  })

  it('draws the placeholder of the chart in its kind at the height the schema names, the medium one by default', () => {
    const named = mountCard({ key: 'trend', chart: { kind: ChartKind.Area, height: UiChartHeight.Short } }, true)
    const plain = mountCard({ key: 'trend', chart: { kind: ChartKind.Line } }, true)

    expect(named.get('[data-testid="chart-skeleton"]').attributes('data-kind')).toBe(ChartKind.Area)
    expect(named.get('[data-testid="chart-skeleton"]').classes()).toContain('h-52')
    expect(plain.get('[data-testid="chart-skeleton"]').classes()).toContain('h-64')
  })

  it('is a bare tab strip with no card around it when the schema has tabs: the real tabs in the slot, the captions while loading', () => {
    const card: SkeletonDetailCard = { key: 'sections', tabs: [{ key: 'a', label: 'shared.id' }, { key: 'b', label: 'shared.name' }] }
    const ready = mountCard(card)
    const loading = mountCard(card, true)

    expect(ready.get('[data-testid="ui-detail-card-sections"]').find('[data-testid="body"]').exists()).toBe(true)
    expect(ready.find('[role="tab"]').exists()).toBe(false)
    expect(loading.find('[data-testid="body"]').exists()).toBe(false)
    expect(loading.findAll('[role="tab"]').map((tab) => tab.text())).toEqual(['ID', 'Name'])
  })
})
