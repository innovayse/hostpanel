/**
 * What a checkout page hands `stores/checkout.ts` `placeOrder`; the page owns the form and the card element.
 *
 * @module types/placeorderinput
 */

import type { ConfirmedPayment } from '~/types/confirmedpayment'
import type { GuestDetails } from '~/types/guestdetails'

/** What a checkout page hands `stores/checkout.ts` `placeOrder`; the page owns the form and the card element. */
export interface PlaceOrderInput {
  /** The guest's details; ignored when the visitor is signed in. */
  guest: GuestDetails
  /**
   * Confirms a card payment with the payment provider's card element.
   *
   * @param clientSecret - The secret the order's payment intent was opened with.
   * @returns The confirmed payment's id.
   * @throws When the card element is not ready or the provider refuses the payment.
   */
  confirmCard: (clientSecret: string) => Promise<ConfirmedPayment>
  /**
   * Asks the payer whether to pay a total that differs from the one the cart showed.
   *
   * @param shown - The total the cart showed, formatted.
   * @param charged - The total the backend billed, formatted.
   * @returns True when the payer agrees to pay the billed total.
   */
  confirmTotalChange: (shown: string, charged: string) => boolean
  /**
   * Turns a site path into the visitor's localised path; a store has no router.
   *
   * @param path - A path such as `/client/invoices`, optionally with a query string.
   * @returns The same path under the active language.
   */
  localizePath: (path: string) => string
}
