/**
 * Unit tests for UiGrid: the responsive column presets, the gap and the tag.
 */
import { describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import UiGrid from '~/components/ui/UiGrid.vue'
import { UiGap, UiGridColumns, UiGridTag } from '~/utils/ui/enums'

describe('UiGrid', () => {
  it('is a grid with the default preset and gap and renders its slot', () => {
    const wrapper = mountWithKit(UiGrid, { slots: { default: 'cell' } })

    expect(wrapper.classes()).toEqual(expect.arrayContaining(['grid', 'grid-cols-1', 'sm:grid-cols-2', 'gap-4']))
    expect(wrapper.text()).toBe('cell')
  })

  it.each([
    [UiGridColumns.One, 'grid-cols-1'],
    [UiGridColumns.Three, 'lg:grid-cols-3'],
    [UiGridColumns.Four, 'lg:grid-cols-4'],
  ])('maps the %s preset to %s', (cols, expected) => {
    expect(mountWithKit(UiGrid, { props: { cols } }).classes()).toContain(expected)
  })

  it('keeps single-column presets free of larger breakpoints', () => {
    expect(mountWithKit(UiGrid, { props: { cols: UiGridColumns.One } }).classes()).not.toContain('sm:grid-cols-2')
  })

  it.each([
    [UiGap.None, 'gap-0'],
    [UiGap.Lg, 'gap-6'],
    [UiGap.Xl, 'gap-10'],
  ])('maps gap %s to %s', (gap, expected) => {
    expect(mountWithKit(UiGrid, { props: { gap } }).classes()).toContain(expected)
  })

  it('renders the requested tag', () => {
    expect(mountWithKit(UiGrid, { props: { as: UiGridTag.Ul } }).element.tagName).toBe('UL')
  })

  it('fits as many columns as the width allows for the auto preset', () => {
    const wrapper = mountWithKit(UiGrid, { props: { cols: UiGridColumns.Auto } })

    expect(wrapper.classes().join(' ')).toContain('auto-fit')
  })
})
