/**
 * One name and value of the query the payer came back from the gateway with.
 *
 * @module types/paymentreturnparameter
 */

/** A parameter of the gateway's return request, handed to the backend so the plugin can complete the payment. */
export interface PaymentReturnParameter {
  /** The parameter name. */
  name: string
  /** The parameter value; empty when the address gave the name none. */
  value: string
}
