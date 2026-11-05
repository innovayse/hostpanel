/**
 * Unit tests for `utils/paymentModules.ts`: which module ids are hosted gateways.
 *
 * @module tests/utils/paymentModules.test
 */

import { describe, expect, it } from 'vitest'
import { PaymentModule } from '~/types/paymentmodule'
import { isHostedGatewayModule } from '~/utils/paymentModules'

describe('isHostedGatewayModule', () => {
  it('reads a plugin-backed module as a hosted gateway', () => {
    expect(isHostedGatewayModule('hosted_gateway')).toBe(true)
    expect(isHostedGatewayModule('otherbank')).toBe(true)
  })

  it('does not read the built-in bank transfer as a hosted gateway', () => {
    expect(isHostedGatewayModule(PaymentModule.BankTransfer)).toBe(false)
  })

  it('does not read an empty choice as a hosted gateway', () => {
    expect(isHostedGatewayModule('')).toBe(false)
  })
})
