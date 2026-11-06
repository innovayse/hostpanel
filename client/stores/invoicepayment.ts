/**
 * Pinia store for one invoice: reading it, starting its hosted-gateway payment from
 * `pages/client/invoices/[id]/pay.vue`, and verifying that payment on `pages/payment/result.vue`.
 *
 * Owns the loaded invoice, the payment gateways (and whether reading them failed), the in-flight
 * flag and the refusal sentence, so the pages render them and never wrap an API call in their
 * own try/catch. Invoices are paid only through a hosted gateway; there are no saved methods here.
 * URLs and transport belong to {@link useBillingApi}.
 *
 * @module stores/invoicepayment
 */

import { defineStore } from 'pinia'
import { useBillingApi } from '~/composables/apis/useBillingApi'
import { apiErrorMessage } from '~/lib/errorHandler'
import { saveBlob } from '~/utils/saveBlob'
import type { BankTransferDetails } from '~/types/banktransferdetails'
import type { ClientInvoice } from '~/types/clientinvoice'
import type { GatewayMethod } from '~/types/gatewaymethod'
import { GatewayPaymentState } from '~/types/gatewaypaymentstate'
import { InvoiceStatus } from '~/types/invoicestatus'
import { PaymentModule } from '~/types/paymentmodule'
import type { PaymentTarget } from '~/types/paymenttarget'
import { isSafeRedirectUrl } from '~/utils/externalRedirect'
import { isHostedGatewayModule } from '~/utils/paymentModules'
import { translate } from '~/utils/translate'

/**
 * Payment state for the invoice payment page.
 *
 * @returns The payment state and the actions that change it.
 */
