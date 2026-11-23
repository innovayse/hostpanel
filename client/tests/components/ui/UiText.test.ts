/**
 * Unit tests for UiText: the tag, size, colour role, weight and truncation.
 */
import { describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import UiText from '~/components/ui/UiText.vue'
import { UiTextSize, UiTextTag, UiTextTone, UiTextWeight } from '~/utils/ui/enums'

describe('UiText', () => {
  it('is a base-size paragraph with its slot', () => {
    const wrapper = mountWithKit(UiText, { slots: { default: 'Hello' } })

    expect(wrapper.element.tagName).toBe('P')
    expect(wrapper.text()).toBe('Hello')
    expect(wrapper.classes()).toEqual(expect.arrayContaining(['text-base', 'font-normal', 'text-gray-900']))
  })

  it('renders the requested tag', () => {
    expect(mountWithKit(UiText, { props: { as: UiTextTag.Span } }).element.tagName).toBe('SPAN')
  })

  it.each([
    [UiTextSize.Xs, 'text-xs'],
    [UiTextSize.Sm, 'text-sm'],
    [UiTextSize.Lg, 'text-lg'],
  ])('maps size %s to %s', (size, expected) => {
    expect(mountWithKit(UiText, { props: { size } }).classes()).toContain(expected)
  })

  it.each([
    [UiTextTone.Muted, 'text-gray-600'],
    [UiTextTone.Subtle, 'text-gray-500'],
    [UiTextTone.Primary, 'text-primary-600'],
    [UiTextTone.Success, 'text-green-700'],
    [UiTextTone.Danger, 'text-red-600'],
  ])('paints the %s role with %s', (tone, expected) => {
    expect(mountWithKit(UiText, { props: { tone } }).classes()).toContain(expected)
  })

  it('keeps the muted and subtle roles readable on the dark surface', () => {
    expect(mountWithKit(UiText, { props: { tone: UiTextTone.Muted } }).classes()).toContain('dark:text-gray-300')
    expect(mountWithKit(UiText, { props: { tone: UiTextTone.Subtle } }).classes()).toContain('dark:text-gray-400')
  })

  it.each([
    [UiTextWeight.Medium, 'font-medium'],
    [UiTextWeight.Semibold, 'font-semibold'],
    [UiTextWeight.Bold, 'font-bold'],
  ])('maps weight %s to %s', (weight, expected) => {
    expect(mountWithKit(UiText, { props: { weight } }).classes()).toContain(expected)
  })

  it('truncates only when asked', () => {
    expect(mountWithKit(UiText, { props: { truncate: true } }).classes()).toContain('truncate')
    expect(mountWithKit(UiText).classes()).not.toContain('truncate')
  })

  it('paints no colour and no size for the inherit steps, so the screen supplies them', () => {
    const classes = mountWithKit(UiText, { props: { tone: UiTextTone.Inherit, size: UiTextSize.Inherit } }).classes()

    expect(classes).not.toContain('text-base')
    expect(classes.some(name => name.startsWith('text-gray'))).toBe(false)
    expect(classes).toContain('font-normal')
  })
})
