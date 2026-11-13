/**
 * Unit tests for UiGrid: responsive column classes and gap.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiGrid from '@/components/ui/UiGrid.vue'

describe('UiGrid', () => {
  it('uses two responsive columns by default', () => {
    const wrapper = mount(UiGrid)

    expect(wrapper.classes()).toEqual(expect.arrayContaining(['grid', 'grid-cols-1', 'md:grid-cols-2', 'gap-4']))
  })

  it.each([
    [3, 'lg:grid-cols-3'],
    [4, 'lg:grid-cols-4'],
    [6, 'lg:grid-cols-6'],
  ] as const)('maps %i columns to %s', (columns, expected) => {
    expect(mount(UiGrid, { props: { columns } }).classes()).toContain(expected)
  })

  it('fills the row with as many columns as fit when given a minimum item width', () => {
    const classes = mount(UiGrid, { props: { minItemWidth: 'md', columns: 3 } }).classes()

    expect(classes).toContain('sm:grid-cols-[repeat(auto-fill,minmax(18rem,1fr))]')
    expect(classes).not.toContain('lg:grid-cols-3')
  })

  it('applies the chosen gap', () => {
    expect(mount(UiGrid, { props: { gap: 'xl' } }).classes()).toContain('gap-8')
  })
})
