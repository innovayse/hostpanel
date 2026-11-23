/**
 * Unit tests for UiCard: the title, the header and footer slots, padding and the hover look.
 */
import { describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import UiCard from '~/components/ui/UiCard.vue'
import { UiCardPadding, UiCardTone } from '~/utils/ui/enums'

describe('UiCard', () => {
  it('renders the default slot in a framed surface', () => {
    const wrapper = mountWithKit(UiCard, { slots: { default: 'Body' } })

    expect(wrapper.text()).toBe('Body')
    expect(wrapper.classes()).toEqual(expect.arrayContaining(['rounded-2xl', 'border', 'p-6']))
  })

  it('renders the title as a heading', () => {
    const wrapper = mountWithKit(UiCard, { props: { title: 'Details' } })

    expect(wrapper.find('h3').text()).toBe('Details')
  })

  it('renders no heading without a title', () => {
    const wrapper = mountWithKit(UiCard)

    expect(wrapper.find('h3').exists()).toBe(false)
  })

  it('renders the header and footer slots', () => {
    const wrapper = mountWithKit(UiCard, { slots: { header: 'Head', footer: 'Foot' } })

    expect(wrapper.text()).toContain('Head')
    expect(wrapper.text()).toContain('Foot')
  })

  it.each([
    [UiCardPadding.Sm, 'p-4'],
    [UiCardPadding.Md, 'p-5'],
    [UiCardPadding.Xl, 'p-6'],
    [UiCardPadding.Xl, 'sm:p-8'],
  ])('maps padding %s to %s', (padding, expected) => {
    const wrapper = mountWithKit(UiCard, { props: { padding } })

    expect(wrapper.classes()).toContain(expected)
  })

  it('has no padding class for none', () => {
    const wrapper = mountWithKit(UiCard, { props: { padding: UiCardPadding.None } })

    expect(wrapper.classes().some((name) => /^p-\d/.test(name))).toBe(false)
  })

  it('adds the hover transition only when asked', () => {
    expect(mountWithKit(UiCard, { props: { hover: true } }).classes()).toContain('transition-colors')
    expect(mountWithKit(UiCard).classes()).not.toContain('transition-colors')
  })

  it('is structure only when bare: the screen paints background and border colour', () => {
    const wrapper = mountWithKit(UiCard, { props: { tone: UiCardTone.Bare, hover: true } })

    expect(wrapper.classes()).toEqual(expect.arrayContaining(['rounded-2xl', 'border', 'p-6']))
    expect(wrapper.classes().some((name) => name.startsWith('bg-') || name.startsWith('border-') || name.includes('dark:'))).toBe(false)
  })

  it('lifts the default card off the light page with a shadow that the dark page drops', () => {
    const wrapper = mountWithKit(UiCard)

    expect(wrapper.classes()).toEqual(expect.arrayContaining(['shadow-sm', 'dark:shadow-none']))
  })
})
