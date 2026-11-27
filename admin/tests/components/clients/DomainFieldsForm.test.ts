/**
 * Mount tests for DomainFieldsForm: each declared kind is drawn with the kit control for it, texts come as the plugin worded
 * them, edits are reported with the whole set of values and a refusal shows beside its field. Every key is invented.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import DomainFieldsForm from '@/components/clients/DomainFieldsForm.vue'
import { type DomainField, DomainFieldType } from '@/types/domain'
import { createTestI18n } from '../../helpers/i18n'

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

/**
 * Mounts the form.
 *
 * @param props - The props of the form.
 * @returns The wrapper.
 */
const mountForm = (props: { fields: DomainField[]; modelValue: Record<string, string>; errors?: Record<string, string> }) =>
  mount(DomainFieldsForm, { props, global: { plugins: [createTestI18n()] } })

describe('DomainFieldsForm', () => {
  it('draws nothing when the registrar asks for nothing', () => {
    expect(mountForm({ fields: [], modelValue: {} }).find('[data-testid="clients-domain-fields"]').exists()).toBe(false)
  })

  it('draws one control per field by its kind, with the label the plugin gave', () => {
    const wrapper = mountForm({
      modelValue: {},
      fields: [
        field('text-key', DomainFieldType.Text),
        field('number-key', DomainFieldType.Number),
        field('url-key', DomainFieldType.Url),
        field('area-key', DomainFieldType.Textarea),
        field('select-key', DomainFieldType.Select, { options: [{ value: 'a', label: 'Option A' }] }),
        field('date-key', DomainFieldType.Date),
      ],
    })

    expect(wrapper.find('input[data-testid="clients-domain-fields-text-key"]').attributes('type')).toBe('text')
    expect(wrapper.find('input[data-testid="clients-domain-fields-number-key"]').attributes('type')).toBe('number')
    expect(wrapper.find('input[data-testid="clients-domain-fields-url-key"]').attributes('type')).toBe('url')
    expect(wrapper.find('textarea[data-testid="clients-domain-fields-area-key"]').exists()).toBe(true)
    expect(wrapper.find('select').exists()).toBe(false)
    expect(wrapper.text()).toContain('Label of text-key')
  })

  it('shows the values it is given and the plugin help text', () => {
    const wrapper = mountForm({
      modelValue: { 'text-key': 'N-1' },
      fields: [field('text-key', DomainFieldType.Text, { description: 'As on the certificate' })],
    })

    expect((wrapper.get('[data-testid="clients-domain-fields-text-key"]').element as HTMLInputElement).value).toBe('N-1')
    expect(wrapper.text()).toContain('As on the certificate')
  })

  it('reports an edit with the whole set of values', async () => {
    const wrapper = mountForm({ modelValue: { other: 'kept' }, fields: [field('text-key', DomainFieldType.Text)] })

    await wrapper.get('[data-testid="clients-domain-fields-text-key"]').setValue('N-2')

    expect(wrapper.emitted('update:modelValue')?.at(-1)).toEqual([{ other: 'kept', 'text-key': 'N-2' }])
  })

  it('shows the refusal of a field next to it', () => {
    const wrapper = mountForm({
      modelValue: {},
      fields: [field('text-key', DomainFieldType.Text)],
      errors: { 'text-key': 'The registry refused it.' },
    })

    expect(wrapper.text()).toContain('The registry refused it.')
  })
})
