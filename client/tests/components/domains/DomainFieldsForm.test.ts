/**
 * Unit tests for DomainFieldsForm: each declared kind is drawn with the kit control for it, texts come as the plugin worded
 * them, edits are reported with the whole set of values and a refusal shows beside its field. Every key and zone is invented.
 *
 * @module tests/components/domains/DomainFieldsForm.test
 */

import { describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import DomainFieldsForm from '~/components/domains/DomainFieldsForm.vue'
import type { DomainField } from '~/types/domainfield'
import { DomainFieldType } from '~/types/domainfieldtype'

/**
 * Builds a field of one kind.
 *
 * @param key - The field key.
 * @param type - The kind.
 * @param overrides - What differs from a plain optional field.
 * @returns The field.
 */
const field = (key: string, type: DomainFieldType, overrides: Partial<DomainField> = {}): DomainField => ({
  key,
  label: 'Label of ' + key,
  type,
  required: false,
  description: null,
  placeholder: null,
  pattern: null,
  minLength: null,
  maxLength: null,
  options: [],
  ...overrides,
})

describe('DomainFieldsForm', () => {
  it('draws nothing when the registrar asks for nothing', () => {
    const wrapper = mountWithKit(DomainFieldsForm, { props: { fields: [], modelValue: {} } })

    expect(wrapper.find('[data-testid="domain-fields"]').exists()).toBe(false)
  })

  it('draws one control per field by its kind, with the label the plugin gave', () => {
    const wrapper = mountWithKit(DomainFieldsForm, {
      props: {
        modelValue: {},
        fields: [
          field('text-key', DomainFieldType.Text),
          field('number-key', DomainFieldType.Number),
          field('url-key', DomainFieldType.Url),
          field('area-key', DomainFieldType.Textarea),
          field('select-key', DomainFieldType.Select, { options: [{ value: 'a', label: 'Option A' }] }),
          field('date-key', DomainFieldType.Date),
        ],
      },
    })

    expect(wrapper.find('input[data-testid="domain-fields-text-key"]').attributes('type')).toBe('text')
    expect(wrapper.find('input[data-testid="domain-fields-number-key"]').attributes('type')).toBe('number')
    expect(wrapper.find('input[data-testid="domain-fields-url-key"]').attributes('type')).toBe('url')
    expect(wrapper.find('textarea[data-testid="domain-fields-area-key"]').exists()).toBe(true)
    expect(wrapper.find('button[data-testid="domain-fields-select-key"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="domain-fields-date-key-day"]').exists()).toBe(true)
    expect(wrapper.text()).toContain('Label of text-key')
    expect(wrapper.find('select').exists()).toBe(false)
  })

  it('shows the values it is given and the plugin\'s help text', () => {
    const wrapper = mountWithKit(DomainFieldsForm, {
      props: { modelValue: { 'text-key': 'N-1' }, fields: [field('text-key', DomainFieldType.Text, { description: 'As on the certificate' })] },
    })

    expect((wrapper.find('input[data-testid="domain-fields-text-key"]').element as HTMLInputElement).value).toBe('N-1')
    expect(wrapper.text()).toContain('As on the certificate')
  })

  it('reports an edit with the whole set of values', async () => {
    const wrapper = mountWithKit(DomainFieldsForm, {
      props: { modelValue: { other: 'kept' }, fields: [field('text-key', DomainFieldType.Text)] },
    })

    await wrapper.find('input[data-testid="domain-fields-text-key"]').setValue('N-2')

    expect(wrapper.emitted('update:modelValue')?.at(-1)).toEqual([{ other: 'kept', 'text-key': 'N-2' }])
  })

  it('reports a picked option', async () => {
    const wrapper = mountWithKit(DomainFieldsForm, {
      props: {
        modelValue: {},
        fields: [field('select-key', DomainFieldType.Select, { options: [{ value: 'a', label: 'Option A' }] })],
      },
    })

    await wrapper.find('button[data-testid="domain-fields-select-key"]').trigger('click')
    await wrapper.find('[data-testid="ui-select-option-a"]').trigger('click')

    expect(wrapper.emitted('update:modelValue')?.at(-1)).toEqual([{ 'select-key': 'a' }])
  })

  it('shows the refusal of a field next to it', () => {
    const wrapper = mountWithKit(DomainFieldsForm, {
      props: { modelValue: {}, fields: [field('text-key', DomainFieldType.Text)], errors: { 'text-key': 'The registry refused it.' } },
    })

    expect(wrapper.text()).toContain('The registry refused it.')
  })

  it('disables every control', () => {
    const wrapper = mountWithKit(DomainFieldsForm, {
      props: { modelValue: {}, fields: [field('text-key', DomainFieldType.Text)], disabled: true },
    })

    expect(wrapper.find('input[data-testid="domain-fields-text-key"]').attributes('disabled')).toBeDefined()
  })
})
