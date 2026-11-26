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
    expect(wrapper.classes()).toContain('rounded-2xl')
  })

  it('ends a label longer than its row in an ellipsis, never wider than the row', () => {
    const wrapper = mount(UiBadge, { slots: { default: 'A very long sentence' } })

    expect(wrapper.classes()).toContain('max-w-full')
    expect(wrapper.get('.truncate').classes()).toContain('min-w-0')
    expect(wrapper.get('.truncate').text()).toBe('A very long sentence')
  })

  it('keeps a long label on one line instead of wrapping it', () => {
    const classes = mount(UiBadge, { slots: { default: 'A very long sentence' } }).classes()

    expect(classes).toEqual(expect.arrayContaining(['whitespace-nowrap', 'shrink-0']))
    expect(classes).not.toContain('whitespace-normal')
    expect(classes).not.toContain('break-words')
    expect(classes).not.toContain('rounded-pill')
  })

  it('has one fixed height per size and no vertical padding', () => {
    const full = mount(UiBadge).classes()
    const small = mount(UiBadge, { props: { small: true } }).classes()

    expect(full).toContain('h-6')
    expect(small).toContain('h-4')
    expect(full.some((name) => name.startsWith('py-'))).toBe(false)
    expect(small.some((name) => name.startsWith('py-'))).toBe(false)
  })

  it('takes its colours from the tone', () => {
    const wrapper = mount(UiBadge, { props: { tone: 'success' } })

    expect(wrapper.classes()).toEqual(expect.arrayContaining(['bg-tint-green', 'text-status-green']))
    expect(wrapper.classes()).not.toContain('border')
  })

  it('draws a status dot on request', () => {
    const plain = mount(UiBadge)
    const dotted = mount(UiBadge, { props: { dot: true, tone: 'danger' } })

    // The one child of a plain badge is its label; a dotted badge draws the dot before it.
    expect(plain.element.children).toHaveLength(1)
    expect(dotted.element.children).toHaveLength(2)
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

  it('sets the full size as the concept pill: 12px, semibold, 24px high, 10px padding', () => {
    expect(mount(UiBadge).classes()).toEqual(expect.arrayContaining(['text-xs', 'font-semibold', 'px-2.5', 'h-6']))
  })
})
