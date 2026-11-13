/**
 * Unit tests for UiField: caption wired to the control, hint, and the server's error sentence.
 */
import { describe, expect, it } from 'vitest'
import { h, ref } from 'vue'
import { mount } from '@vue/test-utils'
import UiField from '@/components/ui/UiField.vue'
import { UI_FIELD_LOADING } from '@/utils/ui/injection'

/**
 * Mounts a field whose control is a bare input bound to the slot props.
 *
 * @param props - Props for the field.
 * @returns The wrapper.
 */
const mountField = (props: InstanceType<typeof UiField>['$props']) =>
  mount(UiField, {
    props,
    slots: {
      default: (slot: { id: string; invalid: boolean; describedby?: string }) =>
        h('input', { id: slot.id, 'aria-invalid': slot.invalid, 'aria-describedby': slot.describedby }),
    },
  })

describe('UiField', () => {
  it('draws a skeleton in place of the control while loading and keeps the caption', () => {
    const wrapper = mountField({ label: 'Name', loading: true })

    expect(wrapper.find('[data-testid="ui-field-skeleton"]').exists()).toBe(true)
    expect(wrapper.find('input').exists()).toBe(false)
    expect(wrapper.get('label').text()).toBe('Name')
  })

  it('follows the loading flag a parent form provides', () => {
    const wrapper = mount(UiField, {
      props: { label: 'Name' },
      slots: { default: () => h('input') },
      global: { provide: { [UI_FIELD_LOADING as symbol]: ref(true) } },
    })

    expect(wrapper.find('[data-testid="ui-field-skeleton"]').exists()).toBe(true)
  })

  it('links the caption to the control', () => {
    const wrapper = mountField({ label: 'Name' })

    expect(wrapper.get('label').attributes('for')).toBe(wrapper.get('input').attributes('id'))
    expect(wrapper.get('label').text()).toBe('Name')
  })

  it('marks a required field', () => {
    const wrapper = mountField({ label: 'Name', required: true })

    expect(wrapper.get('label').text()).toContain('*')
  })

  it('shows the hint while there is no error', () => {
    const wrapper = mountField({ hint: 'Letters only' })

    expect(wrapper.text()).toContain('Letters only')
    expect(wrapper.get('input').attributes('aria-describedby')).toBeDefined()
  })

  it('shows the error instead of the hint and announces it', () => {
    const wrapper = mountField({ hint: 'Letters only', error: 'Name is taken' })
    const alert = wrapper.get('[role="alert"]')

    expect(alert.text()).toBe('Name is taken')
    expect(wrapper.text()).not.toContain('Letters only')
    expect(wrapper.get('input').attributes('aria-invalid')).toBe('true')
    expect(wrapper.get('input').attributes('aria-describedby')).toBe(alert.attributes('id'))
  })

  it('renders only the control when there is nothing else', () => {
    const wrapper = mountField({})

    expect(wrapper.find('label').exists()).toBe(false)
    expect(wrapper.find('[role="alert"]').exists()).toBe(false)
    expect(wrapper.get('input').attributes('aria-describedby')).toBeUndefined()
  })

  it('hides the required asterisk from assistive technology', () => {
    const wrapper = mount(UiField, { props: { label: 'Name', required: true } })

    expect(wrapper.get('span[aria-hidden="true"]').text()).toBe('*')
  })
})
