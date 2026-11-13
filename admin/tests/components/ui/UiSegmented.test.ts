/**
 * Unit tests for UiSegmented: radio group semantics, selection, arrow-key navigation and the concept's look.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiSegmented from '@/components/ui/UiSegmented.vue'

/** Three options, the middle one disabled. */
const items = [
  { value: '30', label: '30 days', testid: 'opt-30' },
  { value: '60', label: '60 days', disabled: true, testid: 'opt-60' },
  { value: '90', label: '90 days', testid: 'opt-90' },
]

describe('UiSegmented', () => {
  it('renders a radio group with the selected option checked and in the tab order', () => {
    const wrapper = mount(UiSegmented, { props: { items, modelValue: '30', ariaLabel: 'Period' } })
    const options = wrapper.findAll('[role="radio"]')

    expect(wrapper.get('[role="radiogroup"]').attributes('aria-label')).toBe('Period')
    expect(options.map(o => o.text())).toEqual(['30 days', '60 days', '90 days'])
    expect(options[0]?.attributes('aria-checked')).toBe('true')
    expect(options[0]?.attributes('tabindex')).toBe('0')
    expect(options[2]?.attributes('aria-checked')).toBe('false')
    expect(options[2]?.attributes('tabindex')).toBe('-1')
  })

  it('raises the selected option on the active surface and mutes the others', () => {
    const options = mount(UiSegmented, { props: { items, modelValue: '30' } }).findAll('[role="radio"]')

    expect(options[0]?.classes()).toEqual(expect.arrayContaining(['bg-surface-active', 'text-text-primary']))
    expect(options[2]?.classes()).toContain('text-text-muted')
  })

  it('sizes the frame and the buttons as the concept toggle', () => {
    const wrapper = mount(UiSegmented, { props: { items, modelValue: '30' } })

    expect(wrapper.classes()).toEqual(expect.arrayContaining(['p-1', 'gap-1.5', 'rounded-control', 'bg-surface-field']))
    expect(wrapper.get('[role="radio"]').classes()).toEqual(expect.arrayContaining(['h-8', 'px-3.5', 'rounded-[9px]']))
  })

  it('emits the value of a clicked option', async () => {
    const wrapper = mount(UiSegmented, { props: { items, modelValue: '30' } })

    await wrapper.get('[data-testid="opt-90"]').trigger('click')

    expect(wrapper.emitted('update:modelValue')).toEqual([['90']])
  })

  it('moves to the next enabled option with the arrow keys, skipping the disabled one and wrapping', async () => {
    const wrapper = mount(UiSegmented, { props: { items, modelValue: '30' } })

    await wrapper.get('[data-testid="opt-30"]').trigger('keydown', { key: 'ArrowRight' })
    await wrapper.get('[data-testid="opt-90"]').trigger('keydown', { key: 'ArrowRight' })
    await wrapper.get('[data-testid="opt-30"]').trigger('keydown', { key: 'ArrowLeft' })

    expect(wrapper.emitted('update:modelValue')).toEqual([['90'], ['30'], ['90']])
  })

  it('ignores other keys', async () => {
    const wrapper = mount(UiSegmented, { props: { items, modelValue: '30' } })

    await wrapper.get('[data-testid="opt-30"]').trigger('keydown', { key: 'a' })

    expect(wrapper.emitted('update:modelValue')).toBeUndefined()
  })
})
