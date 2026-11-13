/**
 * Unit tests for UiPhoneInput: the country code picker is the kit's searchable select (trigger,
 * search by name, native name, ISO code and calling code, mouse and keyboard choice), plus the
 * typed number and the per-country hint. The component carries no listbox logic of its own.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiPhoneInput from '@/components/ui/UiPhoneInput.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import { createTestI18n } from '../../helpers/i18n'

/** Selector of the picker's trigger button. */
const TRIGGER = '[data-testid="ui-select-trigger"]'

/** Selector of the picker's search field. */
const SEARCH = '[data-testid="ui-select-search"]'

/** Selector of one row of the picker's list. */
const OPTION = '[data-testid="ui-select-option"]'

/**
 * Mounts the phone input attached to the document, with the English catalogue.
 *
 * @param props - Props for the field.
 * @returns The wrapper.
 */
const mountPhone = (props: Record<string, unknown> = {}) =>
  mount(UiPhoneInput, { props, attachTo: document.body, global: { plugins: [createTestI18n()] } })

describe('UiPhoneInput', () => {
  it('builds the code picker from the kit select, searchable', () => {
    const wrapper = mountPhone()
    const select = wrapper.findComponent(UiSelect)

    expect(select.exists()).toBe(true)
    expect((select.props() as { searchable: boolean }).searchable).toBe(true)
    expect(select.attributes('data-testid')).toBe('phone-country-select')
  })

  it('names the trigger and joins it to the number field', () => {
    const trigger = mountPhone().get(TRIGGER)

    expect(trigger.attributes('aria-label')).toBe('Country calling code')
    expect(trigger.classes()).toContain('rounded-l-control')
    expect(trigger.classes()).toContain('border-r-0')
  })

  it('defaults to the first listed country', () => {
    const wrapper = mountPhone()

    expect(wrapper.get(TRIGGER).text()).toContain('+1')
    expect(wrapper.get('[data-testid="phone-number-input"]').attributes('placeholder')).toBe('201-555-0123')
  })

  it('shows the code and sample number of the chosen country', () => {
    const wrapper = mountPhone({ countryCode: 'GB' })

    expect(wrapper.get(TRIGGER).text()).toContain('+44')
    expect(wrapper.get('[data-testid="phone-number-input"]').attributes('placeholder')).toBe('7700 900000')
  })

  it('falls back to a generic hint for a country without a sample number', () => {
    const wrapper = mountPhone({ countryCode: 'AF' })

    expect(wrapper.get('[data-testid="phone-number-input"]').attributes('placeholder')).toBe('Phone number')
  })

  it('shows the number it was given and emits typing', async () => {
    const wrapper = mountPhone({ phoneNumber: '123' })
    const input = wrapper.get('[data-testid="phone-number-input"]')

    expect((input.element as HTMLInputElement).value).toBe('123')

    await input.setValue('456')

    expect(wrapper.emitted('update:phoneNumber')?.[0]).toEqual(['456'])
  })

  it('opens with an empty search field that has a placeholder', async () => {
    const wrapper = mountPhone()

    await wrapper.get(TRIGGER).trigger('click')

    const search = wrapper.get(SEARCH)
    expect((search.element as HTMLInputElement).value).toBe('')
    expect(search.attributes('placeholder')).toBeTruthy()
  })

  it('offers every country with its name and calling code, and marks the chosen one', async () => {
    const wrapper = mountPhone({ countryCode: 'AM' })

    await wrapper.get(TRIGGER).trigger('click')

    const rows = wrapper.findAll(OPTION)
    const chosen = rows.filter((row) => row.attributes('aria-selected') === 'true')
    expect(chosen).toHaveLength(1)
    expect(chosen[0]?.text()).toContain('Armenia')
    expect(chosen[0]?.text()).toContain('+374')
    expect(chosen[0]?.find('svg').exists()).toBe(true)
    expect(rows.some((row) => row.text().includes('Afghanistan'))).toBe(true)
  })

  it('filters by name, native name, ISO code and calling code', async () => {
    const wrapper = mountPhone()
    await wrapper.get(TRIGGER).trigger('click')
    const search = wrapper.get(SEARCH)

    await search.setValue('afgh')
    expect(wrapper.findAll(OPTION).some((row) => row.text().includes('Afghanistan'))).toBe(true)
    expect(wrapper.findAll(OPTION).some((row) => row.text().includes('Armenia'))).toBe(false)

    await search.setValue('Հայաստան')
    expect(wrapper.findAll(OPTION).some((row) => row.text().includes('Armenia'))).toBe(true)

    await search.setValue('de')
    expect(wrapper.findAll(OPTION).some((row) => row.text().includes('Germany'))).toBe(true)

    await search.setValue('+44')
    expect(wrapper.findAll(OPTION).some((row) => row.text().includes('United Kingdom'))).toBe(true)
    expect(wrapper.findAll(OPTION).some((row) => row.text().includes('Armenia'))).toBe(false)
  })

  it('shows an empty state when nothing matches', async () => {
    const wrapper = mountPhone()
    await wrapper.get(TRIGGER).trigger('click')

    await wrapper.get(SEARCH).setValue('zzzzqq')

    expect(wrapper.get('[data-testid="ui-select-empty"]').text()).toBe('No results')
  })

  it('emits the picked country and closes the list', async () => {
    const wrapper = mountPhone()
    await wrapper.get(TRIGGER).trigger('click')

    const row = wrapper.findAll(OPTION).find((option) => option.text().includes('United Kingdom'))
    await row?.trigger('mousedown')

    expect(wrapper.emitted('update:countryCode')?.[0]).toEqual(['GB'])
    expect(wrapper.find('[data-testid="ui-select-panel"]').exists()).toBe(false)
  })

  it('moves the highlight with the arrow keys and chooses with Enter', async () => {
    const wrapper = mountPhone()
    await wrapper.get(TRIGGER).trigger('click')
    const search = wrapper.get(SEARCH)
    await search.setValue('united')
    const rows = wrapper.findAll(OPTION)

    await search.trigger('keydown', { key: 'ArrowDown' })
    expect(search.attributes('aria-activedescendant')).toBe(rows[1]?.attributes('id'))
    await search.trigger('keydown', { key: 'Enter' })

    expect(wrapper.emitted('update:countryCode')).toHaveLength(1)
    expect(wrapper.find('[data-testid="ui-select-panel"]').exists()).toBe(false)
  })

  it('closes on Escape and returns focus to the trigger', async () => {
    const wrapper = mountPhone()
    await wrapper.get(TRIGGER).trigger('click')

    await wrapper.get(SEARCH).trigger('keydown', { key: 'Escape' })

    expect(wrapper.find('[data-testid="ui-select-panel"]').exists()).toBe(false)
    expect(document.activeElement).toBe(wrapper.get(TRIGGER).element)
  })

  it('closes when the pointer goes down outside', async () => {
    const wrapper = mountPhone()
    await wrapper.get(TRIGGER).trigger('click')

    document.body.dispatchEvent(new MouseEvent('mousedown', { bubbles: true }))
    await wrapper.vm.$nextTick()

    expect(wrapper.find('[data-testid="ui-select-panel"]').exists()).toBe(false)
  })

  it('draws each flag as text from the country list, not as an image from a host', async () => {
    const wrapper = mountPhone()

    await wrapper.get(TRIGGER).trigger('click')

    expect(wrapper.html()).not.toContain('style=')
    expect(wrapper.html()).not.toContain('http')
  })
})
