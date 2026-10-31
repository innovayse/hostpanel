/**
 * Turning the API's saved payment method into the shape the client renders.
 *
 * The API sends camelCase fields (`brand`, `last4`, `expiryMonth` and the rest); the screens read
 * {@link PaymentMethod}. One mapper at the API boundary keeps every consumer on one shape.
 *
 * @module utils/paymentMethodMapper
 */

import { PaymentMethodType } from '~/types/payment'
import type { PaymentMethod, PaymentMethodWire } from '~/types/payment'

/** Card networks whose display name is not just the capitalised gateway code. */
const BRAND_NAMES: Record<string, string> = {
  visa: 'Visa',
  mastercard: 'Mastercard',
  amex: 'American Express',
  discover: 'Discover',
  diners: 'Diners Club',
  jcb: 'JCB',
  unionpay: 'UnionPay'
}

/** The masking prefix shown before the last four digits. */
const MASK = '••••'

/**
 * Display name of a card network.
 *
 * @param brand - The gateway's network code (e.g. "visa").
 * @returns The name to show; an unknown code is capitalised.
 */
export const brandName = (brand: string): string =>
  BRAND_NAMES[brand.toLowerCase()] ?? brand.charAt(0).toUpperCase() + brand.slice(1)

/**
 * Formats an expiry as MM/YY.
 *
 * @param month - Expiry month (1-12).
 * @param year - Expiry year (4 digits).
 * @returns For example `08/28`, or null when either part is missing.
 */
export const formatSavedExpiry = (month?: number | null, year?: number | null): string | null => {
  if (!month || !year) {
    return null
  }
  return `${String(month).padStart(2, '0')}/${String(year % 100).padStart(2, '0')}`
}

/**
 * Maps one wire payment method to the client shape.
 *
 * @param wire - The method as the API sends it.
 * @returns The method with a masked-number label, formatted expiry and default flag.
 */
export const toPaymentMethod = (wire: PaymentMethodWire): PaymentMethod => {
  const isBank = wire.type === PaymentMethodType.BankAccount
  const name = wire.brand ? (isBank ? wire.brand : brandName(wire.brand)) : ''
  const masked = wire.last4 ? `${MASK} ${wire.last4}` : ''
  return {
    id: wire.id,
    type: wire.type,
    description: [name, masked].filter(Boolean).join(' '),
    gatewayName: wire.type,
    cardLastFour: wire.last4 ?? null,
    cardExpiry: formatSavedExpiry(wire.expiryMonth, wire.expiryYear),
    cardType: isBank ? null : (wire.brand ?? null),
    bankName: isBank ? (wire.brand ?? null) : null,
    isDefault: wire.isDefault,
    autoCharge: wire.autoCharge === true
  }
}
