/**
 * Tests for BlockListAddForm: the value field is typed for an address, a range and a domain and
 * chosen from the country listbox for a country, the Add button waits for a value, a submit is
 * reported once, and the API's field messages show under their fields.
 */
import { describe, expect, it } from 'vitest'
import type { VueWrapper } from '@vue/test-utils'
import BlockListAddForm from '@/components/fraud/BlockListAddForm.vue'
import { BlockListType } from '@/types/fraud'
import { mountWithApp } from '../../helpers/mount'
import { pickOption, selectText } from '../../helpers/select'

/** Test id of the value field. */
const VALUE = '[data-testid="fraud-block-list-value"]'

/**
 * Mounts the form with sensible defaults.
 *
 * @param props - Props that differ from the defaults.
 * @returns The mounted form.
 */
const mountForm = (props: Record<string, unknown> = {}): VueWrapper =>
  mountWithApp(BlockListAddForm, {
    props: { type: BlockListType.Ip, adding: false, errors: {}, value: '', note: '', ...props },
  })

describe('BlockListAddForm', () => {
  it('types the value of an address list, named and hinted for what the list holds', () => {
    const wrapper = mountForm()

    expect(wrapper.text()).toContain('IP address')
    expect(wrapper.get(VALUE).attributes('placeholder')).toBe('203.0.113.7')
  })

  it('hints an IP range, an email domain and a country in their own words', async () => {
    const wrapper = mountForm({ type: BlockListType.Cidr })
    expect(wrapper.get(VALUE).attributes('placeholder')).toBe('203.0.113.0/24')

    await wrapper.setProps({ type: BlockListType.EmailDomain })
    expect(wrapper.get(VALUE).attributes('placeholder')).toBe('mail.example')
  })

  it('chooses a country from the listbox and reports the code', async () => {
    const wrapper = mountForm({ type: BlockListType.Country })

    expect(wrapper.find(`input${VALUE}`).exists()).toBe(false)
    expect(selectText(wrapper, 'fraud-block-list-value')).toBe('Select country')

    await pickOption(wrapper, 'fraud-block-list-value', 'France')

    expect(wrapper.emitted('update:value')).toEqual([['FR']])
  })

  it('reports the typed value and the typed note', async () => {
    const wrapper = mountForm()

    await wrapper.get(VALUE).setValue('203.0.113.7')
    await wrapper.get('[data-testid="fraud-block-list-note"]').setValue('spam')

    expect(wrapper.emitted('update:value')).toEqual([['203.0.113.7']])
    expect(wrapper.emitted('update:note')).toEqual([['spam']])
  })

  it('limits the note to the length the API accepts', () => {
    expect(mountForm().get('[data-testid="fraud-block-list-note"]').attributes('maxlength')).toBe('500')
  })

  it('keeps Add blocked until there is a value, and sends nothing for a blank one', async () => {
    const wrapper = mountForm({ value: '   ' })

    expect(wrapper.get('[data-testid="fraud-block-list-add"]').attributes('disabled')).toBeDefined()
    await wrapper.get('form').trigger('submit')
    expect(wrapper.emitted('add')).toBeUndefined()
  })

  it('reports an add when the form is submitted with a value', async () => {
    const wrapper = mountForm({ value: '203.0.113.7' })

    expect(wrapper.get('[data-testid="fraud-block-list-add"]').attributes('disabled')).toBeUndefined()
    await wrapper.get('form').trigger('submit')

    expect(wrapper.emitted('add')).toHaveLength(1)
  })

  it('blocks Add while the request is in flight', () => {
    const wrapper = mountForm({ value: '203.0.113.7', adding: true })

    expect(wrapper.get('[data-testid="fraud-block-list-add"]').attributes('disabled')).toBeDefined()
  })

  it('shows the messages the API refused the fields with', () => {
    const wrapper = mountForm({ value: 'nope', errors: { value: 'Enter a valid IP address.', note: 'The note is too long.' } })

    expect(wrapper.get('[data-testid="fraud-block-list-value-error"]').text()).toBe('Enter a valid IP address.')
    expect(wrapper.get('[data-testid="fraud-block-list-note-error"]').text()).toBe('The note is too long.')
  })
})
