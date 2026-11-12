/**
 * Where to send the payer once an order has been placed.
 *
 * @module types/checkoutdestination
 */

import type { CheckoutDestinationKind } from '~/types/checkoutdestinationkind'
import type { GatewayStart } from '~/types/gatewaystart'

/** A page of this site. */
export interface CheckoutRouteDestination {
  /** Marks a page of this site. */
  kind: CheckoutDestinationKind.Route
  /** The localised path to go to. */
  target: string
}

/** The gateway's hosted step. */
export interface CheckoutGatewayDestination {
  /** Marks the gateway's hosted step. */
  kind: CheckoutDestinationKind.Gateway
  /** What to hand the browser to: a redirect, an auto submitted form or a browser step. */
  start: GatewayStart
}

/** Where to send the payer once an order has been placed. */
export type CheckoutDestination = CheckoutRouteDestination | CheckoutGatewayDestination
