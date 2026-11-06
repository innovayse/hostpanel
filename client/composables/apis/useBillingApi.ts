/**
 * Endpoints for money: invoices under `/api/portal/client/invoices`, the gateways an invoice
 * or order may be paid through, and ordering under `/api/portal/order`.
 *
 * Every function returns a plain `Promise`. Nothing here is cached or held.
 *
 * @module composables/apis/useBillingApi
 */

import { apiFetch } from '~/composables/useApi'
import type { BankTransferDetails } from '~/types/banktransferdetails'
import type { ClientInvoice } from '~/types/clientinvoice'
import type { GatewayCompletion } from '~/types/gatewaycompletion'
import type { GatewayMethod } from '~/types/gatewaymethod'
import type { PlaceOrderResult } from '~/types/placeorderresult'
import { PaymentTarget } from '~/types/paymenttarget'
import { HttpMethod } from '~/types/httpmethod'

/**
 * The billing surface, one function per endpoint.
 *
 * @returns The billing endpoint functions.
 */
export const useBillingApi = () => {
  /**
   * Reads one invoice with its line items.
   *
   * @param id - Invoice id.
   * @returns The invoice.
   * @throws Whatever `apiFetch` throws.
   */
  const fetchInvoice = (id: string): Promise<ClientInvoice> =>
    apiFetch<ClientInvoice>(`/api/portal/client/invoices/${id}`)

  /**
   * Reads the gateways an order may be paid through.
   *
   * Note the path: this is under `/api/portal/order`, not `/api/portal/client`, because the
   * list is the same whether or not anybody is signed in.
   *
   * @returns Every gateway the order can be paid through.
   * @throws Whatever `apiFetch` throws.
   */
  const fetchGatewayMethods = (): Promise<GatewayMethod[]> =>
    apiFetch<GatewayMethod[]>('/api/portal/order/payment-methods')

  /**
   * Places an order, creating the order and its invoice on the backend.
   *
   * @param order - Cart contents, billing details and chosen payment method.
   * @returns The new order's id and number, its invoice id, the payment token that authorises paying for it,
   * and the billed total; a total of zero means the backend already settled and fulfilled the order.
   * @throws Whatever `apiFetch` throws.
   */
  const createOrder = (order: Record<string, unknown>): Promise<PlaceOrderResult> =>
    apiFetch<PlaceOrderResult>('/api/portal/order/create', {
      method: HttpMethod.Post,
      body: order
    })

  /**
   * Registers a hosted-gateway payment for an order and asks where to send the payer.
   *
   * @param orderId - Order to pay.
   * @param module - Gateway module to pay through.
   * @param returnUrl - Absolute URL the bank returns the payer to.
   * @param paymentToken - The order's payment token, returned when it was placed. Checkout is open
   * to guests, so this is what authorises paying for an order, in place of a credential.
   * @returns The bank's URL to hand the browser to.
   * @throws Whatever `apiFetch` throws.
   */
  const startOrderGatewayPayment = (
    orderId: string | number,
    module: string,
    returnUrl: string,
    paymentToken: string
  ): Promise<{ redirectUrl: string }> =>
    apiFetch<{ redirectUrl: string }>(`/api/portal/order/${orderId}/gateway-payment/start`, {
      method: HttpMethod.Post,
      body: { module, returnUrl, paymentToken }
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
      { method: HttpMethod.Post, body: { module, returnUrl } }
    )

  /**
   * Asks the backend to settle a hosted-gateway payment with the bank, after the payer has
   * been returned to the site.
   *
   * @param target - Whether the payment was raised against an order or an invoice.
   * @param id - The order or invoice id.
   * @param paymentToken - The order's payment token. Required when `target` is `PaymentTarget.Order`; ignored
   * for an invoice, which is authorised by the caller's own credential.
   * @returns What the bank said the payment did.
   * @throws Whatever `apiFetch` throws: the caller must not read a failure here as a
   * declined payment; the money may well have moved.
   */
  const completeGatewayPayment = (
    target: PaymentTarget,
    id: string | number,
    paymentToken?: string
  ): Promise<GatewayCompletion> =>
    apiFetch<GatewayCompletion>(
      target === PaymentTarget.Order
        ? `/api/portal/order/${id}/gateway-payment/complete`
        : `/api/portal/client/invoices/${id}/gateway-payment/complete`,
      // Only the order endpoint reads a token; the invoice one is behind the client's own
      // credential and is sent an empty body, exactly as before.
      { method: HttpMethod.Post, body: target === PaymentTarget.Order ? { paymentToken } : {} }
    )

  /**
   * Chooses manual bank transfer for an invoice: the invoice waits for staff to confirm the money, and the answer carries
   * the bank details and the reference to write on the transfer. Choosing it again answers the same details.
   *
   * @param invoiceId - Invoice to pay by bank transfer.
   * @returns The bank details and the reference.
   * @throws Whatever `apiFetch` throws.
   */
  const selectBankTransfer = (invoiceId: string | number): Promise<BankTransferDetails> =>
    apiFetch<BankTransferDetails>(`/api/portal/client/invoices/${invoiceId}/bank-transfer`, {
      method: HttpMethod.Post,
      body: {}
    })

  /**
   * Reads the bank details and the reference of the transfer waiting on an invoice.
   *
   * @param invoiceId - Invoice the transfer waits on.
   * @returns The bank details and the reference.
   * @throws Whatever `apiFetch` throws; a refusal when no transfer waits.
   */
  const fetchBankTransfer = (invoiceId: string | number): Promise<BankTransferDetails> =>
    apiFetch<BankTransferDetails>(`/api/portal/client/invoices/${invoiceId}/bank-transfer`)

  /**
   * Reads an invoice as a PDF.
   *
   * No `responseType` is set: ofetch picks it from the answer, so the PDF arrives as a `Blob`
   * while a refusal still arrives as parsed JSON and `apiErrorMessage` can read its sentence.
   *
   * @param id - Invoice id, encoded so it stays one path segment.
   * @returns The PDF.
   * @throws Whatever `apiFetch` throws.
   */
  const fetchInvoicePdf = (id: string): Promise<Blob> =>
    apiFetch<Blob>(`/api/portal/client/invoices/${encodeURIComponent(id)}/pdf`)

  return {
    fetchInvoice,
    fetchInvoicePdf,
    fetchGatewayMethods,
    createOrder,
    startOrderGatewayPayment,
    startInvoiceGatewayPayment,
    selectBankTransfer,
    fetchBankTransfer,
    completeGatewayPayment
  }
}
