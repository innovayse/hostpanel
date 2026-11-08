/**
 * Unit tests for UiSegmentedControl: radio-group semantics, v-model, keyboard navigation and the
 * pill look of the chosen and the available segments.
 */
import { describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import UiSegmentedControl from '~/components/ui/UiSegmentedControl.vue'
import type { SegmentedOption } from '~/types/segmentedoption'

/** Three segments to exercise wrapping and Home / End. */
const OPTIONS: SegmentedOption<string>[] = [
  { value: 'monthly', label: 'Monthly', testid: 'seg-monthly' },
  { value: 'quarterly', label: 'Quarterly', testid: 'seg-quarterly' },
  { value: 'annual', label: 'Annual', testid: 'seg-annual' },
]

/**
 * Mounts the control attached to the document so focus can be asserted.
 *
 * @param modelValue - The chosen value.
 * @returns The wrapper.
 */
const mountControl = (modelValue: string) =>
  mountWithKit(UiSegmentedControl, {
    props: { options: OPTIONS, modelValue, label: 'Billing period', testid: 'seg' },
    attachToBody: true,
  })

describe('UiSegmentedControl', () => {
  it('renders a labelled radio group with one radio per option', () => {
    const wrapper = mountControl('quarterly')
    const group = wrapper.find('[data-testid="seg"]')

    expect(group.attributes('role')).toBe('radiogroup')
    expect(group.attributes('aria-label')).toBe('Billing period')
    expect(wrapper.findAll('[role="radio"]').map((radio) => radio.text())).toEqual(['Monthly', 'Quarterly', 'Annual'])
    expect(wrapper.find('[data-testid="seg-quarterly"]').attributes('aria-checked')).toBe('true')
    expect(wrapper.find('[data-testid="seg-monthly"]').attributes('aria-checked')).toBe('false')
    wrapper.unmount()
  })

  it('makes only the chosen segment a tab stop', () => {
    const wrapper = mountControl('annual')

    expect(wrapper.find('[data-testid="seg-annual"]').attributes('tabindex')).toBe('0')
    expect(wrapper.find('[data-testid="seg-monthly"]').attributes('tabindex')).toBe('-1')
    wrapper.unmount()
  })

  it('emits the clicked value and stays quiet for the current one', async () => {
    const wrapper = mountControl('monthly')

    await wrapper.find('[data-testid="seg-annual"]').trigger('click')
    await wrapper.find('[data-testid="seg-monthly"]').trigger('click')

    expect(wrapper.emitted('update:modelValue')).toEqual([['annual']])
    wrapper.unmount()
  })

  it('moves the choice and focus with the arrow keys, wrapping at both ends', async () => {
    const wrapper = mountControl('annual')

    await wrapper.find('[data-testid="seg-annual"]').trigger('keydown', { key: 'ArrowRight' })
    expect(document.activeElement).toBe(wrapper.find('[data-testid="seg-monthly"]').element)
    await wrapper.find('[data-testid="seg-annual"]').trigger('keydown', { key: 'ArrowLeft' })
    await wrapper.find('[data-testid="seg-annual"]').trigger('keydown', { key: 'ArrowDown' })

    expect(wrapper.emitted('update:modelValue')).toEqual([['monthly'], ['quarterly'], ['monthly']])
    wrapper.unmount()
  })

  it('jumps to the first and last segment with Home and End and ignores other keys', async () => {
    const wrapper = mountControl('quarterly')
    const current = wrapper.find('[data-testid="seg-quarterly"]')

    await current.trigger('keydown', { key: 'End' })
    await current.trigger('keydown', { key: 'Home' })
    await current.trigger('keydown', { key: 'Enter' })

    expect(wrapper.emitted('update:modelValue')).toEqual([['annual'], ['monthly']])
    wrapper.unmount()
  })

  it('paints the chosen segment as a borderless pill with the brand gradient and its contrast text', () => {
    const wrapper = mountControl('monthly')
    const active = wrapper.find('[data-testid="seg-monthly"]').classes()

    expect(active).toEqual(expect.arrayContaining(['rounded-full', 'bg-brand', 'text-on-brand']))
    expect(active).not.toContain('rounded-lg')
    expect(active.some((c) => c === 'border' || c.startsWith('border-'))).toBe(false)
    expect(active.some((c) => c.startsWith('text-') && c !== 'text-on-brand' && c !== 'text-center' && c !== 'text-sm')).toBe(false)
    wrapper.unmount()
  })

  it('keeps the available segments readable in both themes with a hover state and no border', () => {
    const wrapper = mountControl('monthly')
    const idle = wrapper.find('[data-testid="seg-annual"]').classes()

    expect(idle).toEqual(expect.arrayContaining(['rounded-full', 'text-gray-600', 'dark:text-gray-300', 'hover:text-gray-900']))
    expect(idle).not.toContain('bg-brand')
    expect(idle.some((c) => c === 'border' || c.startsWith('border-'))).toBe(false)
    wrapper.unmount()
  })

  it('draws the focus ring inside the pill and only for keyboard focus', () => {
    const wrapper = mountControl('monthly')
    const segment = wrapper.find('[data-testid="seg-monthly"]').classes()

    expect(segment).toEqual(expect.arrayContaining(['focus-visible:ring-2', 'focus-visible:ring-inset', 'focus-visible:ring-offset-0']))
    expect(segment.some((c) => c.startsWith('focus:ring'))).toBe(false)
    wrapper.unmount()
  })

  it('scrolls sideways inside its own track instead of overflowing when there are many segments', () => {
    const wrapper = mountControl('monthly')

    expect(wrapper.find('[data-testid="seg"]').classes()).toEqual(expect.arrayContaining(['max-w-full', 'overflow-x-auto']))
    wrapper.unmount()
  })

  it('puts the track on the same pill radius with equal-width segments', () => {
    const wrapper = mountControl('monthly')

    expect(wrapper.find('[data-testid="seg"]').classes()).toEqual(expect.arrayContaining(['rounded-full', 'inline-grid', 'auto-cols-fr']))
    wrapper.unmount()
  })

  it('steps the segment padding with the size', () => {
    const small = mountWithKit(UiSegmentedControl, {
      props: { options: OPTIONS, modelValue: 'monthly', label: 'Billing period', size: 'sm' },
    })

    expect(small.find('[data-testid="seg-monthly"]').classes()).toContain('text-xs')
  })
})
