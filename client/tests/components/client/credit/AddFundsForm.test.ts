/**
 * Unit tests for ClientCreditAddFundsForm: the amount is held to the admin's limits before it is sent, a plain amount with a comma
 * or a point is read as the same number, and the backend's refusal and the busy state show.
 *
 * @module tests/components/client/credit/AddFundsForm.test
 */

import { describe, expect, it } from 'vitest'
import { mountClient } from '~/tests/helpers/mountClient'
import AddFundsForm from '~/components/client/credit/AddFundsForm.vue'

/** Props every case starts from: limits 5 to 100 in dollars. */
const base = { minimum: 5, maximum: 100, currency: 'USD' }

/**
 * Types an amount and submits the form.
 *
 * @param wrapper - The mounted form.
 * @param value - What the client types.
 * @returns Resolves once the submit has been handled.
 */
const submitAmount = async (wrapper: ReturnType<typeof mountClient>, value: string): Promise<void> => {
  await wrapper.get('[data-testid="credit-add-amount"]').setValue(value)
  await wrapper.get('form').trigger('submit')
}

describe('ClientCreditAddFundsForm', () => {
  it('states the limits in the client currency', () => {
    const wrapper = mountClient(AddFundsForm, { props: base })

    expect(wrapper.get('[data-testid="credit-add-limits"]').text()).toBe('client.credit.add.limits')
  })

  it('emits the amount when it is inside the limits', async () => {
    const wrapper = mountClient(AddFundsForm, { props: base })

    await submitAmount(wrapper, '40')

    expect(wrapper.emitted('submit')).toEqual([[40]])
  })

  it('accepts both limits themselves and a decimal comma', async () => {
    const wrapper = mountClient(AddFundsForm, { props: base })

    await submitAmount(wrapper, '5')
    await submitAmount(wrapper, '100')
    await submitAmount(wrapper, '12,5')

    expect(wrapper.emitted('submit')).toEqual([[5], [100], [12.5]])
  })

  it.each(['4.99', '100.01', '0', '-5', 'ten', '', '1.234', '1e3'])('refuses %j and says why', async (value) => {
    const wrapper = mountClient(AddFundsForm, { props: base })

    await submitAmount(wrapper, value)

    expect(wrapper.emitted('submit')).toBeUndefined()
    expect(wrapper.text()).toContain('client.credit.add.amountInvalid')
  })

  it('says nothing about the amount before the field was touched', () => {
    const wrapper = mountClient(AddFundsForm, { props: base })

    expect(wrapper.text()).not.toContain('client.credit.add.amountInvalid')
  })

  it('shows the sentence the backend refused with', () => {
    const wrapper = mountClient(AddFundsForm, { props: { ...base, error: 'The amount is outside the range.' } })

    expect(wrapper.get('[data-testid="ui-form-error"]').text()).toBe('The amount is outside the range.')
  })

  it('disables the button while the invoice is being raised', () => {
    const wrapper = mountClient(AddFundsForm, { props: { ...base, busy: true } })

    expect(wrapper.get('[data-testid="credit-add-submit"]').attributes('disabled')).toBeDefined()
  })

  it('lets the client back out to the credit page', () => {
    const wrapper = mountClient(AddFundsForm, { props: base })

    expect(wrapper.get('[data-testid="credit-add-cancel"]').attributes('href')).toBe('/client/credit')
  })
})
