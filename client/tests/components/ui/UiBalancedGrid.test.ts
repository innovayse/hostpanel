/**
 * Unit tests for UiBalancedGrid: the column variables follow the item count for 1, 2, 5 and 7
 * items, the rows are centred, and the tag is configurable.
 */
import { describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import UiBalancedGrid from '~/components/ui/UiBalancedGrid.vue'

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
    const root = wrapper.element as HTMLElement

    expect(root.getAttribute('data-columns')).toBe(desktop)
    expect(root.getAttribute('data-columns-md')).toBe(tablet)
    expect(wrapper.findAll('.cell')).toHaveLength(count)
  })

  it('is a wrapping, centred flex row so an incomplete last row sits in the middle', () => {
    expect(mountGrid(5).classes()).toContain('ui-balanced-grid')
  })

  it('renders the requested tag', () => {
    const wrapper = mountWithKit(UiBalancedGrid, { props: { count: 1, as: 'ul' }, slots: { default: '<li>a</li>' } })

    expect(wrapper.element.tagName).toBe('UL')
  })
})
