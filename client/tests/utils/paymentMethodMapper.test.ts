/**
 * Unit tests for the payment method mapper: wire fields to the shape the screens render.
 */
import { describe, expect, it } from 'vitest'
import { PaymentMethodType } from '~/types/payment'
import { brandName, formatSavedExpiry, toPaymentMethod } from '~/utils/paymentMethodMapper'

describe('toPaymentMethod', () => {
  it('maps a card to a masked label, formatted expiry and the default flag', () => {
    const method = toPaymentMethod({
      gatewayModuleId: 'stripe', id: 'pm_1', brand: 'visa', last4: '4242', expiryMonth: 8, expiryYear: 2030, isDefault: true
    })

    expect(method).toMatchObject({
      gatewayModuleId: 'stripe', id: 'pm_1', type: PaymentMethodType.Card, gatewayName: 'stripe',
      description: 'Visa •••• 4242', cardLastFour: '4242', cardExpiry: '08/30', cardType: 'visa', isDefault: true
    })
  })

  it('carries the agreement to automatic charges, and reads a missing flag as off', () => {
    const agreed = toPaymentMethod({ gatewayModuleId: 'stripe', id: 'pm_3', brand: 'visa', last4: '4242', isDefault: true, autoCharge: true })
    const plain = toPaymentMethod({ gatewayModuleId: 'stripe', id: 'pm_4', brand: 'visa', last4: '1881', isDefault: false })

    expect(agreed.autoCharge).toBe(true)
    expect(plain.autoCharge).toBe(false)
  })

  it('labels a method the gateway reports no brand or digits for with an empty description', () => {
    const method = toPaymentMethod({ gatewayModuleId: 'inecobank', id: 'tok_1', brand: null, last4: null, isDefault: false })

    expect(method).toMatchObject({ gatewayModuleId: 'inecobank', description: '', cardType: null, cardLastFour: null, cardExpiry: null })
  })
})

describe('brandName', () => {
  it('names known networks and capitalises unknown ones', () => {
    expect(brandName('mastercard')).toBe('Mastercard')
    expect(brandName('amex')).toBe('American Express')
    expect(brandName('elo')).toBe('Elo')
  })
})

describe('formatSavedExpiry', () => {
  it('pads the month and shortens the year, or is null when a part is missing', () => {
    expect(formatSavedExpiry(3, 2027)).toBe('03/27')
    expect(formatSavedExpiry(null, 2027)).toBeNull()
  })
})
