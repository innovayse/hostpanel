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
import type { ClientCredit } from '~/types/clientcredit'
import type { ClientCreditNote } from '~/types/clientcreditnote'
import type { ClientInvoice } from '~/types/clientinvoice'
import type { ConsentTextVersion } from '~/types/consenttextversion'
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
   * @param savePaymentMethod - Whether the payer asked for the method they enter in this payment to be saved for
   * automatic renewals; the gateway then keeps it within the same payment. Leave out for a plain payment.
   * @param consentTextVersion - The wording the payer agreed to when they ticked the box; the server records the
   * consent from this same request. Sent with `savePaymentMethod` only.
   * @returns The bank's URL to hand the browser to.
   * @throws Whatever `apiFetch` throws.
   */
  const startOrderGatewayPayment = (
    orderId: string | number,
    module: string,
    returnUrl: string,
    paymentToken: string,
    savePaymentMethod?: boolean,
    consentTextVersion?: ConsentTextVersion
  ): Promise<{ redirectUrl: string }> =>
    apiFetch<{ redirectUrl: string }>(`/api/portal/order/${orderId}/gateway-payment/start`, {
      method: HttpMethod.Post,
      body: { module, returnUrl, paymentToken, savePaymentMethod, consentTextVersion }
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
   * Reads the signed-in client's own credit: the balance, the limits of one top-up and a page of the history.
   *
   * @param page - 1-based page of the history.
   * @param pageSize - History lines per page.
   * @returns The credit.
   * @throws Whatever `apiFetch` throws.
   */
  const fetchCredit = (page = 1, pageSize = 25): Promise<ClientCredit> =>
    apiFetch<ClientCredit>('/api/portal/client/credit', { query: { page, pageSize } })

  /**
   * Raises the top-up invoice the client pays to add funds. The balance rises when that invoice is paid, through the same
   * payment pages as any other invoice.
   *
   * @param amount - The amount to add, in the client's billing currency.
   * @returns The id of the new top-up invoice.
   * @throws Whatever `apiFetch` throws; a refusal when the amount is outside the limits.
   */
  const createCreditTopUp = (amount: number): Promise<number> =>
    apiFetch<number>('/api/portal/client/credit/top-ups', { method: HttpMethod.Post, body: { amount } })

  /**
   * Pays part or all of an invoice from the client's credit balance.
   *
   * @param invoiceId - Invoice to apply credit to.
   * @param amount - The credit to apply, in the invoice currency.
   * @returns Nothing.
   * @throws Whatever `apiFetch` throws; a refusal when the balance is short or the invoice owes less.
   */
  const applyInvoiceCredit = (invoiceId: string | number, amount: number): Promise<undefined> =>
    apiFetch<undefined>(`/api/portal/client/invoices/${invoiceId}/credit`, { method: HttpMethod.Post, body: { amount } })

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

  /**
   * Reads the credit notes issued to the signed-in client, newest first. A credit note is not invoice credit: it is a numbered
   * document that reverses part of an invoice that was already paid.
   *
   * @returns The notes; drafts are never sent to a client.
   * @throws Whatever `apiFetch` throws.
   */
  const fetchCreditNotes = (): Promise<ClientCreditNote[]> =>
    apiFetch<ClientCreditNote[]>('/api/portal/client/credit-notes')

  /**
   * Reads one of the client's credit notes as a PDF, printed in the client's language.
   *
   * No `responseType` is set, for the reason {@link fetchInvoicePdf} gives: the PDF arrives as a `Blob` while a refusal still arrives
   * as parsed JSON.
   *
   * @param id - Credit note id.
   * @returns The PDF.
   * @throws Whatever `apiFetch` throws.
   */
  const fetchCreditNotePdf = (id: number): Promise<Blob> =>
    apiFetch<Blob>(`/api/portal/client/credit-notes/${encodeURIComponent(String(id))}/pdf`)

  return {
    fetchInvoice,
    fetchInvoicePdf,
    fetchGatewayMethods,
    createOrder,
    startOrderGatewayPayment,
    startInvoiceGatewayPayment,
    selectBankTransfer,
    fetchBankTransfer,
    fetchCredit,
    createCreditTopUp,
    applyInvoiceCredit,
    fetchCreditNotes,
    fetchCreditNotePdf,
    completeGatewayPayment
  }
}
