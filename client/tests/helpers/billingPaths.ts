/**
 * The BFF paths of the billing endpoints a payment or checkout screen reaches (the invoice, its
 * bank transfer, placing an order, starting and settling a hosted-gateway payment), so a test that answers them
 * from a stub BFF route table names an endpoint instead of spelling out the URL.
 *
 * @module tests/helpers/billingPaths
 */

/** The BFF prefix of the order surface: placing an order, the gateways on offer and an order's hosted payment. */
const ORDER_ROOT = '/api/portal/order'

/** The billing endpoints that do not take an id. */
export const BillingEndpoint = {
  OrderCreate: `${ORDER_ROOT}/create`,
  OrderPaymentMethods: `${ORDER_ROOT}/payment-methods`,
} as const

/**
 * The path of one order.
 *
 * @param orderId - The order id.
 * @returns The BFF path every endpoint of that order starts with.
 */
export const orderPath = (orderId: number | string): string => `${ORDER_ROOT}/${orderId}`

/**
 * The path of one invoice.
 *
 * @param invoiceId - The invoice id.
 * @returns The BFF path that reads it.
 */
export const invoicePath = (invoiceId: number | string): string => `/api/portal/client/invoices/${invoiceId}`

/**
 * The path of one invoice's bank transfer.
 *
 * @param invoiceId - The invoice id.
 * @returns The BFF path that reads the bank details or chooses the transfer.
 */
export const bankTransferPath = (invoiceId: number | string): string => `${invoicePath(invoiceId)}/bank-transfer`

/**
 * The path that settles a hosted-gateway payment of an order.
 *
 * @param orderId - The order id.
 * @returns The BFF path.
 */
export const orderPaymentCompletePath = (orderId: number | string): string => `${orderPath(orderId)}/gateway-payment/complete`

/**
 * The path that starts a hosted-gateway payment of an order.
 *
 * @param orderId - The order id.
 * @returns The BFF path.
 */
export const orderPaymentStartPath = (orderId: number | string): string => `${orderPath(orderId)}/gateway-payment/start`

/**
 * The path that settles a hosted-gateway payment of an invoice.
 *
 * @param invoiceId - The invoice id.
 * @returns The BFF path.
 */
export const invoicePaymentCompletePath = (invoiceId: number | string): string => `${invoicePath(invoiceId)}/gateway-payment/complete`

/**
 * The path that starts a hosted-gateway payment of an invoice.
 *
 * @param invoiceId - The invoice id.
 * @returns The BFF path.
 */
export const invoicePaymentStartPath = (invoiceId: number | string): string => `${invoicePath(invoiceId)}/gateway-payment/start`
