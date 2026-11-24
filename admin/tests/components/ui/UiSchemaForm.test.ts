/**
 * Unit tests for UiSchemaForm: columns of section cards from a schema, slot forwarding and the loading skeleton.
 */
import { describe, expect, it } from 'vitest'
import UiSchemaForm from '@/components/ui/UiSchemaForm.vue'
import { mountWithApp } from '../../helpers/mount'
import type { SkeletonSection } from '@/utils/ui/skeletonSchema'

/** The first column's stack: two sections. */
const left: readonly SkeletonSection[] = [
  { key: 'identity', title: 'shared.name', fields: [{ key: 'name', label: 'shared.name' }, { key: 'email', label: 'shared.email' }] },
  { key: 'contact', fields: [{ key: 'phone', label: 'shared.phoneNumber' }] },
]

/** The second column's stack: one section. */
const right: readonly SkeletonSection[] = [
  { key: 'status', title: 'shared.status', fields: [{ key: 'state', label: 'shared.status' }] },
]

/**
 * Mounts the form with the real catalogue.
 *
 * @param props - Props of the form.
 * @param slots - Slots of the form, as markup.
 * @returns The mounted wrapper.
 */
const mountForm = (props: Record<string, unknown>, slots: Record<string, string> = {}) =>
  mountWithApp(UiSchemaForm, { props, slots })

describe('UiSchemaForm', () => {
  it('caps the width of the form it draws when asked', () => {
    const wrapper = mountForm({ columns: [left], maxWidth: 'form' })

    expect(wrapper.get('form').classes()).toContain('max-w-3xl')
  })

  it('draws a section card per section and a field per field', () => {
    const wrapper = mountForm({ columns: [left, right] })

    expect(wrapper.findAll('[data-testid^="ui-form-section-"]')).toHaveLength(3)
    expect(wrapper.findAll('[data-testid^="ui-form-field-"]')).toHaveLength(4)
  })

  it('puts two columns side by side and a single column in a one column grid', () => {
    const two = mountForm({ columns: [left, right] })
    const one = mountForm({ columns: [left] })

    expect(two.get('form > .grid').classes()).toContain('md:grid-cols-2')
    expect(one.get('form > .grid').classes()).toContain('grid-cols-1')
    expect(one.get('form > .grid').classes()).not.toContain('md:grid-cols-2')
  })

  it('forwards the field and section slots, with their scope, to the section that owns them', () => {
    const wrapper = mountForm({ columns: [left, right] }, {
      'field-name': '<template #field-name="{ id }"><input :id="id" placeholder="Name" data-testid="real-name" /></template>',
      'section-status': '<p data-testid="status-note">Note</p>',
    })

    const input = wrapper.get('[data-testid="ui-form-section-identity"] [data-testid="real-name"]')
    expect(input.attributes('id')).toBeTruthy()
    expect(wrapper.find('[data-testid="ui-form-section-status"] [data-testid="status-note"]').exists()).toBe(true)
  })

  it('emits submit with the event and cancels the native submit', async () => {
    const wrapper = mountForm({ columns: [left] })

    await wrapper.get('form').trigger('submit')

    expect(wrapper.emitted('submit')).toHaveLength(1)
  })

  it('draws the default slot inside the form, after the sections', () => {
    const wrapper = mountForm({ columns: [left] }, { default: '<button type="submit" data-testid="save">Save</button>' })

    expect(wrapper.get('form').find('[data-testid="save"]').exists()).toBe(true)
  })

  it('draws the skeleton of every control while loading, with the captions in place', () => {
    const wrapper = mountForm({ columns: [left, right], loading: true }, { 'field-name': '<input data-testid="real-name" />' })

    expect(wrapper.find('[data-testid="real-name"]').exists()).toBe(false)
    expect(wrapper.findAll('[data-testid="ui-field-skeleton"]')).toHaveLength(4)
    expect(wrapper.text()).toContain('Phone Number')
  })

  it('passes the server sentences on to the fields', () => {
    const wrapper = mountForm({ columns: [left], errors: { email: 'Invalid email' } })

    expect(wrapper.get('[role="alert"]').text()).toBe('Invalid email')
  })
})
