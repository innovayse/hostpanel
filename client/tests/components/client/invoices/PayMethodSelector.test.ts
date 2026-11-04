/**
 * Unit tests for PayMethodSelector: the saved cards, the chosen one and the choice it emits.
 */
import { describe, expect, it } from 'vitest'
import { mountClient } from '~/tests/helpers/mountClient'
import PayMethodSelector from '~/components/client/invoices/PayMethodSelector.vue'
import { PaymentMethodType } from '~/types/payment'
import type { PaymentMethod } from '~/types/payment'

/** Two saved cards. */
const cards = [
  { gatewayModuleId: 'stripe', id: 'pm_1', type: PaymentMethodType.Card, description: 'Visa', gatewayName: 'stripe', cardType: 'visa', cardLastFour: '4242', cardExpiry: '12/30' },
  { gatewayModuleId: 'stripe', id: 'pm_2', type: PaymentMethodType.Card, description: 'Mastercard', gatewayName: 'stripe', cardType: 'mastercard', cardLastFour: '1111', cardExpiry: '01/31' },
] as PaymentMethod[]

describe('PayMethodSelector', () => {
  it('lists the saved cards with their last digits and expiry', () => {
    const wrapper = mountClient(PayMethodSelector, { props: { savedMethods: cards, modelValue: 'pm_1' } })

    expect(wrapper.text()).toContain('Visa •••• 4242')
    expect(wrapper.text()).toContain('12/30')
    expect(wrapper.find('[data-testid="pay-method-pm_2"]').exists()).toBe(true)
  })

  it('marks the chosen card', () => {
    const wrapper = mountClient(PayMethodSelector, { props: { savedMethods: cards, modelValue: 'pm_2' } })

    expect(wrapper.find('[data-testid="pay-method-pm_2"]').attributes('data-state')).toBe('checked')
    expect(wrapper.find('[data-testid="pay-method-pm_1"]').attributes('data-state')).toBe('unchecked')
  })

  it('emits the card that was clicked', async () => {
    const wrapper = mountClient(PayMethodSelector, { props: { savedMethods: cards, modelValue: 'pm_1' } })

    await wrapper.find('[data-testid="pay-method-pm_2-input"]').setValue(true)

    expect(wrapper.emitted('update:modelValue')).toEqual([['pm_2']])
  })
})
