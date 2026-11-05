/**
 * Unit tests for UiCheckbox: toggling from the box and the label, the disabled state, the
 * description and accessibility attributes.
 */
import { describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import UiCheckbox from '~/components/ui/UiCheckbox.vue'

describe('UiCheckbox', () => {
  it('exposes the checked state to assistive technology', () => {
    const on = mountWithKit(UiCheckbox, { props: { modelValue: true } })
    const off = mountWithKit(UiCheckbox, { props: { modelValue: false } })

    expect(on.find('[role="checkbox"]').attributes('aria-checked')).toBe('true')
    expect(off.find('[role="checkbox"]').attributes('aria-checked')).toBe('false')
  })

  it('emits the opposite value when the box is clicked', async () => {
    const wrapper = mountWithKit(UiCheckbox, { props: { modelValue: false } })

    await wrapper.find('[role="checkbox"]').trigger('click')

    expect(wrapper.emitted('update:modelValue')?.[0]).toEqual([true])
  })

  it('emits when the label is clicked', async () => {
    const wrapper = mountWithKit(UiCheckbox, { props: { modelValue: true, label: 'Accept' } })

    await wrapper.find('label').trigger('click')

    expect(wrapper.emitted('update:modelValue')?.[0]).toEqual([false])
  })

  it('does not emit when disabled', async () => {
    const wrapper = mountWithKit(UiCheckbox, { props: { modelValue: false, disabled: true, label: 'Accept' } })

    await wrapper.find('[role="checkbox"]').trigger('click')
    await wrapper.find('label').trigger('click')

    expect(wrapper.emitted('update:modelValue')).toBeUndefined()
    expect(wrapper.find('[role="checkbox"]').classes()).toContain('cursor-not-allowed')
  })

  it('follows the native checkbox change', async () => {
    const wrapper = mountWithKit(UiCheckbox, { props: { modelValue: false } })

    await wrapper.find('input[type="checkbox"]').setValue(true)

    expect(wrapper.emitted('update:modelValue')?.[0]).toEqual([true])
  })

  it('renders the label, the description and the slot', () => {
    const wrapper = mountWithKit(UiCheckbox, { props: { label: 'Terms', description: 'Read them first' }, slots: { default: ' (link)' } })

    expect(wrapper.text()).toContain('Terms')
    expect(wrapper.text()).toContain('Read them first')
    expect(wrapper.text()).toContain('(link)')
  })

  it('binds the label to the native checkbox and passes the testid', () => {
    const wrapper = mountWithKit(UiCheckbox, { props: { label: 'Terms', testid: 'terms-box' } })

    expect(wrapper.find('label').attributes('for')).toBe(wrapper.find('input').attributes('id'))
    expect(wrapper.find('[role="checkbox"]').attributes('data-testid')).toBe('terms-box')
  })

  it('names the box by its label and describes the box and the native input by the description', () => {
    const wrapper = mountWithKit(UiCheckbox, { props: { id: 'agree', label: 'Terms', description: 'Read them first' } })

    const box = wrapper.find('[role="checkbox"]')
    expect(box.attributes('aria-labelledby')).toBe(wrapper.find('label').attributes('id'))
    expect(box.attributes('aria-describedby')).toBe(wrapper.find('p').attributes('id'))
    expect(wrapper.find('input').attributes('aria-describedby')).toBe(wrapper.find('p').attributes('id'))
  })

  it('adds no label or description references when there are none', () => {
    const wrapper = mountWithKit(UiCheckbox)

    expect(wrapper.find('[role="checkbox"]').attributes('aria-labelledby')).toBeUndefined()
    expect(wrapper.find('[role="checkbox"]').attributes('aria-describedby')).toBeUndefined()
  })

  it('tags the native input derived from the testid, and leaves it untagged without one', () => {
    const tagged = mountWithKit(UiCheckbox, { props: { testid: 'terms-box' } })
    const plain = mountWithKit(UiCheckbox)

    expect(tagged.find('input').attributes('data-testid')).toBe('terms-box-input')
    expect(plain.find('input').attributes('data-testid')).toBeUndefined()
  })

  it('draws the kit focus ring on the visible box', () => {
    const box = mountWithKit(UiCheckbox).find('[role="checkbox"]')

    expect(box.classes()).toContain('focus-visible:ring-2')
    expect(box.classes()).toContain('focus-visible:ring-primary-500')
  })
})
