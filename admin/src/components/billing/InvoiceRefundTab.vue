<script setup lang="ts">
/**
 * Refund tab of the invoice screen: pick a payment, the amount and the kind of refund, decide
 * what happens to the credit the payment created, and send it. A draft, cancelled or already
 * refunded invoice takes no refund, so the form is replaced by the reason. The refund is confirmed
 * first (amount and method in the prompt), and its outcome (success, or the API's sentence) shows
 * in the tab. The line items and the transaction ledgers sit below.
 */
import { computed, ref, watch } from 'vue'
import { formatPlainAmount } from '@/lib/money'
import { useI18n } from 'vue-i18n'
import InvoiceItemsSection from '@/components/billing/InvoiceItemsSection.vue'
import InvoiceTransactionsSection from '@/components/billing/InvoiceTransactionsSection.vue'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiField from '@/components/ui/UiField.vue'
import UiForm from '@/components/ui/UiForm.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiLabel from '@/components/ui/UiLabel.vue'
import UiNumberInput from '@/components/ui/UiNumberInput.vue'
import UiRadio from '@/components/ui/UiRadio.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { useResultBanner } from '@/composables/useResultBanner'
import { useBillingStore } from '@/stores/billing'
import { InvoiceRefundType, InvoiceTransactionType, InvoiceStatus } from '@/types/invoice'
import type { InvoiceDetail, InvoiceTransaction } from '@/types/invoice'
import { UiTone } from '@/utils/ui/tone'
import { UiDialogVariant } from '@/utils/ui/dialog'

const { t } = useI18n()

const props = defineProps<{
  /** The invoice to refund a payment of. */
  invoice: InvoiceDetail
  /** True to show the notice and the items without the refund form. */
  readonly?: boolean
}>()

/** What happens to the credit a refunded payment created. */
enum CreditAction {
  None = '',
  Remove = 'remove',
  Leave = 'leave',
}

const store = useBillingStore()

/** Id of the payment picked for refund, as the select holds it. */
const selectedTransactionId = ref('')

/** Amount to refund. */
const refundAmount = ref(0)

/** Kind of refund. */
const refundType = ref<InvoiceRefundType>(InvoiceRefundType.Gateway)

/** Reference of the refund, for a refund made outside the gateway. */
const refundReference = ref('')

/** The choice about the credit; the refund waits for it. */
const creditAction = ref<CreditAction>(CreditAction.None)

/** True while the refund is being sent. */
const processing = ref(false)

/** True while the confirmation prompt of the refund is open. */
const confirming = ref(false)

/** The outcome line of the last refund, shown in the tab. */
const feedback = useResultBanner()

/** Payments that can still be refunded. */
const payments = computed<InvoiceTransaction[]>(() =>
  (props.invoice.transactions ?? []).filter((transaction) => transaction.type === InvoiceTransactionType.Payment),
)

/**
 * What can still be refunded: the payments recorded against the invoice less the refunds already
 * made. The API refuses a larger amount; this only proposes a starting value.
 */
const refundable = computed<number>(() =>
  (props.invoice.transactions ?? []).reduce((sum, transaction) => {
    if (transaction.type === InvoiceTransactionType.Payment) {
      return sum + transaction.amount
    }
    if (transaction.type === InvoiceTransactionType.Refund) {
      return sum - Math.abs(transaction.amount)
    }
    return sum
  }, 0),
)

/** The picked payment, if any. */
const selectedPayment = computed<InvoiceTransaction | undefined>(() =>
  payments.value.find((transaction) => String(transaction.id) === selectedTransactionId.value),
)

/** True when the refund is made outside the gateway and needs a reference. */
const isManual = computed<boolean>(() => refundType.value === InvoiceRefundType.Manual)

/** True when the invoice is a draft, which cannot be refunded. */
const isDraft = computed<boolean>(() => props.invoice.status === InvoiceStatus.Draft)

/** True when the invoice is cancelled, which cannot be refunded. */
const isCancelled = computed<boolean>(() => props.invoice.status === InvoiceStatus.Cancelled)

/** True when the invoice was already refunded. */
const isRefunded = computed<boolean>(() => props.invoice.status === InvoiceStatus.Refunded)

/** True when the refund form is replaced by a notice. */
const formBlocked = computed<boolean>(() => isDraft.value || isCancelled.value || isRefunded.value)

