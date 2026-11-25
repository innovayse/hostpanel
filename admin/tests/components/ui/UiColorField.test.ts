/**
 * Unit tests for UiColorField: hex text input, swatch preview, validation, keyboard-only editing.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiColorField from '@/components/ui/UiColorField.vue'

describe('UiColorField', () => {
  it('renders a text input, never a native colour picker', () => {
    const wrapper = mount(UiColorField, { props: { modelValue: '#0ea5e9' } })
    const input = wrapper.get('input')

    expect(input.attributes('type')).toBe('text')
    expect((input.element as HTMLInputElement).value).toBe('#0ea5e9')
    expect(wrapper.find('input[type="color"]').exists()).toBe(false)
  })

  it('emits the typed text', async () => {
    const wrapper = mount(UiColorField, { props: { modelValue: '' } })

    await wrapper.get('input').setValue('#112233')

    expect(wrapper.emitted('update:modelValue')?.[0]).toEqual(['#112233'])
  })

  it('previews a complete colour as a filled swatch', () => {
    const wrapper = mount(UiColorField, { props: { modelValue: '#AABBCC' } })

    expect(wrapper.get('svg rect').attributes('fill')).toBe('#aabbcc')
  })

  it('previews the fallback while the text is incomplete', () => {
    const wrapper = mount(UiColorField, { props: { modelValue: '#12', fallback: '#0ea5e9' } })

    expect(wrapper.get('svg rect').attributes('fill')).toBe('#0ea5e9')
  })

  it('draws no fill for empty text without a fallback', () => {
    const wrapper = mount(UiColorField, { props: { modelValue: '' } })

    expect(wrapper.find('svg rect').exists()).toBe(false)
  })

  it('flags text that is neither empty nor #rrggbb as invalid', () => {
    const bad = mount(UiColorField, { props: { modelValue: '#12' } })
    const good = mount(UiColorField, { props: { modelValue: '#123456' } })
    const empty = mount(UiColorField, { props: { modelValue: '' } })

    expect(bad.get('input').attributes('aria-invalid')).toBe('true')
    expect(good.get('input').attributes('aria-invalid')).toBeUndefined()
    expect(empty.get('input').attributes('aria-invalid')).toBeUndefined()
  })

  it('forwards attributes and key listeners to the input', async () => {
    let entered = 0
    const wrapper = mount(UiColorField, {
      props: { modelValue: '' },
      attrs: { 'aria-label': 'Brand colour', 'data-testid': 'colour', onKeyup: () => { entered += 1 } },
    })
    const input = wrapper.get('input')

    await input.trigger('keyup', { key: 'Enter' })

    expect(input.attributes('aria-label')).toBe('Brand colour')
    expect(input.attributes('data-testid')).toBe('colour')
    expect(entered).toBe(1)
  })
})

describe('UiColorField swatch frame', () => {
  it('has the control border token and the height of the text field beside it', () => {
    const frame = mount(UiColorField, { props: { modelValue: '#0ea5e9' } }).get('svg').element.parentElement as HTMLElement

    expect(frame.className).toContain('border-border-control')
    expect(frame.className).toContain('h-10.5')
  })
})
