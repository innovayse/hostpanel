/**
 * Unit tests for UiStatusMessage: the region exists before any text, only its text changes, and a
 * failure is announced assertively.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiStatusMessage from '@/components/ui/UiStatusMessage.vue'

describe('UiStatusMessage', () => {
  it('renders an empty live region without a message', () => {
    const wrapper = mount(UiStatusMessage)

    expect(wrapper.attributes('role')).toBe('status')
    expect(wrapper.attributes('aria-live')).toBe('polite')
    expect(wrapper.text()).toBe('')
    expect(wrapper.attributes('data-tone')).toBeUndefined()
  })

  it('keeps the same element when the text arrives', async () => {
    const wrapper = mount(UiStatusMessage)
    const region = wrapper.element

    await wrapper.setProps({ message: 'Saved.' })

    expect(wrapper.element).toBe(region)
    expect(wrapper.text()).toBe('Saved.')
    expect(wrapper.classes()).toContain('text-status-green')
    expect(wrapper.classes()).toContain('rounded-card')
  })

  it('announces a failure assertively in the danger look', () => {
    const wrapper = mount(UiStatusMessage, { props: { message: 'Refused.', tone: 'danger' } })

    expect(wrapper.attributes('aria-live')).toBe('assertive')
    expect(wrapper.attributes('data-tone')).toBe('danger')
    expect(wrapper.classes()).toContain('text-status-red')
  })
})
