/**
 * Unit tests for `constants/paymentGatewayKind.ts`: every module maps to a kind, every kind has
 * an icon, and an unknown module is the generic kind.
 */
import { describe, expect, it } from 'vitest'
import {
  PAYMENT_GATEWAY_KIND_DESCRIPTION_KEY,
  PAYMENT_GATEWAY_KIND_ICON,
  paymentGatewayKind
} from '~/constants/paymentGatewayKind'
import { PaymentGatewayKind } from '~/types/paymentgatewaykind'
import { PaymentModule } from '~/types/paymentmodule'

describe('paymentGatewayKind', () => {
  it('maps the manual transfer to the bank transfer kind', () => {
    expect(paymentGatewayKind(PaymentModule.BankTransfer)).toBe(PaymentGatewayKind.BankTransfer)
  })

  it('reads an unknown module as the generic kind', () => {
    expect(paymentGatewayKind('crypto')).toBe(PaymentGatewayKind.Other)
  })

  it('gives every kind an icon, and a description to every kind but the generic one', () => {
    for (const kind of Object.values(PaymentGatewayKind)) {
      expect(PAYMENT_GATEWAY_KIND_ICON[kind]).toBeTruthy()
    }
    expect(PAYMENT_GATEWAY_KIND_DESCRIPTION_KEY[PaymentGatewayKind.Other]).toBeNull()
    expect(PAYMENT_GATEWAY_KIND_DESCRIPTION_KEY[PaymentGatewayKind.BankTransfer]).toBe('checkout.gatewayKinds.bankTransfer')
  })
})
