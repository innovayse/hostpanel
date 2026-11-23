/**
 * Unit tests for UiOption: the option role, the active look, the click and hover events and the scroll into view
 * when the option becomes active.
 */
import { describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import UiOption from '@/components/ui/UiOption.vue'

describe('UiOption', () => {
  it('is an option that reports whether it is the active one', async () => {
    const wrapper = mount(UiOption, { props: { active: false }, slots: { default: 'Orders' } })

    expect(wrapper.attributes('role')).toBe('option')
    expect(wrapper.attributes('aria-selected')).toBe('false')
    expect(wrapper.text()).toBe('Orders')
    expect(wrapper.classes()).not.toContain('bg-surface-hover')

    await wrapper.setProps({ active: true })

    expect(wrapper.attributes('aria-selected')).toBe('true')
    expect(wrapper.classes()).toContain('bg-surface-hover')
  })

  it('emits select on a click and hover on a pointer move', async () => {
    const wrapper = mount(UiOption, { props: { active: false } })

    await wrapper.trigger('click')
    await wrapper.trigger('mousemove')

    expect(wrapper.emitted('select')).toHaveLength(1)
    expect(wrapper.emitted('hover')).toHaveLength(1)
  })

  it('scrolls itself into view when it becomes active, and not when it stops being active', async () => {
    const wrapper = mount(UiOption, { props: { active: false }, attachTo: document.body })
    const scroll = vi.fn()
    wrapper.element.scrollIntoView = scroll

    await wrapper.setProps({ active: true })
    await wrapper.vm.$nextTick()
    expect(scroll).toHaveBeenCalledWith({ block: 'nearest' })

    scroll.mockClear()
    await wrapper.setProps({ active: false })
    await wrapper.vm.$nextTick()
    expect(scroll).not.toHaveBeenCalled()
    wrapper.unmount()
  })
})
