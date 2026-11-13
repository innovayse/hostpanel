/**
 * Unit tests for UiFormActions: the shared end-of-form action row.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiFormActions from '@/components/ui/UiFormActions.vue'

describe('UiFormActions', () => {
  it('renders the slot in order on a right-aligned row', () => {
    const wrapper = mount(UiFormActions, { slots: { default: '<span>CANCEL</span><span>SAVE</span>' } })

    expect(wrapper.classes()).toEqual(expect.arrayContaining(['flex', 'flex-row', 'justify-end']))
    expect(wrapper.text().indexOf('CANCEL')).toBeLessThan(wrapper.text().indexOf('SAVE'))
  })

  it('stacks the buttons at full width on phones, primary on top', () => {
    const wrapper = mount(UiFormActions)

    expect(wrapper.classes()).toEqual(expect.arrayContaining(['max-sm:flex-col-reverse', 'max-sm:items-stretch']))
    expect(wrapper.attributes('data-testid')).toBe('ui-form-actions')
  })
})
