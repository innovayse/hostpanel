/**
 * Unit tests for UiSection: the tag, padding, background and the optional page-width container.
 */
import { describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import UiSection from '~/components/ui/UiSection.vue'
import { UiBlockTag, UiSectionPadding, UiSectionTone } from '~/utils/ui/enums'

describe('UiSection', () => {
  it('is a section whose content sits in the page container', () => {
    const wrapper = mountWithKit(UiSection, { slots: { default: 'content' } })

    expect(wrapper.element.tagName).toBe('SECTION')
    expect(wrapper.find('.container-custom').text()).toBe('content')
  })

  it('keeps the band full width: the container is an inner child, never the root', () => {
    const wrapper = mountWithKit(UiSection, { props: { tone: UiSectionTone.Muted }, slots: { default: 'content' } })

    expect(wrapper.classes()).toContain('bg-gray-50')
    expect(wrapper.classes()).not.toContain('container-custom')
    expect(wrapper.find('.container-custom').element.parentElement).toBe(wrapper.element)
  })

  it('renders the content directly when not contained', () => {
    const wrapper = mountWithKit(UiSection, { props: { contained: false }, slots: { default: 'content' } })

    expect(wrapper.find('.container-custom').exists()).toBe(false)
    expect(wrapper.text()).toBe('content')
  })

  it.each([
    [UiSectionPadding.Sm, 'py-8'],
    [UiSectionPadding.Md, 'py-10'],
    [UiSectionPadding.Lg, 'md:py-20'],
  ])('maps padding %s to %s', (padding, expected) => {
    expect(mountWithKit(UiSection, { props: { padding } }).classes()).toContain(expected)
  })

  it('adds no vertical padding for the none step', () => {
    const classes = mountWithKit(UiSection, { props: { padding: UiSectionPadding.None } }).classes()

    expect(classes.some((name) => name.startsWith('py-') || name.startsWith('md:py-'))).toBe(false)
  })

  it('tints the muted tone only', () => {
    expect(mountWithKit(UiSection, { props: { tone: UiSectionTone.Muted } }).classes()).toContain('bg-gray-50')
    expect(mountWithKit(UiSection).classes()).not.toContain('bg-gray-50')
  })

  it('renders the requested tag', () => {
    expect(mountWithKit(UiSection, { props: { as: UiBlockTag.Article } }).element.tagName).toBe('ARTICLE')
  })
})
