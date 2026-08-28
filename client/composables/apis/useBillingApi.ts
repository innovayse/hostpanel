/**
 * Endpoints for money — invoices under `/api/portal/client/invoices`, the saved payment
 * methods at `/api/portal/client/payment-methods`, and ordering under `/api/portal/order`.
 *
 * Two naming shapes, as elsewhere in `composables/apis/`: `load*` returns the `useApi()`
 * handle for a server-rendered read, everything else returns a plain `Promise`. Nothing here
 * is cached or held.
 *
 * @module composables/apis/useBillingApi
 */

import { apiFetch, useApi } from '~/composables/useApi'
import type { ClientInvoice } from '~/types/clientinvoice'
import type { GatewayMethod } from '~/types/gatewaymethod'
import type { PaymentMethod } from '~/types/payment'

/**
 * The billing surface, one function per endpoint.
 *
 * @returns The billing endpoint functions.
 */
export function useBillingApi() {
  /**
   * Reads one invoice with its line items.
   *
   * @param id - Invoice id; a getter so the page re-reads when the route changes.
   * @returns The `useApi()` handle for the invoice.
   */
  const loadInvoice = (id: () => string) =>
    useApi<ClientInvoice>(() => `/api/portal/client/invoices/${id()}`)

  /**
   * Reads the client's saved cards and bank accounts.
   *
   * @returns The `useApi()` handle for the saved payment methods, defaulting to an empty list.
   */
  const loadPaymentMethods = () =>
    useApi<PaymentMethod[]>('/api/portal/client/payment-methods', { default: () => [] })

  /**
   * Reads the client's saved cards and bank accounts as a one-shot call.
   *
   * @returns Every saved payment method on the account.
   * @throws Whatever `apiFetch` throws.
   */
  const fetchPaymentMethods = (): Promise<PaymentMethod[]> =>
    apiFetch<PaymentMethod[]>('/api/portal/client/payment-methods')

  /**
   * Reads the gateways an order may be paid through.
   *
   * Note the path: this is under `/api/portal/order`, not `/api/portal/client`, because the
   * list is the same whether or not anybody is signed in.
   *
   * @returns The `useApi()` handle for the gateway list, defaulting to an empty list.
   */
  const loadGatewayMethods = () =>
    useApi<GatewayMethod[]>('/api/portal/order/payment-methods', { default: () => [] })

  /**
   * Pays an invoice with a saved payment method.
   *
   * @param invoiceId - Invoice to pay.
   * @param payMethodId - Saved payment method to charge.
   * @returns Whatever the endpoint answers with.
   * @throws Whatever `apiFetch` throws — a declined card included.
   */
  const payInvoice = (invoiceId: string, payMethodId: string | number): Promise<unknown> =>
    apiFetch(`/api/portal/client/invoices/${invoiceId}/pay`, {
      method: 'POST',
      body: { paymethodid: payMethodId }
    })

  /**
   * Updates a saved payment method's details.
   *
   * @param id - Payment method to update.
   * @param body - The fields to change.
   * @returns Whatever the endpoint answers with.
   * @throws Whatever `apiFetch` throws.
   */
  const updatePaymentMethod = (id: string, body: Record<string, unknown>): Promise<unknown> =>
    apiFetch(`/api/portal/client/payment-methods/${id}`, { method: 'PUT', body })

  /**
   * Makes a saved payment method the account default.
   *
   * @param id - Payment method to promote.
   * @returns Whatever the endpoint answers with.
   * @throws Whatever `apiFetch` throws.
   */
  const setDefaultPaymentMethod = (id: string): Promise<unknown> =>
    apiFetch(`/api/portal/client/payment-methods/${id}`, {
      method: 'PUT',
      body: { set_as_default: true }
    })

  /**
   * Removes a saved payment method.
   *
   * @param id - Payment method to delete.
   * @returns Whatever the endpoint answers with.
   * @throws Whatever `apiFetch` throws.
   */
  const removePaymentMethod = (id: string): Promise<unknown> =>
    apiFetch(`/api/portal/client/payment-methods/${id}`, { method: 'DELETE' })

  /**
   * Places an order, creating the order and its invoice on the backend.
   *
   * @param order - Cart contents, billing details and chosen payment method.
   * @returns The new order and invoice ids.
   * @throws Whatever `apiFetch` throws.
   */
  const createOrder = (order: Record<string, unknown>): Promise<{ orderId: number, invoiceId: number }> =>
    apiFetch<{ orderId: number, invoiceId: number }>('/api/portal/order/create', {
      method: 'POST',
      body: order
    })

  /**
   * Opens a Stripe PaymentIntent for an order.
   *
   * @param orderId - Order to charge.
   * @returns The client secret Stripe.js needs to confirm the card.
   * @throws Whatever `apiFetch` throws.
   */
  const createOrderPaymentIntent = (orderId: string | number): Promise<{ clientSecret: string }> =>
    apiFetch<{ clientSecret: string }>(`/api/portal/order/${orderId}/create-payment-intent`, {
      method: 'POST'
    })

  /**
   * Registers a hosted-gateway payment for an order and asks where to send the payer.
   *
   * @param orderId - Order to pay.
   * @param module - Gateway module to pay through.
   * @param returnUrl - Absolute URL the bank returns the payer to.
   * @returns The bank's URL to hand the browser to.
   * @throws Whatever `apiFetch` throws.
   */
  const startOrderGatewayPayment = (
    orderId: string | number,
    module: string,
    returnUrl: string
  ): Promise<{ redirectUrl: string }> =>
    apiFetch<{ redirectUrl: string }>(`/api/portal/order/${orderId}/gateway-payment/start`, {
      method: 'POST',
      body: { module, returnUrl }
    })

  /**
   * Registers a hosted-gateway payment for an invoice and asks where to send the payer.
   *
   * @param invoiceId - Invoice to pay.
   * @param module - Gateway module to pay through.
   * @param returnUrl - Absolute URL the bank returns the payer to.
   * @returns The bank's URL to hand the browser to.
   * @throws Whatever `apiFetch` throws.
   */
  const startInvoiceGatewayPayment = (
    invoiceId: string | number,
    module: string,
    returnUrl: string
  ): Promise<{ redirectUrl: string }> =>
    apiFetch<{ redirectUrl: string }>(
      `/api/portal/client/invoices/${invoiceId}/gateway-payment/start`,
      { method: 'POST', body: { module, returnUrl } }
    )

  /**
   * Asks the backend to settle a hosted-gateway payment with the bank, after the payer has
   * been returned to the site.
   *
   * @param target - Whether the payment was raised against an order or an invoice.
   * @param id - The order or invoice id.
   * @returns What the bank said the payment did.
   * @throws Whatever `apiFetch` throws — the caller must not read a failure here as a
   * declined payment; the money may well have moved.
   */
  const completeGatewayPayment = (
    target: 'order' | 'invoice',
    id: string | number
  ): Promise<{ state: 'paid' | 'pending' | 'declined' }> =>
    apiFetch<{ state: 'paid' | 'pending' | 'declined' }>(
      target === 'order'
        ? `/api/portal/order/${id}/gateway-payment/complete`
        : `/api/portal/client/invoices/${id}/gateway-payment/complete`,
      { method: 'POST' }
    )

  /**
   * Confirms a card payment against an order after the gateway has authorised it.
   *
   * @param orderId - Order the payment belongs to.
   * @param body - Whatever the gateway handed back for confirmation.
   * @returns Whatever the endpoint answers with.
   * @throws Whatever `apiFetch` throws.
   */
  const confirmOrderPayment = (orderId: string | number, body: Record<string, unknown>): Promise<unknown> =>
    apiFetch(`/api/portal/order/${orderId}/confirm-payment`, { method: 'POST', body })

  return {
    loadInvoice,
    loadPaymentMethods,
    fetchPaymentMethods,
    loadGatewayMethods,
    createOrder,
    createOrderPaymentIntent,
    startOrderGatewayPayment,
    startInvoiceGatewayPayment,
    completeGatewayPayment,
    payInvoice,
    updatePaymentMethod,
    setDefaultPaymentMethod,
    removePaymentMethod,
    confirmOrderPayment
  }
}
