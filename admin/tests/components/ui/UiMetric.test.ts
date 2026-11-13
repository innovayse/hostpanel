/**
 * Unit tests for UiMetric: value, caption, tone, sizes, slot and loading skeleton.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiMetric from '@/components/ui/UiMetric.vue'

describe('UiMetric', () => {
  it('shows the value in the display font in tabular figures', () => {
    const wrapper = mount(UiMetric, { props: { value: 1200 } })

    expect(wrapper.get('p').text()).toBe('1200')
    expect(wrapper.get('p').classes()).toEqual(expect.arrayContaining(['font-display', 'tabular-nums', 'text-[1.875rem]']))
  })

  it('shows the label and the caption only when given', () => {
    expect(mount(UiMetric, { props: { value: 1 } }).text()).toBe('1')

    const wrapper = mount(UiMetric, { props: { value: 1, label: 'Accounts', caption: 'since May' } })

    expect(wrapper.text()).toContain('Accounts')
    expect(wrapper.text()).toContain('since May')
  })

  it('colours the value from the tone', () => {
    expect(mount(UiMetric, { props: { value: 3, tone: 'danger' } }).get('p').classes()).toContain('text-status-red')
  })

  it('sizes the value from the size', () => {
    expect(mount(UiMetric, { props: { value: 3, size: 'sm' } }).get('p').classes()).toContain('text-xl')
    expect(mount(UiMetric, { props: { value: 3, size: 'lg' } }).get('p').classes()).toContain('text-4xl')
  })

  it('draws the slot in place of the plain value', () => {
    const wrapper = mount(UiMetric, { props: { value: 1 }, slots: { default: '<b data-testid="custom">Live</b>' } })

    expect(wrapper.get('p [data-testid="custom"]').text()).toBe('Live')
  })

  it('draws one bar of the value size in place of the number, keeping the label and caption, while loading', () => {
    const wrapper = mount(UiMetric, { props: { value: 1200, label: 'Accounts', caption: 'in total', loading: true } })

    expect(wrapper.findAll('[data-testid="ui-skeleton-bar"]')).toHaveLength(1)
    expect(wrapper.get('[data-testid="ui-skeleton-bar"]').classes()).toContain('h-[30px]')
    expect(wrapper.find('p.font-display').exists()).toBe(false)
    expect(wrapper.text()).not.toContain('1200')
    expect(wrapper.text()).toContain('Accounts')
    expect(wrapper.text()).toContain('in total')
    expect(wrapper.attributes('aria-busy')).toBe('true')
  })
})
