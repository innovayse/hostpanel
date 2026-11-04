/**
 * Unit tests for PaymentMethodRow: the label, translated type, expiry, default badge and the two emitted actions.
 */
import { describe, expect, it } from 'vitest'
import { mountAccount } from '~/tests/helpers/mountAccount'
import PaymentMethodRow from '~/components/account/PaymentMethodRow.vue'
import { PaymentMethodType } from '~/types/payment'
import type { PaymentMethod } from '~/types/payment'

/** A saved card that is not the default. */
const method: PaymentMethod = {
  gatewayModuleId: 'stripe', id: '9', type: PaymentMethodType.Card, description: 'Visa •••• 4242', gatewayName: 'card',
  cardLastFour: '4242', cardExpiry: '12/30', cardType: 'visa', isDefault: false
}

describe('PaymentMethodRow', () => {
  it('shows the brand with the masked number and the expiry', () => {
    const text = mountAccount(PaymentMethodRow, { props: { method } }).text()

    expect(text).toContain('Visa •••• 4242')
    expect(text).toContain('12/30')
  })

  it('shows the translated type label, never the raw type value', () => {
    const wrapper = mountAccount(PaymentMethodRow, { props: { method } })

    expect(wrapper.find('[data-testid="account-payment-type"]').text()).toBe('client.payment.type.card')
  })

  it('marks the default method and hides "set default" on it', () => {
    const wrapper = mountAccount(PaymentMethodRow, { props: { method: { ...method, isDefault: true } } })

    expect(wrapper.find('[data-testid="account-payment-default-badge"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="account-payment-default-9"]').exists()).toBe(false)
  })

  it('offers "set default" and no badge on a method that is not the default', () => {
    const wrapper = mountAccount(PaymentMethodRow, { props: { method } })

    expect(wrapper.find('[data-testid="account-payment-default-badge"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="account-payment-default-9"]').exists()).toBe(true)
  })

  it('emits the method for "set default"', async () => {
    const wrapper = mountAccount(PaymentMethodRow, { props: { method } })

    await wrapper.find('[data-testid="account-payment-default-9"]').trigger('click')

    expect(wrapper.emitted('setDefault')).toEqual([[method]])
  })

  it('emits the method for "remove"', async () => {
    const wrapper = mountAccount(PaymentMethodRow, { props: { method } })

    await wrapper.find('[data-testid="account-payment-remove-9"]').trigger('click')

    expect(wrapper.emitted('remove')).toEqual([[method]])
  })

  it('disables both buttons while the method is being made the default', () => {
    const wrapper = mountAccount(PaymentMethodRow, { props: { method, settingDefault: true } })

    expect(wrapper.find('[data-testid="account-payment-default-9"]').attributes('disabled')).toBeDefined()
    expect(wrapper.find('[data-testid="account-payment-remove-9"]').attributes('disabled')).toBeDefined()
  })

  it('disables both buttons while the method is being removed', () => {
    const wrapper = mountAccount(PaymentMethodRow, { props: { method, removing: true } })

    expect(wrapper.find('[data-testid="account-payment-default-9"]').attributes('disabled')).toBeDefined()
    expect(wrapper.find('[data-testid="account-payment-remove-9"]').attributes('disabled')).toBeDefined()
  })

  it('marks "remove" as a destructive action', () => {
    const wrapper = mountAccount(PaymentMethodRow, { props: { method } })

    expect(wrapper.find('[data-testid="account-payment-remove-9"]').classes()).toContain('ui-btn-danger')
  })
})
