/**
 * How the payer's browser is taken to a gateway's hosted step, as the backend names it.
 *
 * A closed set the hand-off branches on, so it is an enum. The API sends these exact strings, so a
 * response's `kind` is read as this enum without any mapping.
 *
 * @module types/gatewaysessionkind
 */
export enum GatewaySessionKind {
  /** The browser is sent to the address with a plain GET. */
  Redirect = 'Redirect',
  /** The browser posts the described form to the gateway's page. */
  FormPost = 'FormPost',
  /** The browser is sent to a step the payer finishes there; the portal's return page then completes the payment. */
  BrowserStep = 'BrowserStep'
}
