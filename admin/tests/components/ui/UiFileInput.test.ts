/**
 * Unit tests for UiFileInput: native file input, files emitted as an array, native change kept.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiFileInput from '@/components/ui/UiFileInput.vue'

describe('UiFileInput', () => {
  it('renders a file input carrying accept and classes', () => {
    const wrapper = mount(UiFileInput, { attrs: { accept: 'image/png', class: 'hidden' } })

    expect(wrapper.attributes('type')).toBe('file')
    expect(wrapper.attributes('accept')).toBe('image/png')
    expect(wrapper.classes()).toContain('hidden')
  })

  it('forwards the change listener to the input', async () => {
    let calls = 0
    const wrapper = mount(UiFileInput, { attrs: { onChange: () => { calls += 1 } } })

    await wrapper.trigger('change')

    expect(calls).toBe(1)
  })

  it('emits the chosen files as an array', async () => {
    const wrapper = mount(UiFileInput)
    const file = new File(['x'], 'a.zip')
    Object.defineProperty(wrapper.element, 'files', { value: [file], configurable: true })

    await wrapper.trigger('change')

    expect(wrapper.emitted('update:modelValue')?.[0]).toEqual([[file]])
  })

  it('emits an empty array when nothing is selected', async () => {
    const wrapper = mount(UiFileInput)
    Object.defineProperty(wrapper.element, 'files', { value: null, configurable: true })

    await wrapper.trigger('change')

    expect(wrapper.emitted('update:modelValue')?.[0]).toEqual([[]])
  })
})

describe('UiFileInput button look', () => {
  it('draws the picker button with the control border token', () => {
    const classes = mount(UiFileInput).classes()

    expect(classes).toContain('file:border-border-control')
    expect(classes).not.toContain('file:border-border')
  })
})
