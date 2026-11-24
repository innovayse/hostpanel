/**
 * Unit tests for UiFormSection: a schema section drawn as a card of fields, and its loading skeleton.
 */
import { describe, expect, it } from 'vitest'
import UiFormSection from '@/components/ui/UiFormSection.vue'
import { mountWithApp } from '../../helpers/mount'
import { SkeletonFieldKind, SkeletonSpan } from '@/utils/ui/skeletonSchema'
import type { SkeletonSection } from '@/utils/ui/skeletonSchema'

/** A section with one field of each kind, one full row and one required. */
const section: SkeletonSection = {
  key: 'main',
  title: 'shared.clients',
  fields: [
    { key: 'name', label: 'shared.name', required: true },
    { key: 'email', label: 'shared.email' },
    { key: 'notes', label: 'shared.notes', kind: SkeletonFieldKind.Textarea, span: SkeletonSpan.Full },
    { key: 'active', label: 'shared.status', kind: SkeletonFieldKind.Toggle },
  ],
}

/**
 * Mounts the section with the real catalogue.
 *
 * @param props - Props of the section.
 * @param slots - Slots of the section, as markup.
 * @returns The mounted wrapper.
 */
const mountSection = (props: Record<string, unknown>, slots: Record<string, string> = {}) =>
  mountWithApp(UiFormSection, { props, slots })

describe('UiFormSection', () => {
  it('draws the title and the captions from the locale keys, with an asterisk on a required field', () => {
    const wrapper = mountSection({ section })

    expect(wrapper.get('h2').text()).toBe('Clients')
    expect(wrapper.get('[data-testid="ui-form-field-name"]').text()).toContain('Name')
    expect(wrapper.get('[data-testid="ui-form-field-name"]').text()).toContain('*')
    expect(wrapper.get('[data-testid="ui-form-field-email"]').text()).not.toContain('*')
  })

  it('draws no heading for a section without a title', () => {
    const wrapper = mountSection({ section: { key: 'bare', fields: [{ key: 'a', label: 'shared.name' }] } })

    expect(wrapper.find('h2').exists()).toBe(false)
  })

  it('lays the fields out in two columns unless the section says one', () => {
    const two = mountSection({ section })
    const one = mountSection({ section: { ...section, columns: 1 } })

    expect(two.get('.grid').classes()).toContain('@lg:grid-cols-2')
    expect(one.get('.grid').classes()).toContain('grid-cols-1')
    expect(one.get('.grid').classes()).not.toContain('@lg:grid-cols-2')
  })

  it('makes a full span field take the whole row', () => {
    const wrapper = mountSection({ section })

    expect(wrapper.get('[data-testid="ui-form-field-notes"]').classes()).toContain('col-span-full')
    expect(wrapper.get('[data-testid="ui-form-field-name"]').classes()).not.toContain('col-span-full')
  })

  it('hands the control slot the id its caption points at', () => {
    const wrapper = mountSection({ section }, {
      'field-name': '<template #field-name="{ id, invalid }"><input :id="id" :data-invalid="invalid" placeholder="Name" /></template>',
    })

    const input = wrapper.get('input')
    expect(wrapper.get('label').attributes('for')).toBe(input.attributes('id'))
    expect(input.attributes('data-invalid')).toBe('false')
  })

  it('shows the server sentence and the hint of a field by its key', () => {
    const wrapper = mountSection({ section, errors: { name: 'Name is taken' }, hints: { email: 'Work address' } })

    expect(wrapper.get('[role="alert"]').text()).toBe('Name is taken')
    expect(wrapper.get('[data-testid="ui-form-field-name-error"]').text()).toBe('Name is taken')
    expect(wrapper.get('[data-testid="ui-form-field-email"]').text()).toContain('Work address')
  })

  it('draws the section slot under the fields', () => {
    const wrapper = mountSection({ section }, { 'section-main': '<p data-testid="note">Note</p>' })

    expect(wrapper.find('[data-testid="note"]').exists()).toBe(true)
  })

  describe('while loading', () => {
    it('keeps the title and the captions and draws a skeleton in place of every control', () => {
      const wrapper = mountSection({ section, loading: true }, { 'field-name': '<input data-testid="real" />' })

      expect(wrapper.get('h2').text()).toBe('Clients')
      expect(wrapper.get('[data-testid="ui-form-field-name"]').text()).toContain('Name')
      expect(wrapper.find('[data-testid="real"]').exists()).toBe(false)
      expect(wrapper.findAll('[data-testid="ui-field-skeleton"]')).toHaveLength(2)
      expect(wrapper.findAll('[data-testid="ui-form-field-bar"]')).toHaveLength(2)
      expect(wrapper.attributes('aria-busy')).toBe('true')
    })

    it('gives a textarea and a toggle the bars of their own heights', () => {
      const wrapper = mountSection({ section, loading: true })

      expect(wrapper.get('[data-testid="ui-form-field-notes"] [data-testid="ui-form-field-bar"]').classes()).toContain('h-24')
      expect(wrapper.get('[data-testid="ui-form-field-active"] [data-testid="ui-form-field-bar"]').classes()).toContain('h-6')
    })

    it('does not draw the section slot', () => {
      const wrapper = mountSection({ section, loading: true }, { 'section-main': '<p data-testid="note">Note</p>' })

      expect(wrapper.find('[data-testid="note"]').exists()).toBe(false)
    })
  })

  describe('a toggle and a radio group', () => {
    it('keeps a toggle on one row, the caption left and the switch right, in the skeleton', () => {
      const wrapper = mountSection({ section, loading: true })
      const field = wrapper.get('[data-testid="ui-form-field-active"]')

      expect(field.get('label').element.parentElement?.classList.contains('justify-between')).toBe(true)
      expect(field.get('[data-testid="ui-form-field-bar"]').classes()).toContain('rounded-full')
    })

    it('draws a radio group as a dot and the real caption per option while loading, and the slot when loaded', () => {
      const radios: SkeletonSection = {
        key: 'owner',
        fields: [{ key: 'owner', label: 'shared.client', kind: SkeletonFieldKind.Radio, options: ['shared.name', 'shared.email'] }],
      }
      const loading = mountSection({ section: radios, loading: true })
      const loaded = mountSection({ section: radios }, { 'field-owner': '<p data-testid="real">Real</p>' })

      expect(loading.get('[data-testid="ui-form-radio-options"]').text()).toContain('Name')
      expect(loading.get('[data-testid="ui-form-radio-options"]').text()).toContain('Email')
      expect(loading.find('[data-testid="ui-field-skeleton"]').exists()).toBe(false)
      expect(loaded.find('[data-testid="ui-form-radio-options"]').exists()).toBe(false)
      expect(loaded.find('[data-testid="real"]').exists()).toBe(true)
    })
  })
})
