/**
 * Helpers that classify a payment module id.
 *
 * @module utils/paymentModules
 */

import { PaymentModule } from '~/types/paymentmodule'

/**
 * Whether a payment module is a hosted gateway, which hands the browser off to the bank.
 *
 * Every plugin-backed module is one: anything that is not the built-in bank transfer. Branching on that rather than on a specific plugin id keeps a second
 * gateway plugin from being silently ignored.
 *
 * @param module - The module id as the API sent it; empty when nothing is chosen.
 * @returns True for a plugin-backed gateway, false for the built-ins and for an empty id.
 */
export const isHostedGatewayModule = (module: string): boolean =>
  module !== ''
    && module !== PaymentModule.BankTransfer
