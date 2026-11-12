/**
 * A hosted payment page that is opened with a form POST.
 *
 * @module types/paymentformpost
 */

import type { PaymentFormField } from '~/types/paymentformfield'

/** The form the browser posts to a gateway's hosted page. */
export interface PaymentFormPost {
  /** The absolute `https` address the form is posted to. */
  action: string
  /** The fields, in the order they are posted; a name may repeat. */
  fields: PaymentFormField[]
}
