/**
 * Unit tests for UiSkeleton: single shapes, line count, the shorter last line and the three
 * presets (table, cards) with their counts.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiSkeleton from '@/components/ui/UiSkeleton.vue'

/** Selector of one shimmering bar. */
const BAR = '[data-testid="ui-skeleton-bar"]'

describe('UiSkeleton', () => {
  it('is hidden from assistive technology and shimmers', () => {
    const wrapper = mount(UiSkeleton)

    expect(wrapper.attributes('aria-hidden')).toBe('true')
    expect(wrapper.find(BAR).exists()).toBe(true)
  })

  it('draws one line by default and the requested number of lines', () => {
    expect(mount(UiSkeleton).findAll(BAR)).toHaveLength(1)
    expect(mount(UiSkeleton, { props: { lines: 3 } }).findAll(BAR)).toHaveLength(3)
  })

  it('shortens only the last of several lines', () => {
    const rows = mount(UiSkeleton, { props: { lines: 3 } }).findAll(BAR)

    expect(rows[0]?.classes()).toContain('w-full')
    expect(rows[2]?.classes()).toContain('w-2/3')
  })

  it('draws a single shape for block and circle whatever the line count', () => {
    expect(mount(UiSkeleton, { props: { shape: 'block', lines: 4 } }).findAll(BAR)).toHaveLength(1)
    expect(mount(UiSkeleton, { props: { shape: 'circle' } }).get(BAR).classes()).toContain('rounded-full')
  })

  it('draws a table preset: a header row and the requested body rows of cells', () => {
    const wrapper = mount(UiSkeleton, { props: { shape: 'table', count: 3, columns: 5 } })

    expect(wrapper.findAll('[data-testid="ui-skeleton-table-head"]')).toHaveLength(1)
    const rows = wrapper.findAll('[data-testid="ui-skeleton-table-row"]')
    expect(rows).toHaveLength(3)
    expect(rows[0]?.findAll(BAR)).toHaveLength(5)
  })

  it('draws a table of five rows and four columns by default', () => {
    const wrapper = mount(UiSkeleton, { props: { shape: 'table' } })

    expect(wrapper.findAll('[data-testid="ui-skeleton-table-row"]')).toHaveLength(5)
    expect(wrapper.get('[data-testid="ui-skeleton-table-head"]').findAll(BAR)).toHaveLength(4)
  })

  it('draws a card grid on the frosted surface', () => {
    const wrapper = mount(UiSkeleton, { props: { shape: 'cards', count: 4 } })

    const cards = wrapper.findAll('[data-testid="ui-skeleton-card"]')
    expect(cards).toHaveLength(4)
    expect(cards[0]?.classes()).toContain('glass')
    expect(wrapper.classes()).toContain('grid')
    expect(mount(UiSkeleton, { props: { shape: 'cards' } }).findAll('[data-testid="ui-skeleton-card"]')).toHaveLength(6)
  })

  it('keeps every preset hidden from assistive technology', () => {
    for (const shape of ['table', 'cards'] as const) {
      expect(mount(UiSkeleton, { props: { shape } }).attributes('aria-hidden')).toBe('true')
    }
  })
})
