/**
 * Unit tests for TableTd: the data cell and its alignment.
 */
import { describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import TableTd from '~/components/ui/UiTableTd.vue'
import { UiAlign } from '~/utils/ui/enums'

describe('TableTd', () => {
  it('is a td with the standard padding, left-aligned like the header', () => {
    const wrapper = mountWithKit(TableTd, { slots: { default: 'Value' } })

    expect(wrapper.element.tagName).toBe('TD')
    expect(wrapper.text()).toBe('Value')
    expect(wrapper.classes()).toContain('p-4')
    expect(wrapper.classes()).toContain('text-left')
    expect(wrapper.classes()).not.toContain('text-right')
  })

  it.each([
    [UiAlign.Center, 'text-center'],
    [UiAlign.Right, 'text-right'],
  ])('aligns %s with %s', (align, expected) => {
    expect(mountWithKit(TableTd, { props: { align } }).classes()).toContain(expected)
  })
})
