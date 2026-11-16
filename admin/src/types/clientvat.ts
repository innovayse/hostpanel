/**
 * The VAT number check of a client: the verdict, as the client detail carries it.
 */

/** The verdict of a completed VAT number check, exactly as the API sends it. */
export enum ClientVatStatus {
  /** The service confirmed the number is registered. */
  Valid = 'Valid',
  /** The service answered and the number is not registered. */
  Invalid = 'Invalid',
}

/** The last completed check of a client's VAT number (their tax ID). */
export interface ClientVat {
  /** The verdict; null when the number was never checked or was changed since. */
  status: ClientVatStatus | null
  /** When the check was made (ISO date-time), null while there is no verdict. */
  checkedAt: string | null
  /** The registered name the service returned for a valid number, or null. */
  name: string | null
  /** The registered address the service returned for a valid number, or null. */
  address: string | null
}
