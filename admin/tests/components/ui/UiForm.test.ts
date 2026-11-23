/**
 * Unit tests for UiForm: native form, cancelled submit re-emitted, stacked layout.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiForm from '@/components/ui/UiForm.vue'

describe('UiForm', () => {
  it('renders the native form with its slot', () => {
    const wrapper = mount(UiForm, { slots: { default: 'content' } })

    expect(wrapper.element.tagName).toBe('FORM')
    expect(wrapper.text()).toBe('content')
  })

  it('passes classes and data attributes through', () => {
    const wrapper = mount(UiForm, { attrs: { class: 'x-class', 'data-testid': 'probe' } })

    expect(wrapper.classes()).toContain('x-class')
    expect(wrapper.attributes('data-testid')).toBe('probe')
  })

  it('cancels the native submit and re-emits it', async () => {
    const wrapper = mount(UiForm)
    const event = new Event('submit', { cancelable: true })

    wrapper.element.dispatchEvent(event)

    expect(event.defaultPrevented).toBe(true)
    expect(wrapper.emitted('submit')).toHaveLength(1)
  })

  it('caps its width when asked and fills its parent otherwise', () => {
    expect(mount(UiForm, { props: { maxWidth: 'form' } }).classes()).toContain('max-w-3xl')
    expect(mount(UiForm).classes().some((name) => name.startsWith('max-w-'))).toBe(false)
  })

  it('stacks its fields when asked', () => {
    expect(mount(UiForm, { props: { stacked: true } }).classes()).toEqual(expect.arrayContaining(['flex', 'flex-col', 'gap-4']))
  })
})
