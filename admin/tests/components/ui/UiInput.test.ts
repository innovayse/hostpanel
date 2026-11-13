/**
 * Unit tests for UiInput: native input, themed field, model updates, label and icon wrapper.
 */
import { describe, expect, it } from 'vitest'
import { h } from 'vue'
import { mount } from '@vue/test-utils'
import UiInput from '@/components/ui/UiInput.vue'

describe('UiInput', () => {
  it('renders a text input showing the model value', () => {
    const wrapper = mount(UiInput, { props: { modelValue: 'abc' } })
    const input = wrapper.element as HTMLInputElement

    expect(input.tagName).toBe('INPUT')
    expect(input.type).toBe('text')
    expect(input.value).toBe('abc')
  })

  it('shows an empty field for a null model', () => {
    const wrapper = mount(UiInput, { props: { modelValue: null } })

    expect((wrapper.element as HTMLInputElement).value).toBe('')
  })

  it('emits the typed text', async () => {
    const wrapper = mount(UiInput)

    await wrapper.setValue('hello')

    expect(wrapper.emitted('update:modelValue')?.[0]).toEqual(['hello'])
  })

  it('honours the type and placeholder props and passes classes through', () => {
    const wrapper = mount(UiInput, {
      props: { type: 'password', placeholder: 'secret' },
      attrs: { class: 'w-24' },
    })

    const input = wrapper.get('input')

    expect(input.attributes('type')).toBe('password')
    expect(input.attributes('placeholder')).toBe('secret')
    expect(input.classes()).toContain('w-24')
  })

  it('draws the themed field from tokens', () => {
    const wrapper = mount(UiInput)

    expect(wrapper.classes()).toEqual(expect.arrayContaining(['bg-surface-field', 'border-border-control', 'rounded-control']))
  })

  it('marks an invalid field', () => {
    const wrapper = mount(UiInput, { props: { invalid: true } })

    expect(wrapper.attributes('aria-invalid')).toBe('true')
    expect(wrapper.classes()).toContain('border-status-red/60')
  })

  it('forwards native attributes to the input', () => {
    const wrapper = mount(UiInput, { attrs: { maxlength: '5', readonly: '', 'data-testid': 'probe' } })

    expect(wrapper.attributes('maxlength')).toBe('5')
    expect(wrapper.attributes('readonly')).toBeDefined()
    expect(wrapper.attributes('data-testid')).toBe('probe')
  })

  it('wraps the input with a caption when given a label, keeping attrs on the input', () => {
    const wrapper = mount(UiInput, {
      props: { label: 'Email' },
      attrs: { 'data-testid': 'email' },
    })

    expect(wrapper.text()).toContain('Email')
    expect(wrapper.get('input').attributes('data-testid')).toBe('email')
    expect(wrapper.get('input').attributes('aria-label')).toBe('Email')
  })

  it('renders the leading icon slot and pads the input for it', () => {
    const wrapper = mount(UiInput, { slots: { icon: () => h('i', { 'data-testid': 'icon' }) } })

    expect(wrapper.find('[data-testid="icon"]').exists()).toBe(true)
    expect(wrapper.get('input').classes()).toContain('pl-9')
  })

  it('hides a password and flips it to text with the show/hide button', async () => {
    const wrapper = mount(UiInput, { props: { type: 'password', modelValue: 's3cret' } })
    const toggle = wrapper.get('[data-testid="input-toggle-password"]')

    expect(wrapper.get('input').attributes('type')).toBe('password')
    expect(toggle.attributes('aria-pressed')).toBe('false')
    expect(toggle.attributes('aria-label')).toMatch(/show/i)

    await toggle.trigger('click')

    expect(wrapper.get('input').attributes('type')).toBe('text')
    expect(toggle.attributes('aria-pressed')).toBe('true')
    expect(toggle.attributes('aria-label')).toMatch(/hide/i)
  })

  it('offers no show/hide button on a plain text field', () => {
    expect(mount(UiInput).find('[data-testid="input-toggle-password"]').exists()).toBe(false)
  })

  it('hints autofill by type and lets the screen override it', () => {
    expect(mount(UiInput, { props: { type: 'email' } }).attributes('autocomplete')).toBe('email')
    expect(mount(UiInput, { props: { type: 'password' } }).get('input').attributes('autocomplete')).toBe('current-password')
    expect(
      mount(UiInput, { props: { type: 'password' }, attrs: { autocomplete: 'new-password' } }).get('input').attributes('autocomplete'),
    ).toBe('new-password')
    expect(mount(UiInput).attributes('autocomplete')).toBeUndefined()
  })

  it('shows a disabled field as muted with a not-allowed cursor', () => {
    const wrapper = mount(UiInput, { attrs: { disabled: '' } })

    expect(wrapper.attributes('disabled')).toBeDefined()
    expect(wrapper.classes()).toEqual(expect.arrayContaining(['disabled:text-text-muted', 'disabled:cursor-not-allowed']))
  })

  it('carries aria-describedby from the screen so an error is tied to the field', () => {
    const wrapper = mount(UiInput, { props: { invalid: true }, attrs: { 'aria-describedby': 'msg' } })

    expect(wrapper.attributes('aria-describedby')).toBe('msg')
    expect(wrapper.attributes('aria-invalid')).toBe('true')
  })
})