export const useInvoicePaymentStore = defineStore('invoicePayment', () => {
  const billing = useBillingApi()

  /** The invoice on screen; null until {@link loadInvoice} or after a failed read. */
  const invoice = ref<ClientInvoice | null>(null)

  /** Whether the invoice is being read. */
  const invoiceLoading = ref(false)

  /** The sentence for the last failed invoice read; empty when there is none to show. */
  const invoiceError = ref('')

  /** The gateways an invoice can be paid through; empty until {@link loadPaymentOptions}. */
  const gatewayMethods = ref<GatewayMethod[]>([])

  /** The sentence for a failed gateway-list read; empty when the list was read (even if it is empty). */
  const optionsError = ref('')

  /**
   * The first plugin-backed gateway (not a fixed module id, so a second gateway plugin is not
   * silently ignored), or null when the bank transfer is all there is.
   */
  const hostedGatewayModule = computed<string | null>(() =>
    gatewayMethods.value
      .find(m => isHostedGatewayModule(m.module))?.module ?? null)

  /**
   * Whether the manual bank transfer is among the ways this invoice can be paid, which the admin switches on in the
   * integrations; the panel offers the choice only then.
   */
  const bankTransferOffered = computed<boolean>(() =>
    gatewayMethods.value.some(m => m.module === PaymentModule.BankTransfer))

  /** The bank details and the reference of the transfer waiting on the invoice; null while none waits or before it is read. */
  const bankTransfer = ref<BankTransferDetails | null>(null)

  /** The sentence for a failed bank details read; empty when there is none to show. */
  const bankTransferError = ref('')

  /**
   * Reads one invoice with its line items.
   *
   * @param id - Invoice id.
   * @returns The invoice; null when it could not be read ({@link invoiceError} holds the sentence).
   */
  const loadInvoice = async (id: string): Promise<ClientInvoice | null> => {
    invoiceLoading.value = true
    invoiceError.value = ''
    invoice.value = null
    bankTransfer.value = null
    bankTransferError.value = ''
    try {
      invoice.value = await billing.fetchInvoice(id)
    } catch (e: unknown) {
      invoiceError.value = apiErrorMessage(e)
    } finally {
      invoiceLoading.value = false
    }
    return invoice.value
  }

  /**
   * Reads the bank details of the transfer waiting on the loaded invoice. Nothing is asked unless the invoice is
   * PaymentPending, so an open or paid invoice never shows bank details.
   *
   * @param id - Invoice id.
   * @returns Nothing; a refused read leaves its sentence in {@link bankTransferError}.
   */
  const loadBankTransfer = async (id: string): Promise<void> => {
    bankTransfer.value = null
    bankTransferError.value = ''
    if (invoice.value?.status !== InvoiceStatus.PaymentPending) {
      return
    }
    try {
      bankTransfer.value = await billing.fetchBankTransfer(id)
    } catch (e: unknown) {
      bankTransferError.value = apiErrorMessage(e)
    }
  }

  /**
   * Reads what the payer can pay with: the available gateways. A list that cannot be read is
   * kept apart from "no gateway": {@link optionsError} holds the sentence so the page can offer a retry.
   *
   * @returns Nothing.
   */
  const loadPaymentOptions = async (): Promise<void> => {
    optionsError.value = ''
    try {
      gatewayMethods.value = await billing.fetchGatewayMethods()
    } catch (e: unknown) {
      gatewayMethods.value = []
      optionsError.value = apiErrorMessage(e)
    }
  }

  /** The number of the order the last verified payment was for; empty for an invoice or before an answer. */
  const verifiedOrderNumber = ref('')

  /**
   * Asks the backend to settle a hosted-gateway payment with the bank, and keeps the order's
   * number from the answer in {@link verifiedOrderNumber}.
   *
   * A failure is never read as a decline: the money may well have moved, so it is reported as
   * {@link GatewayPaymentState.Unknown} and the reconciler settles the record regardless.
   *
   * @param target - Whether the payment was raised against an order or an invoice.
   * @param id - The order or invoice id.
   * @param paymentToken - The order's payment token; ignored for an invoice.
   * @returns What the bank said, or `Unknown` when no answer could be had.
   */
  const verifyGatewayPayment = async (
    target: PaymentTarget,
    id: string,
    paymentToken?: string
  ): Promise<GatewayPaymentState> => {
    try {
      const { state, orderNumber } = await billing.completeGatewayPayment(target, id, paymentToken)
      verifiedOrderNumber.value = orderNumber ?? ''
      return state
    } catch {
      return GatewayPaymentState.Unknown
    }
  }

  /** True while a payment request is in flight (and after a gateway hand-off, until the browser leaves). */
  const submitting = ref(false)

  /** The sentence to show for the last refused payment; empty when there is nothing to report. */
  const error = ref('')

  /**
   * Clears the state left by a previous invoice, called when the payment page opens.
   *
   * @returns Nothing.
   */
  const reset = (): void => {
    submitting.value = false
    error.value = ''
  }

  /**
   * Clears only the last error, called before a new attempt that never reaches the API.
   *
   * @returns Nothing.
   */
  const clearError = (): void => {
    error.value = ''
  }

  /**
   * Starts a hosted-gateway payment and returns where the browser must go next.
   *
   * On success {@link submitting} stays true: the page navigates away, and re-enabling the
   * buttons for that moment would invite a second payment.
   *
   * @param invoiceId - Invoice to pay.
   * @param module - Gateway plugin module id.
   * @param returnUrl - Absolute URL the gateway sends the customer back to.
   * @returns The gateway address to navigate to, or null when the attempt failed
   * ({@link error} holds the sentence to show).
   */
  const startGatewayPayment = async (
    invoiceId: string,
    module: string,
    returnUrl: string
  ): Promise<string | null> => {
    error.value = ''
    submitting.value = true
    try {
      const { redirectUrl } = await billing.startInvoiceGatewayPayment(invoiceId, module, returnUrl)
      if (!isSafeRedirectUrl(redirectUrl)) {
        // An address the browser must not follow is a failed start, not a destination.
        error.value = translate('checkout.errors.generic')
        submitting.value = false
        return null
      }
      return redirectUrl
    } catch (e: unknown) {
      error.value = apiErrorMessage(e)
      submitting.value = false
      return null
    }
  }

  /**
   * Chooses the manual bank transfer for the invoice. On success the invoice is PaymentPending, its bank details are in
   * {@link bankTransfer} and the page shows them; a refusal leaves its sentence in {@link error}.
   *
   * @param invoiceId - Invoice to pay by bank transfer.
   * @returns True when the transfer was chosen (or already was).
   */
  const selectBankTransfer = async (invoiceId: string): Promise<boolean> => {
    error.value = ''
    submitting.value = true
    try {
      bankTransfer.value = await billing.selectBankTransfer(invoiceId)
      bankTransferError.value = ''
      if (invoice.value) {
        invoice.value = { ...invoice.value, status: InvoiceStatus.PaymentPending }
      }
      return true
    } catch (e: unknown) {
      error.value = apiErrorMessage(e)
      return false
    } finally {
      submitting.value = false
    }
  }

  /** True while an invoice PDF is being downloaded. */
  const pdfDownloading = ref(false)

  /** The sentence for a failed PDF download; empty when the last one went through. */
  const pdfError = ref('')

  /**
   * Downloads an invoice as a PDF and hands it to the browser as `invoice-{id}.pdf`.
   *
   * @param id - Invoice id.
   * @returns Nothing; a refusal leaves its sentence in {@link pdfError}.
   */
  const downloadInvoicePdf = async (id: string): Promise<void> => {
    pdfDownloading.value = true
    pdfError.value = ''
    try {
      saveBlob(await billing.fetchInvoicePdf(id), `invoice-${id}.pdf`)
    } catch (e: unknown) {
      pdfError.value = apiErrorMessage(e)
    } finally {
      pdfDownloading.value = false
    }
  }

  return {
    invoice,
    invoiceLoading,
    pdfDownloading,
    pdfError,
    downloadInvoicePdf,
    invoiceError,
    gatewayMethods,
    optionsError,
    hostedGatewayModule,
    bankTransferOffered,
    bankTransfer,
    bankTransferError,
    loadBankTransfer,
    selectBankTransfer,
    loadInvoice,
    loadPaymentOptions,
    verifyGatewayPayment,
    verifiedOrderNumber,
    submitting,
    error,
    reset,
    clearError,
    startGatewayPayment
  }
})
