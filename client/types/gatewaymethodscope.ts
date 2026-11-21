/**
 * What a gateway list is asked for: the products of the cart, or the invoice, being paid.
 *
 * @module types/gatewaymethodscope
 */

/** Names what is being paid for, so the list can leave out the methods its product groups do not allow. */
export interface GatewayMethodScope {
  /** Products of the cart being checked out; their groups may restrict the payment methods. */
  productIds?: number[]
  /** The signed-in payer's own invoice being paid; the groups of its services may restrict the payment methods. */
  invoiceId?: string
}