/**
 * True when the refund can be sent: a payment is picked, the amount is above zero and the credit
 * question answered.
 */
const canRefund = computed<boolean>(
  () =>
    selectedTransactionId.value !== ''
    && refundAmount.value > 0
    && creditAction.value !== CreditAction.None
    && !processing.value,
)

/** Options of the payment select. */
const transactionOptions = computed(() => {
  if (payments.value.length === 0) {
    return [{ value: '', label: t('billing.invoiceRefundTab.noTransactionsAppliedToThis') }]
  }
  return [
    { value: '', label: t('billing.invoiceRefundTab.selectATransaction') },
    ...payments.value.map((transaction) => ({
      value: String(transaction.id),
      label: `#${transaction.id} — ${formatPlainAmount(transaction.amount, props.invoice.currency)} (${transaction.gateway})`,
    })),
  ]
})

/** Options of the refund-kind select. */
const refundTypeOptions = computed(() => [
  { value: InvoiceRefundType.Gateway, label: t('billing.invoiceRefundTab.refundThroughGateway') },
  { value: InvoiceRefundType.Manual, label: t('billing.invoiceRefundTab.manualRefundProcessedExternally') },
  { value: InvoiceRefundType.CreditBalance, label: t('billing.addTransaction.addToClientSCredit') },
])

/** The kind of refund actually sent: removing the credit turns it into a credit-balance refund. */
const sentKind = computed<InvoiceRefundType>(() =>
  creditAction.value === CreditAction.Remove ? InvoiceRefundType.CreditBalance : refundType.value,
)

/** The confirmation sentence, naming the amount and the method of the refund. */
const confirmMessage = computed<string>(() =>
  t('billing.invoiceRefundTab.confirmRefundMessage', {
    amount: formatPlainAmount(refundAmount.value, props.invoice.currency),
    method: refundTypeOptions.value.find((option) => option.value === sentKind.value)?.label ?? sentKind.value,
  }),
)

/** The warning that explains what the credit question is about. */
const creditWarning = computed<string>(() => {
  const refunded = formatPlainAmount(selectedPayment.value?.amount, props.invoice.currency)
  const balance = formatPlainAmount(props.invoice.amounts.credit, props.invoice.currency)
  return `${t('billing.invoiceRefundTab.theseTransactionsResultedInA')} ${refunded}${t('billing.invoiceRefundTab.theCurrentCreditBalanceIs')} ${balance}.`
})

/** Picking a payment resets the credit answer and proposes the payment, capped at what is still refundable. */
watch(selectedTransactionId, () => {
  creditAction.value = CreditAction.None
  if (selectedPayment.value !== undefined) {
    refundAmount.value = Math.max(0, Math.min(selectedPayment.value.amount, refundable.value))
  }
})

/** Opens the confirmation prompt when the form is complete. */
const askRefund = (): void => {
  if (canRefund.value) {
    confirming.value = true
  }
}

/** Closes the confirmation prompt; ignored while the refund is being sent. */
const dismiss = (): void => {
  if (!processing.value) {
    confirming.value = false
  }
}

/**
 * Sends the confirmed refund and shows the outcome; on success the form is cleared.
 *
 * @returns Resolves when the request has settled.
 */
const refund = async (): Promise<void> => {
  if (!canRefund.value) {
    return
  }
  feedback.clear()
  processing.value = true
  const kind = sentKind.value
  const manual = kind === InvoiceRefundType.Manual
  const ok = await store.refundPayment(props.invoice.id, {
    amount: refundAmount.value,
    refundType: kind,
    gateway: manual ? selectedPayment.value?.gateway : undefined,
    refundTransactionId: manual ? refundReference.value : undefined,
  })
  processing.value = false
  confirming.value = false
  if (!ok) {
    feedback.show(store.error ?? t('errors.noAnswer'), true)
    return
  }
  selectedTransactionId.value = ''
  refundAmount.value = 0
  refundType.value = InvoiceRefundType.Gateway
  refundReference.value = ''
  creditAction.value = CreditAction.None
  feedback.show(t('billing.invoiceRefundTab.refunded'), false)
}
</script>

