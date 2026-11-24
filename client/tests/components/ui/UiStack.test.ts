/**
 * Unit tests for UiStack: direction, gap, alignment, distribution, wrapping and the tag.
 */
import { describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import UiStack from '~/components/ui/UiStack.vue'
import { UiCrossAlign, UiDirection, UiGap, UiJustify, UiStackTag } from '~/utils/ui/enums'

describe('UiStack', () => {
  it('is a vertical flex column with the default gap and renders its slot', () => {
    const wrapper = mountWithKit(UiStack, { slots: { default: 'child' } })

    expect(wrapper.element.tagName).toBe('DIV')
    expect(wrapper.classes()).toEqual(expect.arrayContaining(['flex', 'flex-col', 'gap-4', 'items-stretch', 'justify-start']))
    expect(wrapper.text()).toBe('child')
  })

  it('lays out in a row', () => {
    const wrapper = mountWithKit(UiStack, { props: { direction: UiDirection.Row } })

    expect(wrapper.classes()).toContain('flex-row')
    expect(wrapper.classes()).not.toContain('flex-col')
  })

  it.each([
    [UiGap.None, 'gap-0'],
    [UiGap.Xs, 'gap-2'],
    [UiGap.Sm, 'gap-3'],
    [UiGap.Lg, 'gap-6'],
    [UiGap.Xl, 'gap-10'],
  ])('maps gap %s to %s', (gap, expected) => {
    expect(mountWithKit(UiStack, { props: { gap } }).classes()).toContain(expected)
  })

  it.each([
    [UiCrossAlign.Start, 'items-start'],
    [UiCrossAlign.Center, 'items-center'],
    [UiCrossAlign.End, 'items-end'],
  ])('maps align %s to %s', (align, expected) => {
    expect(mountWithKit(UiStack, { props: { align } }).classes()).toContain(expected)
  })

  it.each([
    [UiJustify.Center, 'justify-center'],
    [UiJustify.End, 'justify-end'],
    [UiJustify.Between, 'justify-between'],
  ])('maps justify %s to %s', (justify, expected) => {
    expect(mountWithKit(UiStack, { props: { justify } }).classes()).toContain(expected)
  })

  it('wraps only when asked', () => {
    expect(mountWithKit(UiStack, { props: { wrap: true } }).classes()).toContain('flex-wrap')
    expect(mountWithKit(UiStack).classes()).not.toContain('flex-wrap')
  })

  it('renders the requested tag', () => {
    expect(mountWithKit(UiStack, { props: { as: UiStackTag.Ul } }).element.tagName).toBe('UL')
  })
})
