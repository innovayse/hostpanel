/**
 * Unit tests for UiButton: looks, sizes, loading state and attribute pass-through.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiButton from '@/components/ui/UiButton.vue'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiSize } from '@/utils/ui/size'

describe('UiButton', () => {
  it('renders the native button with its slot', () => {
    const wrapper = mount(UiButton, { slots: { default: 'content' } })

    expect(wrapper.element.tagName).toBe('BUTTON')
    expect(wrapper.text()).toBe('content')
  })

  it('passes classes and data attributes through', () => {
    const wrapper = mount(UiButton, { attrs: { class: 'x-class', 'data-testid': 'probe' } })

    expect(wrapper.classes()).toContain('x-class')
    expect(wrapper.attributes('data-testid')).toBe('probe')
  })

  it('adds no colours of its own in the plain look', () => {
    const wrapper = mount(UiButton)

    expect(wrapper.classes()).not.toContain('gradient-brand')
    expect(wrapper.classes()).not.toContain('cursor-pointer')
  })

  it('owns its styling in a named look', () => {
    const primary = mount(UiButton, { props: { variant: 'primary' } })
    const danger = mount(UiButton, { props: { variant: 'danger' } })

    expect(primary.classes()).toContain('not-disabled:gradient-brand')
    expect(danger.classes()).toContain('text-status-red')
  })

  it('changes padding with the size', () => {
    const small = mount(UiButton, { props: { variant: 'secondary', size: 'sm' } })
    const large = mount(UiButton, { props: { variant: 'secondary', size: 'lg' } })

    expect(small.classes()).toContain('px-3.5')
    expect(large.classes()).toContain('px-6')
  })

  it('gives the link look no padding so it aligns with neighbouring text', () => {
    const link = mount(UiButton, { props: { variant: UiButtonVariant.Link, size: UiSize.Sm } })

    expect(link.classes().some((c) => /^p[xy]?-/.test(c))).toBe(false)
    expect(link.classes()).toContain('text-[0.8125rem]')
    expect(link.classes()).toContain('focus-visible:ring-2')
  })

  it('stretches to the container with block', () => {
    expect(mount(UiButton, { props: { block: true } }).classes()).toContain('w-full')
  })

  it('leaves the native type unset unless given', () => {
    expect(mount(UiButton).attributes('type')).toBeUndefined()
    expect(mount(UiButton, { props: { type: 'submit' } }).attributes('type')).toBe('submit')
  })

  it('keeps the label, shows a spinner, disables the button and marks it busy while loading', () => {
    const wrapper = mount(UiButton, { props: { loading: true }, slots: { default: 'Save' } })

    expect(wrapper.text()).toBe('Save')
    expect(wrapper.find('[data-testid="ui-button-spinner"]').exists()).toBe(true)
    expect(wrapper.attributes('disabled')).toBeDefined()
    expect(wrapper.attributes('aria-busy')).toBe('true')
  })

  it('shows a disabled look that is readable and not-allowed, with no hover applied', () => {
    const primary = mount(UiButton, { props: { variant: 'primary', disabled: true }, slots: { default: 'Save' } })
    const secondary = mount(UiButton, { props: { variant: 'secondary', disabled: true } })

    expect(primary.attributes('disabled')).toBeDefined()
    expect(primary.classes()).toEqual(expect.arrayContaining(['disabled:text-text-muted', 'disabled:cursor-not-allowed', 'disabled:bg-surface-hover']))
    expect(primary.classes()).toContain('not-disabled:gradient-brand')
    expect(primary.classes().some((c) => c.includes('opacity'))).toBe(false)
    expect(secondary.classes()).toContain('not-disabled:hover:bg-surface-active')
    expect(secondary.classes()).not.toContain('hover:bg-surface-active')
  })

  it('draws the primary look as the glowing gradient with a ground-coloured label', () => {
    const primary = mount(UiButton, { props: { variant: 'primary' } })

    expect(primary.classes()).toEqual(expect.arrayContaining(['not-disabled:gradient-brand', 'not-disabled:shadow-glow-primary', 'text-text-on-primary', 'rounded-control']))
  })

  it('does not emit click while disabled', async () => {
    const wrapper = mount(UiButton, { props: { disabled: true } })

    await wrapper.trigger('click')

    expect(wrapper.emitted('click')).toBeUndefined()
  })
})
