/**
 * Unit tests for UiBadge: tone colours, dot, marker and compact size.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiBadge from '@/components/ui/UiBadge.vue'

describe('UiBadge', () => {
  it('renders its slot in a pill', () => {
    const wrapper = mount(UiBadge, { slots: { default: 'Active' } })

    expect(wrapper.text()).toBe('Active')
    expect(wrapper.classes()).toContain('rounded-pill')
  })

  it('takes its colours from the tone', () => {
    const wrapper = mount(UiBadge, { props: { tone: 'success' } })

    expect(wrapper.classes()).toEqual(expect.arrayContaining(['bg-tint-green', 'text-status-green']))
    expect(wrapper.classes()).not.toContain('border')
  })

  it('draws a status dot on request', () => {
    const plain = mount(UiBadge)
    const dotted = mount(UiBadge, { props: { dot: true, tone: 'danger' } })

    expect(plain.element.children).toHaveLength(0)
    expect(dotted.element.children).toHaveLength(1)
    expect(dotted.element.children[0]?.className).toContain('bg-status-red')
  })

  it('draws a bare solid dot of the tone as a marker, without its slot', () => {
    const wrapper = mount(UiBadge, { props: { marker: true, tone: 'primary' }, slots: { default: '3' } })

    expect(wrapper.text()).toBe('')
    expect(wrapper.classes()).toEqual(expect.arrayContaining(['rounded-full', 'h-2.5', 'w-2.5', 'bg-primary-500']))
    expect(wrapper.classes()).not.toContain('border')
  })

  it('shrinks in the small size', () => {
    expect(mount(UiBadge, { props: { small: true } }).classes()).toContain('text-[0.65rem]')
  })

  it('sets the full size as the concept pill: 12px, semibold, 4px by 10px', () => {
    expect(mount(UiBadge).classes()).toEqual(expect.arrayContaining(['text-xs', 'font-semibold', 'px-2.5', 'py-1']))
  })
})
