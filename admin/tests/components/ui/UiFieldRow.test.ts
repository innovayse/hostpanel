/**
 * Unit tests for UiFieldRow: the field and its action button on one wrapping row.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiFieldRow from '@/components/ui/UiFieldRow.vue'

describe('UiFieldRow', () => {
  it('renders the field and the button in order on a wrapping row aligned to the field bottom', () => {
    const wrapper = mount(UiFieldRow, { slots: { default: '<div>FIELD</div><button>SAVE</button>' } })

    expect(wrapper.attributes('data-testid')).toBe('ui-field-row')
    expect(wrapper.classes()).toEqual(expect.arrayContaining(['flex', 'flex-row', 'flex-wrap', 'items-end']))
    expect(wrapper.text().indexOf('FIELD')).toBeLessThan(wrapper.text().indexOf('SAVE'))
  })

  it('lets the field take the free width, at least 12rem and at most the row, so the buttons wrap instead of squeezing it', () => {
    const wrapper = mount(UiFieldRow)

    expect(wrapper.classes()).toEqual(expect.arrayContaining([
      '[&>:first-child]:min-w-0',
      '[&>:first-child]:max-w-full',
      '[&>:first-child]:flex-[1_1_12rem]',
    ]))
  })
})
