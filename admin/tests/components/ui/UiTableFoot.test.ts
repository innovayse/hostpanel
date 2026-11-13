/**
 * Unit tests for UiTableFoot: renders a native `<tfoot>` and forwards class and slot content.
 */
import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import UiTableFoot from '@/components/ui/UiTableFoot.vue'

describe('UiTableFoot', () => {
  it('renders a tfoot element with the slot content', () => {
    const wrapper = mount(UiTableFoot, {
      attrs: { class: 'border-t' },
      slots: { default: '<tr><td>Total</td></tr>' },
      attachTo: document.createElement('table'),
    })
    expect(wrapper.element.tagName).toBe('TFOOT')
    expect(wrapper.classes()).toContain('border-t')
    expect(wrapper.text()).toBe('Total')
  })

  it('is set apart from the rows by a hairline above it', () => {
    const wrapper = mount(UiTableFoot, { attachTo: document.createElement('table') })

    expect(wrapper.classes()).toEqual(expect.arrayContaining(['border-t', 'border-border-subtle', 'font-semibold']))
  })
})
