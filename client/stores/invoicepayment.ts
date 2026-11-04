/**
 * Pinia store for one invoice: reading it, paying it from `pages/client/invoices/[id]/pay.vue`,
 * and verifying a hosted-gateway payment on `pages/payment/result.vue`.
 *
 * Owns the loaded invoice, the saved and hosted payment methods, the in-flight flag, the success
 * flag and the refusal sentence, so the pages render them and never wrap an API call in their
 * own try/catch. URLs and transport belong to {@link useBillingApi}.
 *
 * @module stores/invoicepayment
 */

import { defineStore } from 'pinia'
import { useBillingApi } from '~/composables/apis/useBillingApi'
import { ActionOutcome } from '~/types/actionoutcome'
import { apiErrorMessage } from '~/lib/errorHandler'
import { saveBlob } from '~/utils/saveBlob'
import type { ClientInvoice } from '~/types/clientinvoice'
import type { GatewayMethod } from '~/types/gatewaymethod'
import { GatewayPaymentState } from '~/types/gatewaypaymentstate'
import type { PaymentMethod } from '~/types/payment'
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

  /** The client's saved cards and bank accounts; empty until {@link loadPaymentOptions}. */
  const savedMethods = ref<PaymentMethod[]>([])

  /** The gateways an invoice can be paid through; empty until {@link loadPaymentOptions}. */
  const gatewayMethods = ref<GatewayMethod[]>([])

  /**
   * The first plugin-backed gateway (not a fixed module id, so a second gateway plugin is not
   * silently ignored), or null when the card and bank-transfer built-ins are all there is.
   */
  const hostedGatewayModule = computed<string | null>(() =>
    gatewayMethods.value
      .find(m => isHostedGatewayModule(m.module))?.module ?? null)

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
   * Reads what the payer can pay with: the saved methods and the available gateways. Either list
   * that cannot be read stays empty, which the page already renders as "nothing selectable".
   *
   * @returns Nothing.
   */
  const loadPaymentOptions = async (): Promise<void> => {
    const [saved, gateways] = await Promise.all([
      billing.fetchPaymentMethods().catch((): PaymentMethod[] => []),
      billing.fetchGatewayMethods().catch((): GatewayMethod[] => [])
    ])
    savedMethods.value = saved
    gatewayMethods.value = gateways
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

  /** True once a saved-method payment has been accepted. */
  const paySuccess = ref(false)

  /** The sentence to show for the last refused payment; empty when there is nothing to report. */
  const error = ref('')

  /**
   * Clears the state left by a previous invoice, called when the payment page opens.
   *
   * @returns Nothing.
   */
  const reset = (): void => {
    submitting.value = false
    paySuccess.value = false
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
   * Charges the invoice to a saved payment method.
   *
   * @param invoiceId - Invoice to pay.
   * @param gatewayModuleId - The gateway module that holds the saved payment method.
   * @param methodId - Id of the saved payment method.
   * @param attemptId - The payment attempt the caller chose for this pay action; a retry of the
   * same action passes the same one, so the gateway takes the money once.
   * @returns How the attempt ended; on failure {@link error} holds the sentence to show.
   */
  const payWithSavedMethod = async (
    invoiceId: string,
    gatewayModuleId: string,
    methodId: string,
    attemptId: string
  ): Promise<ActionOutcome> => {
    error.value = ''
    submitting.value = true
    try {
      await billing.payInvoice(invoiceId, gatewayModuleId, methodId, attemptId)
      paySuccess.value = true
      return ActionOutcome.Succeeded
    } catch (e: unknown) {
      error.value = apiErrorMessage(e)
      return ActionOutcome.Failed
    } finally {
      submitting.value = false
    }
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
    savedMethods,
    gatewayMethods,
    hostedGatewayModule,
    loadInvoice,
    loadPaymentOptions,
    verifyGatewayPayment,
    verifiedOrderNumber,
    submitting,
    paySuccess,
    error,
    reset,
    clearError,
    payWithSavedMethod,
    startGatewayPayment
  }
})
