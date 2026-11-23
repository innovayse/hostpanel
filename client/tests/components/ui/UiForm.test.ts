/**
 * Unit tests for UiForm: submit handling, the error and success banners, spacing and the actions row.
 */
import { describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import UiForm from '~/components/ui/UiForm.vue'
import { UiFormSpacing } from '~/utils/ui/enums'

describe('UiForm', () => {
  it('renders a form with its fields', () => {
    const wrapper = mountWithKit(UiForm, { slots: { default: 'fields' } })

    expect(wrapper.element.tagName).toBe('FORM')
    expect(wrapper.text()).toContain('fields')
  })

  it('emits submit and prevents the native submit', () => {
    const wrapper = mountWithKit(UiForm)
    const event = new Event('submit', { cancelable: true })

    wrapper.element.dispatchEvent(event)

    expect(wrapper.emitted('submit')).toHaveLength(1)
    expect(event.defaultPrevented).toBe(true)
  })

  it('shows the error banner as an alert', () => {
    const wrapper = mountWithKit(UiForm, { props: { error: 'Could not save' } })

    const banner = wrapper.find('[data-testid="ui-form-error"]')
    expect(banner.text()).toBe('Could not save')
    expect(banner.attributes('role')).toBe('alert')
    expect(wrapper.find('[data-testid="ui-form-success"]').exists()).toBe(false)
  })

  it('shows the success banner as a status', () => {
    const wrapper = mountWithKit(UiForm, { props: { success: 'Saved' } })

    const banner = wrapper.find('[data-testid="ui-form-success"]')
    expect(banner.text()).toBe('Saved')
    expect(banner.attributes('role')).toBe('status')
  })

  it.each([
    [UiFormSpacing.Sm, 'space-y-3'],
    [UiFormSpacing.Md, 'space-y-4'],
    [UiFormSpacing.Lg, 'space-y-5'],
  ])('maps spacing %s to %s', (spacing, expected) => {
    const wrapper = mountWithKit(UiForm, { props: { spacing }, slots: { default: 'x' } })

    expect(wrapper.html()).toContain(expected)
  })

  it('adds no spacing class for none', () => {
    const wrapper = mountWithKit(UiForm, { props: { spacing: UiFormSpacing.None }, slots: { default: 'x' } })

    expect(wrapper.html()).not.toContain('space-y-')
  })

  it('renders the actions slot', () => {
    const wrapper = mountWithKit(UiForm, { slots: { actions: 'Save' } })

    expect(wrapper.text()).toContain('Save')
  })

  it('lets the browser submit natively when asked, still emitting submit', () => {
    const wrapper = mountWithKit(UiForm, { props: { nativeSubmit: true }, attrs: { action: '/subscribe', method: 'post' } })
    const event = new Event('submit', { cancelable: true })

    wrapper.element.dispatchEvent(event)

    expect(event.defaultPrevented).toBe(false)
    expect(wrapper.emitted('submit')).toHaveLength(1)
    expect(wrapper.attributes('action')).toBe('/subscribe')
  })

  it('carries a default test id that the caller can override', () => {
    const plain = mountWithKit(UiForm)
    const named = mountWithKit(UiForm, { attrs: { 'data-testid': 'contact-form' } })

    expect(plain.attributes('data-testid')).toBe('ui-form')
    expect(named.attributes('data-testid')).toBe('contact-form')
  })

  it('paints the banners with the shared alert tones so the text is readable in both themes', () => {
    const wrapper = mountWithKit(UiForm, { props: { error: 'Bad', success: 'Saved' } })

    expect(wrapper.find('[data-testid="ui-form-error"]').classes()).toContain('bg-red-50')
    expect(wrapper.find('[data-testid="ui-form-error"] p').classes()).toContain('text-red-700')
    expect(wrapper.find('[data-testid="ui-form-success"]').classes()).toContain('bg-green-50')
    expect(wrapper.find('[data-testid="ui-form-success"] p').classes()).toContain('text-green-700')
  })

  it('wraps the actions row so buttons never run off a narrow screen', () => {
    const wrapper = mountWithKit(UiForm, { slots: { actions: 'Save' } })

    expect(wrapper.find('form > div:last-child').classes()).toEqual(expect.arrayContaining(['flex-wrap', 'justify-end']))
  })
})
