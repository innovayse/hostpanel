/**
 * Unit tests for UiCountrySelect: country list over UiSelect, model as ISO code.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiCountrySelect from '@/components/ui/UiCountrySelect.vue'
import { createTestI18n } from '../../helpers/i18n'

/**
 * Mounts the picker with the English catalogue.
 *
 * @param props - Props for the picker.
 * @returns The wrapper.
 */
const mountCountry = (props: { modelValue: string; placeholder?: string }) =>
  mount(UiCountrySelect, { props, global: { plugins: [createTestI18n()] } })

describe('UiCountrySelect', () => {
  it('shows the default placeholder while nothing is chosen', () => {
    expect(mountCountry({ modelValue: '' }).get('button').text()).toBe('Select country')
  })

  it('prefers the caller placeholder', () => {
    expect(mountCountry({ modelValue: '', placeholder: 'Where?' }).get('button').text()).toBe('Where?')
  })

  it('shows the name of the chosen country', () => {
    expect(mountCountry({ modelValue: 'US' }).get('button').text()).toBe('United States')
  })

  it('emits the ISO code of the picked country', async () => {
    const wrapper = mountCountry({ modelValue: '' })

    await wrapper.get('button').trigger('click')
    const armenia = wrapper.findAll('[role="option"]').find(o => o.text() === 'Armenia')
    await armenia?.trigger('mousedown')

    expect(wrapper.emitted('update:modelValue')?.[0]).toEqual(['AM'])
  })

  it('filters the long list by what is typed', async () => {
    const wrapper = mountCountry({ modelValue: '' })

    await wrapper.get('button').trigger('click')
    await wrapper.get('input').setValue('armen')

    expect(wrapper.findAll('[role="option"]').map(o => o.text())).toContain('Armenia')
    expect(wrapper.findAll('[role="option"]').length).toBeLessThan(5)
  })
})
