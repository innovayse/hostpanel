/**
 * What the backend answers a hosted-gateway payment start with.
 *
 * @module types/gatewaystart
 */

import type { GatewaySessionKind } from '~/types/gatewaysessionkind'
import type { PaymentFormPost } from '~/types/paymentformpost'

/** Where and how the browser goes to pay on the gateway's page. */
export interface GatewayStart {
  /** The gateway page: the page a redirect opens, the page of a browser step, or the address a form is posted to. */
  redirectUrl: string
  /** How the browser is taken to the page. */
  kind: GatewaySessionKind
  /** The form to post; set exactly when {@link kind} is `GatewaySessionKind.FormPost`, otherwise null. */
  formPost: PaymentFormPost | null
}
