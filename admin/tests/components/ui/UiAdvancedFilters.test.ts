/**
 * Unit tests for UiAdvancedFilters: row management and the emitted complete rows.
 */
import { describe, expect, it } from 'vitest'
import { nextTick } from 'vue'
import { mount } from '@vue/test-utils'
import UiAdvancedFilters from '@/components/ui/UiAdvancedFilters.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import { createTestI18n } from '../../helpers/i18n'

/** Fields offered to the filters. */
const fields = [
  { value: 'name', label: 'Name' },
  { value: 'email', label: 'Email' },
]

/**
 * Mounts the filters with the English catalogue.
 *
 * @param max - Row limit.
 * @param defaultField - Field the first row starts on.
 * @returns The wrapper.
 */
const mountFilters = (max?: number, defaultField?: string) =>
  mount(UiAdvancedFilters, { props: { fields, max, defaultField }, global: { plugins: [createTestI18n()] } })

describe('UiAdvancedFilters', () => {
  it('starts with one row and no remove button', () => {
    const wrapper = mountFilters()

    expect(wrapper.findAll('[data-testid^="advanced-filters-value-"]')).toHaveLength(1)
    expect(wrapper.find('[data-testid="advanced-filters-remove-0"]').exists()).toBe(false)
    expect(wrapper.text()).toContain('Filter 1')
  })

  it('adds rows up to the limit', async () => {
    const wrapper = mountFilters(2)

    await wrapper.get('[data-testid="advanced-filters-add"]').trigger('click')

    expect(wrapper.findAll('[data-testid^="advanced-filters-value-"]')).toHaveLength(2)
    expect(wrapper.find('[data-testid="advanced-filters-add"]').exists()).toBe(false)
  })

  it('removes a row but always keeps one', async () => {
    const wrapper = mountFilters()
    await wrapper.get('[data-testid="advanced-filters-add"]').trigger('click')

    await wrapper.get('[data-testid="advanced-filters-remove-0"]').trigger('click')

    expect(wrapper.findAll('[data-testid^="advanced-filters-value-"]')).toHaveLength(1)
    expect(wrapper.find('[data-testid="advanced-filters-remove-0"]').exists()).toBe(false)
  })

  it('emits only complete rows', async () => {
    const wrapper = mountFilters()

    await wrapper.get('[data-testid="advanced-filters-value-0"]').setValue('acme')
    await nextTick()
    const afterValueOnly = wrapper.emitted('update:filters')

    expect(afterValueOnly?.[afterValueOnly.length - 1]).toEqual([[]])

    wrapper.findAllComponents(UiSelect)[0]?.vm.$emit('update:modelValue', 'name')
    await nextTick()
    await nextTick()
    const emitted = wrapper.emitted('update:filters')

    expect(emitted?.[emitted.length - 1]).toEqual([[{ field: 'name', condition: 'exact', value: 'acme' }]])
  })

  it('starts the first row on the default field instead of None', () => {
    const wrapper = mountFilters(undefined, 'email')

    expect((wrapper.getComponent(UiSelect).props() as { modelValue: string }).modelValue).toBe('email')
  })

  it('starts on None when no default field is given', () => {
    expect((mountFilters().getComponent(UiSelect).props() as { modelValue: string }).modelValue).toBe('')
  })
})

describe('UiAdvancedFilters with filters already in force', () => {
  it('starts its rows from them, so a screen that mounts it again still shows what is applied', () => {
    const wrapper = mount(UiAdvancedFilters, {
      props: {
        fields,
        initial: [
          { field: 'name', condition: 'exact', value: 'Ada' },
          { field: 'email', condition: 'contains', value: 'example' },
        ],
      },
      global: { plugins: [createTestI18n()] },
    })

    expect(wrapper.findAll('[data-testid^="advanced-filters-value-"]').map((row) => (row.element as HTMLInputElement).value)).toEqual(['Ada', 'example'])
  })

  it('falls back to one blank row when none is in force', () => {
    const wrapper = mount(UiAdvancedFilters, { props: { fields, initial: [] }, global: { plugins: [createTestI18n()] } })

    expect(wrapper.findAll('[data-testid^="advanced-filters-value-"]')).toHaveLength(1)
  })

  it('lays its controls out by its own width, not the screen\'s, so a side panel stacks them', () => {
    const wrapper = mountFilters()

    expect(wrapper.classes()).toContain('@container')
    expect(wrapper.get('[data-testid="advanced-filters-value-0"]').element.closest('.grid')?.className).toContain('@lg:grid-cols-3')
    expect(wrapper.get('[data-testid="advanced-filters-value-0"]').element.closest('.grid')?.className).not.toContain('sm:grid-cols-3')
  })
})
