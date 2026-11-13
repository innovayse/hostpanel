/**
 * Unit tests for UiCard: frosted glass surface, optional padding, attribute pass-through.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiCard from '@/components/ui/UiCard.vue'
import { UiCardRadius } from '@/utils/ui/card'

describe('UiCard', () => {
  it('renders the slot inside the surface classes', () => {
    const wrapper = mount(UiCard, { slots: { default: 'Body text' } })

    expect(wrapper.text()).toBe('Body text')
    expect(wrapper.classes()).toEqual(expect.arrayContaining(['glass', 'rounded-card']))
  })

  it('keeps the classes and attributes the caller passes', () => {
    const wrapper = mount(UiCard, { attrs: { class: 'p-5 mb-5', 'data-testid': 'card' } })

    expect(wrapper.classes()).toEqual(expect.arrayContaining(['p-5', 'mb-5']))
    expect(wrapper.attributes('data-testid')).toBe('card')
  })

  it('adds no padding by default and pads on request', () => {
    expect(mount(UiCard).classes()).not.toContain('p-5')
    expect(mount(UiCard, { props: { padding: 'md' } }).classes()).toContain('p-5')
  })

  it('rounds with the panel radius on request', () => {
    const wrapper = mount(UiCard, { props: { radius: UiCardRadius.Panel } })

    expect(wrapper.classes()).toContain('rounded-panel')
    expect(wrapper.classes()).not.toContain('rounded-card')
  })

  it('draws the frosted glass surface, which carries the light theme card shadow', () => {
    expect(mount(UiCard).classes()).toContain('glass')
  })

  it('swaps the body for placeholder lines while loading and keeps the header', () => {
    const wrapper = mount(UiCard, { props: { loading: true, skeletonLines: 4 }, slots: { header: '<h3>Title</h3>', default: 'Body text' } })

    expect(wrapper.text()).toContain('Title')
    expect(wrapper.text()).not.toContain('Body text')
    expect(wrapper.attributes('aria-busy')).toBe('true')
    expect(wrapper.findAll('[data-testid="ui-skeleton-bar"]')).toHaveLength(4)
  })

  it('draws the body and no placeholder once loaded', () => {
    const wrapper = mount(UiCard, { slots: { header: '<h3>Title</h3>', default: 'Body text' } })

    expect(wrapper.text()).toContain('Body text')
    expect(wrapper.find('[data-testid="ui-card-skeleton"]').exists()).toBe(false)
    expect(wrapper.attributes('aria-busy')).toBeUndefined()
  })

  it('draws the footer slot under the body with the standard divider, also while loading', () => {
    const wrapper = mount(UiCard, { props: { loading: true }, slots: { default: 'Body text', footer: '<span>Pager</span>' } })
    const footer = wrapper.get('[data-testid="ui-card-footer"]')

    expect(footer.text()).toBe('Pager')
    expect(footer.classes()).toEqual(expect.arrayContaining(['border-t', 'border-border', 'px-4', 'py-3']))
  })

  it('draws no footer strip without a footer slot', () => {
    expect(mount(UiCard, { slots: { default: 'Body text' } }).find('[data-testid="ui-card-footer"]').exists()).toBe(false)
  })

  it('moves to the denser frosted surface when elevated', () => {
    const wrapper = mount(UiCard, { props: { elevated: true } })

    expect(wrapper.classes()).toContain('glass-strong')
    expect(wrapper.classes()).not.toContain('glass')
  })
})
