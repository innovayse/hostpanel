/**
 * Unit tests for the payment method mapper: wire fields to the shape the screens render.
 */
import { describe, expect, it } from 'vitest'
import { PaymentMethodType } from '~/types/payment'
import { brandName, formatSavedExpiry, toPaymentMethod } from '~/utils/paymentMethodMapper'

describe('toPaymentMethod', () => {
  it('maps a card to a masked label, formatted expiry and the default flag', () => {
    const method = toPaymentMethod({
      id: 'pm_1', type: PaymentMethodType.Card, brand: 'visa', last4: '4242', expiryMonth: 8, expiryYear: 2030, isDefault: true
    })

    expect(method).toMatchObject({
      id: 'pm_1', description: 'Visa •••• 4242', cardLastFour: '4242', cardExpiry: '08/30', cardType: 'visa', bankName: null, isDefault: true
    })
  })

  it('carries the agreement to automatic charges, and reads a missing flag as off', () => {
    const agreed = toPaymentMethod({ id: 'pm_3', type: PaymentMethodType.Card, brand: 'visa', last4: '4242', isDefault: true, autoCharge: true })
    const plain = toPaymentMethod({ id: 'pm_4', type: PaymentMethodType.Card, brand: 'visa', last4: '1881', isDefault: false })

    expect(agreed.autoCharge).toBe(true)
    expect(plain.autoCharge).toBe(false)
  })

  it('reads a bank account brand as the bank name', () => {
    const method = toPaymentMethod({ id: 'pm_2', type: PaymentMethodType.BankAccount, brand: 'First Bank', last4: '6789', isDefault: false })

    expect(method).toMatchObject({ description: 'First Bank •••• 6789', bankName: 'First Bank', cardType: null, cardExpiry: null })
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
