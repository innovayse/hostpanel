/**
 * One field of the form a gateway's hosted page is opened with.
 *
 * @module types/paymentformfield
 */

/** One field of a form post to a gateway. */
export interface PaymentFormField {
  /** The field name; never empty. */
  name: string
  /** The field value. */
  value: string
}
