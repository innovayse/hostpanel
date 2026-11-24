/**
 * Unit tests for UiKeyValue: the label, the value, the slot and the emphasis.
 */
import { describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import UiKeyValue from '~/components/ui/UiKeyValue.vue'

describe('UiKeyValue', () => {
  it('shows the label and the value', () => {
    const wrapper = mountWithKit(UiKeyValue, { props: { label: 'Domain', value: 'example.com' } })

    expect(wrapper.text()).toContain('Domain')
    expect(wrapper.text()).toContain('example.com')
  })

  it('lets the slot replace the value', () => {
    const wrapper = mountWithKit(UiKeyValue, { props: { label: 'Status', value: 'x' }, slots: { default: 'Active' } })

    expect(wrapper.text()).toContain('Active')
    expect(wrapper.text()).not.toContain('x')
  })

  it('makes a strong value semibold', () => {
    const strong = mountWithKit(UiKeyValue, { props: { label: 'Total', value: 1, strong: true } })
    const normal = mountWithKit(UiKeyValue, { props: { label: 'Total', value: 1 } })

    expect(strong.findAll('span')[1]!.classes()).toContain('font-semibold')
    expect(normal.findAll('span')[1]!.classes()).toContain('font-medium')
  })

  it('lets a long value break inside the row', () => {
    const wrapper = mountWithKit(UiKeyValue, { props: { label: 'Host', value: 'x' } })

    expect(wrapper.findAll('span')[1]!.classes()).toEqual(expect.arrayContaining(['min-w-0', 'break-words']))
  })
})
