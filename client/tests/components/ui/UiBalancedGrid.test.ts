/**
 * Unit tests for UiBalancedGrid: the column variables follow the item count for 1, 2, 5 and 7
 * items, the rows are centred, and the tag is configurable.
 */
import { describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import UiBalancedGrid from '~/components/ui/UiBalancedGrid.vue'
import { balancedColumns, lastRowSize } from '~/utils/balancedGrid'
import { UiGridTag } from '~/utils/ui/enums'

/**
 * Mounts the grid with `count` cells.
 *
 * @param count Number of cells and the `count` prop.
 * @returns The wrapper.
 */
const mountGrid = (count: number) =>
  mountWithKit(UiBalancedGrid, {
    props: { count },
    slots: { default: Array.from({ length: count }, (_, i) => `<div class="cell">${i}</div>`).join('') },
  })

describe('UiBalancedGrid', () => {
  it.each([
    [1, '1', '1'],
    [2, '2', '2'],
    [5, '3', '2'],
    [7, '4', '2'],
  ])('uses the right columns for %i items', (count, desktop, tablet) => {
    const wrapper = mountGrid(count)

    expect(wrapper.attributes('data-columns')).toBe(desktop)
    expect(wrapper.classes().some((name) => name.startsWith('lg:[&>*]:basis-'))).toBe(true)
    expect(wrapper.classes().some((name) => name.startsWith('sm:[&>*]:basis-'))).toBe(true)
    expect(tablet).toBeTruthy()
    expect(wrapper.findAll('.cell')).toHaveLength(count)
  })

  it('is a wrapping, centred flex row so an incomplete last row sits in the middle', () => {
    expect(mountGrid(5).classes()).toEqual(expect.arrayContaining(['ui-balanced-grid', 'flex', 'flex-wrap', 'justify-center']))
  })

  it('renders the requested tag', () => {
    const wrapper = mountWithKit(UiBalancedGrid, { props: { count: 1, as: UiGridTag.Ul }, slots: { default: '<li>a</li>' } })

    expect(wrapper.element.tagName).toBe('UL')
  })

  it('sizes the cards inside display-contents wrappers such as UiAccent, so 7 cards lay out as 4 + 3', () => {
    const wrapper = mountWithKit(UiBalancedGrid, {
      props: { count: 7 },
      slots: { default: Array.from({ length: 7 }, (_, i) => `<div class="contents"><div class="cell">${i}</div></div>`).join('') },
    })

    // A `display: contents` wrapper is not a flex item, so the width has to reach its child.
    expect(wrapper.attributes('data-columns')).toBe('4')
    expect(wrapper.classes()).toEqual(expect.arrayContaining([
      'lg:[&>.contents>*]:basis-[calc((100%-4.5rem)/4)]',
      'sm:[&>.contents>*]:basis-[calc((100%-1.5rem)/2)]',
      '[&>.contents>*]:flex-none',
      '[&>.contents>*]:min-w-0',
    ]))
    expect(balancedColumns(7)).toBe(4)
    expect(lastRowSize(7, balancedColumns(7))).toBe(3)
  })
})

describe('UiBalancedGrid minColumns', () => {
  it('keeps a lone card at the width of a column in a three column grid, centred', () => {
    const wrapper = mountWithKit(UiBalancedGrid, { props: { count: 1, minColumns: 3 }, slots: { default: '<div class="cell">0</div>' } })

    expect(wrapper.attributes('data-columns')).toBe('3')
    expect(wrapper.classes()).toEqual(expect.arrayContaining(['justify-center', 'lg:[&>*]:basis-[calc((100%-3rem)/3)]', 'sm:[&>*]:basis-[calc((100%-1.5rem)/2)]']))
    expect(wrapper.classes()).not.toContain('lg:[&>*]:basis-full')
  })

  it('does not narrow a grid that already has more columns', () => {
    const wrapper = mountWithKit(UiBalancedGrid, { props: { count: 4, minColumns: 3 }, slots: { default: '<div>0</div>' } })

    expect(wrapper.attributes('data-columns')).toBe('4')
  })
})
