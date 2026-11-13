/**
 * Unit tests for UiIconButton: accessible name, looks and disabled state.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiIconButton from '@/components/ui/UiIconButton.vue'

describe('UiIconButton', () => {
  it('names the action for assistive technology', () => {
    const wrapper = mount(UiIconButton, { props: { label: 'Delete row' }, slots: { default: 'x' } })

    expect(wrapper.attributes('aria-label')).toBe('Delete row')
    expect(wrapper.attributes('title')).toBe('Delete row')
    expect(wrapper.attributes('type')).toBe('button')
  })

  it('takes its footprint from the size', () => {
    const small = mount(UiIconButton, { props: { label: 'a', size: 'sm' } })
    const large = mount(UiIconButton, { props: { label: 'a', size: 'lg' } })

    expect(small.classes()).toContain('w-8')
    expect(large.classes()).toContain('w-12')
  })

  it('is disabled when asked and keeps data-testid', () => {
    const wrapper = mount(UiIconButton, {
      props: { label: 'a', disabled: true },
      attrs: { 'data-testid': 'probe' },
    })

    expect(wrapper.attributes('disabled')).toBeDefined()
    expect(wrapper.attributes('data-testid')).toBe('probe')
  })
})