<template>
  <UiStack data-testid="billing-invoice-refund">
    <UiAlert v-if="readonly && payments.length === 0" variant="warning" data-testid="billing-invoice-refund-none">
      {{ t('billing.invoiceRefundTab.thereAreNoTransactionsThat') }}
    </UiAlert>

    <template v-if="!readonly">
      <UiAlert v-if="isDraft" variant="warning" data-testid="billing-invoice-refund-draft">
        {{ t('billing.invoiceRefundTab.publishTheInvoiceBeforeRequesting') }}
      </UiAlert>
      <UiAlert v-else-if="isCancelled" variant="warning" data-testid="billing-invoice-refund-cancelled">
        {{ t('billing.invoiceRefundTab.cannotRefundACancelledInvoice') }}
      </UiAlert>
      <UiAlert v-else-if="isRefunded" variant="warning" data-testid="billing-invoice-refund-refunded">
        {{ t('billing.invoiceRefundTab.thisInvoiceHasAlreadyBeen') }}
      </UiAlert>

      <UiCard v-if="!formBlocked" padding="md">
        <UiForm stacked data-testid="billing-invoice-refund-form" @submit="askRefund">
          <UiField :label="t('shared.transactions')">
            <UiSelect
              v-model="selectedTransactionId"
              :options="transactionOptions"
              :placeholder="t('billing.invoiceRefundTab.selectATransaction')"
              data-testid="billing-invoice-refund-transaction"
            />
          </UiField>
          <UiField :label="t('shared.amount')" :error="store.fieldErrors.amount">
            <UiNumberInput v-model="refundAmount" :step="0.01" :min="0" placeholder="0.00" data-testid="billing-invoice-refund-amount" />
          </UiField>
          <UiField :label="t('billing.invoiceRefundTab.refundType')" :error="store.fieldErrors.refundType">
            <UiSelect v-model="refundType" :options="refundTypeOptions" :placeholder="t('billing.invoiceRefundTab.refundType')" />
          </UiField>
          <UiField v-if="isManual" :label="t('shared.transactionId')" :error="store.fieldErrors.refundTransactionId">
            <template #default="{ id, invalid }">
              <UiInput
                :id="id"
                v-model="refundReference"
                type="text"
                :invalid="invalid"
                :placeholder="t('billing.invoiceRefundTab.externalRefundReference')"
                data-testid="billing-invoice-refund-reference"
              />
            </template>
          </UiField>

          <UiStack v-if="selectedPayment" gap="sm" data-testid="billing-invoice-refund-credit">
            <UiText tone="danger" size="md">{{ t('billing.invoiceRefundTab.warning') }}</UiText>
            <UiText tone="secondary" size="md">{{ creditWarning }}</UiText>
            <UiText tone="muted" size="md">{{ t('billing.invoiceRefundTab.weCanThereforeAutomaticallyRemove') }}</UiText>
            <UiLabel variant="inline">
              <UiRadio
                v-model="creditAction"
                :value="CreditAction.Remove"
                data-testid="billing-invoice-refund-credit-remove"
              />
              {{ t('billing.invoiceRefundTab.clickHereToRemoveThe') }}
            </UiLabel>
            <UiLabel variant="inline">
              <UiRadio
                v-model="creditAction"
                :value="CreditAction.Leave"
                data-testid="billing-invoice-refund-credit-leave"
              />
              {{ t('billing.invoiceRefundTab.clickHereToLeaveThe') }}
            </UiLabel>
          </UiStack>

          <UiStack direction="row" justify="center">
            <UiButton
              type="submit"
              variant="primary"
              :loading="processing"
              :disabled="!canRefund"
              data-testid="billing-invoice-refund-submit"
            >
              {{ t('billing.invoiceRefundTab.refund') }}
            </UiButton>
          </UiStack>
        </UiForm>
      </UiCard>

      <UiAlert
        v-if="feedback.message.value"
        :variant="feedback.isError.value ? UiTone.Danger : UiTone.Success"
        data-testid="billing-invoice-refund-feedback"
      >
        {{ feedback.message.value }}
      </UiAlert>

      <UiDialog
        :open="confirming"
        :variant="UiDialogVariant.Confirm"
        :title="t('billing.invoiceRefundTab.confirmRefundTitle')"
        :message="confirmMessage"
        :confirm-label="t('billing.invoiceRefundTab.refund')"
        :loading="processing"
        :tone="UiTone.Danger"
        @confirm="refund"
        @close="dismiss"
      />
    </template>

    <InvoiceItemsSection :invoice="invoice" :readonly="readonly" />
    <InvoiceTransactionsSection :invoice="invoice" />
  </UiStack>
</template>
