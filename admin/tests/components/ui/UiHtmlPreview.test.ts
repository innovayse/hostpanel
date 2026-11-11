/**
 * Unit tests for UiHtmlPreview: the markup goes into the frame's `srcdoc`, never into the page,
 * and the frame runs nothing (an empty `sandbox`).
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiHtmlPreview from '@/components/ui/UiHtmlPreview.vue'

describe('UiHtmlPreview', () => {
  it('puts the HTML into a sandboxed frame with an accessible name', () => {
    const wrapper = mount(UiHtmlPreview, {
      props: { html: '<p onclick="steal()">Hi</p><script>steal()</script>', title: 'Mail preview' },
      attrs: { 'data-testid': 'frame' },
    })
    const frame = wrapper.get('[data-testid="frame"]')

    expect(frame.element.tagName).toBe('IFRAME')
    expect(frame.attributes('srcdoc')).toContain('<script>steal()</script>')
    expect(frame.attributes('sandbox')).toBe('')
    expect(frame.attributes('title')).toBe('Mail preview')
  })

  it('does not render the markup into the surrounding page', () => {
    const wrapper = mount(UiHtmlPreview, { props: { html: '<p id="inner">Hi</p>', title: 'Mail preview' } })

    expect(wrapper.find('#inner').exists()).toBe(false)
  })
})
