/** One payment gateway an order or invoice may be paid through. */
export interface GatewayMethod {
  /** Gateway module name, what the API expects back. */
  module: string
  /** Gateway name as configured for display. */
  displayName: string
  /** Whether the gateway can keep the payment method entered in a payment for later automatic charges. */
  supportsStoredCredentials: boolean
}
