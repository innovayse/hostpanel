/**
 * Where a placed order sends the payer, as reported by `stores/checkout.ts` `placeOrder`.
 *
 * A closed set the page branches on, so it is an enum rather than a pair of string literals.
 */
export enum CheckoutDestinationKind {
  /** A page of this site; the page navigates to it with the router. */
  Route = 'route',
  /** The gateway's hosted step (a bank's payment page); the page hands the browser to it as the backend described. */
  Gateway = 'gateway'
}
