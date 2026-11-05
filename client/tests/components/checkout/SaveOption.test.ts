/**
 * Unit tests for SaveOption: the label and the agreement wording, the off-by-default state, the
 * choice it reports and the disabled state.
 */
import { describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import SaveOption from '~/components/checkout/SaveOption.vue'

/**
 * Mounts the option.
 *
 * @param props - Props to pass.
 * @returns The wrapper.
 */
const mountOption = (props: { modelValue?: boolean, disabled?: boolean } = {}) =>
  mountWithKit(SaveOption, { props, attrs: { testid: 'save-option' } })

describe('SaveOption', () => {
  it('shows the label and the renewal charge agreement', () => {
    const wrapper = mountOption()

    expect(wrapper.text()).toContain('checkout.saveForRenewals')
    expect(wrapper.text()).toContain('checkout.saveForRenewalsAgreement')
  })

  it('is off unless told otherwise', () => {
    const wrapper = mountOption()

    expect(wrapper.find('[data-testid="save-option"]').attributes('aria-checked')).toBe('false')
  })

  it('reports the choice when the box is ticked', async () => {
    const wrapper = mountOption()

    await wrapper.find('[data-testid="save-option"]').trigger('click')

    expect(wrapper.emitted('update:modelValue')).toEqual([[true]])
  })

  it('shows a ticked box and clears it on the next click', async () => {
    const wrapper = mountOption({ modelValue: true })
    const box = wrapper.find('[data-testid="save-option"]')

    expect(box.attributes('aria-checked')).toBe('true')
    await box.trigger('click')

    expect(wrapper.emitted('update:modelValue')).toEqual([[false]])
  })

  it('cannot be changed while disabled', async () => {
    const wrapper = mountOption({ disabled: true })
    const box = wrapper.find('[data-testid="save-option"]')

    expect(box.attributes('disabled')).toBeDefined()
    await box.trigger('click')

    expect(wrapper.emitted('update:modelValue')).toBeUndefined()
  })
})
