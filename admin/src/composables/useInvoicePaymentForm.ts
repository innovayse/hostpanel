/**
 * The payment form shared by the invoice "Add payment" tab and the Offline CC screen.
 *
 * Both record a payment received outside the gateway through the same endpoint, so the fields, the
 * submit guard, the payload, the form reset and the outcome line live here once. The caller only
 * decides which store action records the payment (with or without reloading the invoice) and what
 * its success sentence says.
 */
import { computed, ref } from 'vue'
import type { ComputedRef, Ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useResultBanner } from '@/composables/useResultBanner'
import type { ResultBanner } from '@/composables/useResultBanner'
import { GATEWAY_OPTIONS } from '@/lib/constants'
import type { SelectOption } from '@/lib/constants'
import type { InvoicePaymentPayload } from '@/types/invoice'
import { PaymentGateway } from '@/types/gateway'
import { UiTone } from '@/utils/ui/tone'

/** Gateway preselected in the form. */
export const DEFAULT_PAYMENT_GATEWAY: PaymentGateway = PaymentGateway.Stripe

/** What the caller supplies to {@link useInvoicePaymentForm}. */
export interface InvoicePaymentFormOptions {
  /** Records the payment against an invoice; true when accepted, false when refused. */
  record: (invoiceId: number, payment: InvoicePaymentPayload) => Promise<boolean>
  /** Reads the sentence of the last refusal (usually the store's `error`). */
  failure: () => string | null
  /** Builds the sentence shown after a successful payment. */
  successMessage: () => string
  /** True when the gateway reference must be filled in before submitting; false by default. */
  requireReference?: boolean
}

/** What {@link useInvoicePaymentForm} hands back. */
export interface InvoicePaymentForm {
  /** Payment date (ISO day), null until picked. */
  paymentDate: Ref<string | null>
  /** Gateway the payment came through. */
  gateway: Ref<PaymentGateway>
  /** Gateway transaction reference. */
  transactionId: Ref<string>
  /** Amount received. */
  amount: Ref<number>
  /** Gateway fees taken from the amount. */
  fees: Ref<number>
  /** True while the payment is being recorded. */
  submitting: Ref<boolean>
  /** Gateways the operator can pick: every real one, never "none". */
  gatewayOptions: ComputedRef<SelectOption<PaymentGateway>[]>
  /** The outcome line of the last payment. */
  feedback: ResultBanner
  /** Tone of the outcome line: danger for a refusal, success otherwise. */
  feedbackTone: ComputedRef<UiTone>
  /** True when the fields have what the API needs. */
  canSubmit: ComputedRef<boolean>
  /** Records the payment against the invoice; true when accepted. The form is cleared on success. */
  submit: (invoiceId: number) => Promise<boolean>
}

/**
 * Creates the state and actions of one payment form.
 *
 * @param options - The recording action and the sentences around it.
 * @returns The fields, the guard and the submit action.
 */
export const useInvoicePaymentForm = (options: InvoicePaymentFormOptions): InvoicePaymentForm => {
  const { t } = useI18n()

  /** Payment date (ISO day), null until picked. */
  const paymentDate = ref<string | null>(null)

  /** Gateway the payment came through. */
  const gateway = ref<PaymentGateway>(DEFAULT_PAYMENT_GATEWAY)

  /** Gateway transaction reference. */
  const transactionId = ref('')

  /** Amount received. */
  const amount = ref(0)

  /** Gateway fees taken from the amount. */
  const fees = ref(0)

  /** True while the payment is being recorded. */
  const submitting = ref(false)

  /** The outcome line of the last payment. */
  const feedback = useResultBanner()

  /** Gateways to offer: the option objects are reused so their labels keep following the language. */
  const gatewayOptions = computed<SelectOption<PaymentGateway>[]>(
    () => GATEWAY_OPTIONS.filter((option) => option.value !== PaymentGateway.None) as SelectOption<PaymentGateway>[],
  )

  /** Tone of the outcome line: danger for a refusal, success otherwise. */
  const feedbackTone = computed<UiTone>(() => (feedback.isError.value ? UiTone.Danger : UiTone.Success))

  /** True when the fields have what the API needs. */
  const canSubmit = computed<boolean>(() => {
    const hasDate = paymentDate.value !== null && paymentDate.value !== ''
    const hasReference = options.requireReference !== true || transactionId.value.trim() !== ''
    return hasDate && hasReference && amount.value > 0 && fees.value >= 0
  })

  /** Puts every field back to its starting value. */
  const reset = (): void => {
    paymentDate.value = null
    gateway.value = DEFAULT_PAYMENT_GATEWAY
    transactionId.value = ''
    amount.value = 0
    fees.value = 0
  }

  /**
   * Records the payment against the invoice and shows the outcome; on success the form is cleared.
   *
   * @param invoiceId - The invoice that was paid.
   * @returns True when the payment was accepted; false when it was refused or the form is incomplete.
   */
  const submit = async (invoiceId: number): Promise<boolean> => {
    if (!canSubmit.value || paymentDate.value === null) {
      return false
    }
    feedback.clear()
    submitting.value = true
    const ok = await options.record(invoiceId, {
      date: new Date(paymentDate.value).toISOString(),
      gateway: gateway.value,
      transactionId: transactionId.value.trim(),
      amount: amount.value,
      fees: fees.value,
    })
    submitting.value = false
    if (!ok) {
      feedback.show(options.failure() ?? t('errors.noAnswer'), true)
      return false
    }
    reset()
    feedback.show(options.successMessage(), false)
    return true
  }

  return { paymentDate, gateway, transactionId, amount, fees, submitting, gatewayOptions, feedback, feedbackTone, canSubmit, submit }
}
