/**
 * A hosted payment page that is opened with a form POST.
 *
 * @module types/paymentformpost
 */

import type { PaymentFormField } from '~/types/paymentformfield'

/** The form the browser posts to a gateway's hosted page; the address it is posted to is the answer's `redirectUrl`. */
export interface PaymentFormPost {
  /** The fields, in the order they are posted; a name may repeat. */
  fields: PaymentFormField[]
}
