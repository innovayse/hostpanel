/**
 * Unit tests for TableTh: the header cell and its alignment.
 */
import { describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import TableTh from '~/components/ui/UiTableTh.vue'
import { UiAlign } from '~/utils/ui/enums'

describe('TableTh', () => {
  it('is a left-aligned th by default', () => {
    const wrapper = mountWithKit(TableTh, { slots: { default: 'Name' } })

    expect(wrapper.element.tagName).toBe('TH')
    expect(wrapper.text()).toBe('Name')
    expect(wrapper.classes()).toContain('text-left')
  })

  it.each([
    [UiAlign.Center, 'text-center'],
    [UiAlign.Right, 'text-right'],
  ])('aligns %s with %s', (align, expected) => {
    expect(mountWithKit(TableTh, { props: { align } }).classes()).toContain(expected)
  })

  it('heads the actions column: right aligned and captioned "Actions" by default', () => {
    const wrapper = mountWithKit(TableTh, { props: { actions: true } })

    expect(wrapper.classes()).toContain('text-right')
    expect(wrapper.text()).toBe('common.actions')
  })

  it('draws no caption of its own for a header that is not the actions column', () => {
    expect(mountWithKit(TableTh).text()).toBe('')
  })
})
