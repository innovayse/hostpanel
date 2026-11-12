/**
 * Tests for `utils/paymentMethodOptions.ts`: the label and value of a gateway option.
 */
import { describe, expect, it } from 'vitest'
import { toPaymentMethodOptions } from '~/utils/paymentMethodOptions'

describe('toPaymentMethodOptions', () => {
  it('labels a gateway with its display name and sends its module name', () => {
    const [option] = toPaymentMethodOptions([{ module: 'card_gateway', displayName: 'Card payment', supportsStoredCredentials: false }])

    expect(option).toEqual({ label: 'Card payment', value: 'card_gateway' })
  })

  it('falls back to the module name when the gateway has no display name', () => {
    const [option] = toPaymentMethodOptions([{ module: 'bank_transfer', displayName: '', supportsStoredCredentials: false }])

    expect(option).toEqual({ label: 'bank_transfer', value: 'bank_transfer' })
  })

  it.each([undefined, null])('reads %s as no methods', (value) => {
    expect(toPaymentMethodOptions(value)).toEqual([])
  })
})
