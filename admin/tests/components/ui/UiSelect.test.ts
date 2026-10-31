/**
 * Unit tests for UiSelect: trigger label, opening, picking, searching and closing, and the compact look for toolbars.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiSelect from '@/components/ui/UiSelect.vue'
import { createTestI18n } from '../../helpers/i18n'

/** Options used by the tests. */
const options = [
  { value: 'a', label: 'Alpha' },
  { value: 'b', label: 'Beta' },
]

/**
 * Mounts the select with the English catalogue.
 *
 * @param props - Props for the select.
 * @returns The wrapper.
 */
const mountSelect = (props: { modelValue: string; placeholder?: string; disabled?: boolean; searchable?: boolean; compact?: boolean }) =>
  mount(UiSelect, { props: { options, ...props }, global: { plugins: [createTestI18n()] } })

describe('UiSelect', () => {
  it('shows the label of the selected option', () => {
    const wrapper = mountSelect({ modelValue: 'b' })

    expect(wrapper.get('button').text()).toBe('Beta')
  })

  it('shows the placeholder in the muted colour while nothing matches', () => {
    const wrapper = mountSelect({ modelValue: '', placeholder: 'Pick one' })

    expect(wrapper.get('button').text()).toBe('Pick one')
    expect(wrapper.get('button span').classes()).toContain('text-text-muted')
  })

  it('draws a chosen label in the primary colour even when it equals the placeholder', () => {
    const wrapper = mountSelect({ modelValue: 'a', placeholder: 'Alpha' })

    expect(wrapper.get('button span').classes()).toContain('text-text-primary')
    expect(wrapper.get('button span').classes()).not.toContain('text-text-muted')
  })

  it('opens the list on click and marks the selected option', async () => {
    const wrapper = mountSelect({ modelValue: 'a' })

    await wrapper.get('button').trigger('click')
    const items = wrapper.findAll('[role="option"]')

    expect(wrapper.find('[role="listbox"]').exists()).toBe(true)
    expect(items).toHaveLength(2)
    expect(items[0]?.attributes('aria-selected')).toBe('true')
    expect(items[1]?.attributes('aria-selected')).toBe('false')
  })

  it('paints the selected option solid primary with white text and a focus ring on every option', async () => {
    const wrapper = mountSelect({ modelValue: 'a' })

    await wrapper.get('button').trigger('click')
    const items = wrapper.findAll('[role="option"]')

    expect(items[0]?.classes()).toEqual(expect.arrayContaining(['bg-primary-600', 'text-white']))
    expect(items[1]?.classes()).not.toContain('bg-primary-600')
    expect(items[1]?.classes()).toContain('focus-visible:ring-2')
  })

  it('emits the picked value and closes', async () => {
    const wrapper = mountSelect({ modelValue: 'a' })

    await wrapper.get('button').trigger('click')
    await wrapper.findAll('[role="option"]')[1]?.trigger('mousedown')

    expect(wrapper.emitted('update:modelValue')?.[0]).toEqual(['b'])
    expect(wrapper.find('[role="listbox"]').exists()).toBe(false)
  })

  it('filters by the search term and reports no results', async () => {
    const wrapper = mountSelect({ modelValue: 'a', searchable: true })

    await wrapper.get('button').trigger('click')
    const search = wrapper.get('input')
    await search.setValue('bet')

    expect(wrapper.findAll('[role="option"]')).toHaveLength(1)

    await search.setValue('zzz')

    expect(wrapper.findAll('[role="option"]')).toHaveLength(0)
    expect(wrapper.text()).toContain('No results')
  })

  it('closes when the pointer goes down outside', async () => {
    const wrapper = mountSelect({ modelValue: 'a' })

    await wrapper.get('button').trigger('click')
    document.body.dispatchEvent(new MouseEvent('mousedown', { bubbles: true }))
    await wrapper.vm.$nextTick()

    expect(wrapper.find('[role="listbox"]').exists()).toBe(false)
  })

  it('does not open while disabled', async () => {
    const wrapper = mountSelect({ modelValue: 'a', disabled: true })

    await wrapper.get('button').trigger('click')

    expect(wrapper.find('[role="listbox"]').exists()).toBe(false)
  })

  it('widens the list on request and keeps data-testid on the root', async () => {
    const wrapper = mount(UiSelect, {
      props: { modelValue: 'a', options, wide: true },
      attrs: { 'data-testid': 'probe' },
      global: { plugins: [createTestI18n()] },
    })

    await wrapper.get('button').trigger('click')

    expect(wrapper.attributes('data-testid')).toBe('probe')
    expect(wrapper.get('[role="listbox"]').classes()).toContain('min-w-[18rem]')
  })

  it('keeps the field look and the solid selection when it is not compact', async () => {
    const wrapper = mountSelect({ modelValue: 'a' })

    expect(wrapper.get('button').classes()).toEqual(expect.arrayContaining(['px-3', 'py-2', 'rounded-[10px]']))
    await wrapper.get('button').trigger('click')
    expect(wrapper.get('[role="listbox"]').classes()).not.toContain('rounded-xl')
  })

  it('draws a small trigger like a small button when compact', () => {
    const wrapper = mountSelect({ modelValue: 'a', compact: true })

    expect(wrapper.get('button').classes()).toEqual(expect.arrayContaining(['py-1', 'text-[0.75rem]', 'rounded-lg']))
    expect(wrapper.get('button').classes()).not.toContain('py-2')
  })

  it('opens a soft list when compact: inset rounded rows and a tinted, not solid, selection', async () => {
    const wrapper = mountSelect({ modelValue: 'a', compact: true })

    await wrapper.get('button').trigger('click')
    const items = wrapper.findAll('[role="option"]')

    expect(wrapper.get('[role="listbox"]').classes()).toEqual(expect.arrayContaining(['rounded-xl', 'p-1']))
    expect(items[0]?.classes()).toEqual(expect.arrayContaining(['rounded-lg', 'bg-primary-500/10']))
    expect(items[0]?.classes()).not.toContain('bg-primary-600')
    expect(items[0]?.classes()).not.toContain('text-white')
    expect(items[1]?.classes()).toContain('hover:bg-surface-hover')
  })

  it('still picks and closes when compact', async () => {
    const wrapper = mountSelect({ modelValue: 'a', compact: true })

    await wrapper.get('button').trigger('click')
    await wrapper.findAll('[role="option"]')[1]?.trigger('mousedown')

    expect(wrapper.emitted('update:modelValue')).toEqual([['b']])
    expect(wrapper.find('[role="listbox"]').exists()).toBe(false)
  })
})
