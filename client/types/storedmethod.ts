/**
 * A payment method stored for later charges.
 *
 * @module types/storedmethod
 */

/** One stored method as the list answers with it; the consent id is what revoking and defaulting name. */
export interface StoredMethod {
  /** Id of the charge consent the method is bound to. */
  consentId: number
  /** Gateway module the method is stored at. */
  gatewayModuleId: string
  /** Whether the gateway could report the method's details; when false, brand, last four and expiry are empty. */
  detailsAvailable: boolean
  /** Card brand, when the gateway reports one. */
  brand: string | null
  /** Last four digits, when the gateway reports them. */
  last4: string | null
  /** Expiry month, 1 to 12; 0 when the details are unavailable. */
  expiryMonth: number
  /** Four-digit expiry year; 0 when the details are unavailable. */
  expiryYear: number
  /** Whether the method is the account's default. */
  isDefault: boolean
}
