/** One payment gateway an order or invoice may be paid through. */
export interface GatewayMethod {
  /** Gateway module name, e.g. "stripe" — what the API expects back. */
  module: string
  /** Gateway name as configured for display. */
  displayname: string
}
