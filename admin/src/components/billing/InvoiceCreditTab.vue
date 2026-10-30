<script setup lang="ts">
/**
 * Credit tab of the invoice screen: apply credit to the invoice or take it back, with the line
 * items and the transaction ledgers below. Taking credit back is confirmed first; the outcome of
 * each action (success, or the API's sentence) shows in the tab.
 */
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import InvoiceItemsSection from '@/components/billing/InvoiceItemsSection.vue'
import InvoiceTransactionsSection from '@/components/billing/InvoiceTransactionsSection.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiNumberInput from '@/components/ui/UiNumberInput.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatusMessage from '@/components/ui/UiStatusMessage.vue'
import UiText from '@/components/ui/UiText.vue'
import { useResultBanner } from '@/composables/useResultBanner'
import { formatPlainAmount } from '@/lib/money'
import { useBillingStore } from '@/stores/billing'
import type { InvoiceDetail } from '@/types/invoice'
import { UiDialogVariant } from '@/utils/ui/dialog'
import { UiTone } from '@/utils/ui/tone'

const { t } = useI18n()

const props = defineProps<{
  /** The invoice to manage credit for. */
  invoice: InvoiceDetail
  /** True to show the line items without editing controls. */
  readonly?: boolean
}>()

const store = useBillingStore()

/** Credit to apply to the invoice. */
const addAmount = ref(0)

/** Credit to take back from the invoice. */
const removeAmount = ref(0)

/** True while a credit change is in flight. */
const processing = ref(false)

/** True while the confirmation prompt of the credit removal is open. */
const confirmingRemove = ref(false)

/** The outcome line of the last credit change, shown in the tab. */
const feedback = useResultBanner()

/** Credit currently applied to the invoice. */
const availableCredit = computed<number>(() => props.invoice.amounts.credit ?? 0)

/** The credit available, formatted for the caption under each box. */
const availableText = computed<string>(() =>
  t('billing.invoiceCreditTab.valueAvailable', { value: formatPlainAmount(availableCredit.value, props.invoice.currency) }),
)

/** True when "apply credit" has an amount to apply. */
const canAdd = computed<boolean>(() => !processing.value && addAmount.value > 0)

/** True when "remove credit" has an amount and credit to take back. */
const canRemove = computed<boolean>(() => !processing.value && removeAmount.value > 0 && availableCredit.value > 0)

/** Tone of the outcome line: danger for a refusal, success otherwise. */
const feedbackTone = computed<UiTone>(() => (feedback.isError.value ? UiTone.Danger : UiTone.Success))

/**
 * Runs one credit change and shows its outcome in the tab.
 *
 * @param change - The store call to run.
 * @param successKey - Locale key of the success sentence.
 * @returns True when the API accepted the change.
 */
const runChange = async (change: () => Promise<boolean>, successKey: string): Promise<boolean> => {
  feedback.clear()
  processing.value = true
  const ok = await change()
  processing.value = false
  if (ok) {
    feedback.show(t(successKey), false)
  } else {
    feedback.show(store.error ?? t('errors.noAnswer'), true)
  }
  return ok
}

/**
 * Applies the credit and shows the outcome.
 *
 * @returns Resolves when the request has settled.
 */
const addCredit = async (): Promise<void> => {
  const ok = await runChange(() => store.applyCredit(props.invoice.id, addAmount.value), 'billing.invoiceCreditTab.creditApplied')
  if (ok) {
    addAmount.value = 0
  }
}

/** Opens the confirmation prompt of the credit removal when it can run. */
const askRemove = (): void => {
  if (canRemove.value) {
    confirmingRemove.value = true
  }
}

/** Closes the confirmation prompt; ignored while the removal runs. */
const dismissRemove = (): void => {
  if (!processing.value) {
    confirmingRemove.value = false
  }
}

/**
 * Takes the credit back (after the operator confirmed) and shows the outcome.
 *
 * @returns Resolves when the request has settled.
 */
const removeCredit = async (): Promise<void> => {
  const ok = await runChange(() => store.removeCredit(props.invoice.id, removeAmount.value), 'billing.invoiceCreditTab.creditRemoved')
  confirmingRemove.value = false
  if (ok) {
    removeAmount.value = 0
  }
}
</script>

<template>
  <UiStack data-testid="billing-invoice-credit">
    <UiCard padding="md">
      <UiStack>
        <UiGrid :columns="2" gap="lg">
          <UiStack align="center" gap="sm">
            <UiHeading :level="3" variant="card">
              {{ t('billing.invoiceCreditTab.addCreditToInvoice') }}
            </UiHeading>
            <UiStack direction="row" gap="sm" align="center">
              <UiNumberInput v-model="addAmount" :step="0.01" :min="0" placeholder="0.00" data-testid="billing-invoice-credit-add-amount" />
              <UiButton
                variant="secondary"
                :disabled="!canAdd"
                data-testid="billing-invoice-credit-add"
                @click="addCredit"
              >
                {{ t('shared.go') }}
              </UiButton>
            </UiStack>
            <UiText tone="success" size="sm">{{ availableText }}</UiText>
          </UiStack>

          <UiStack align="center" gap="sm">
            <UiHeading :level="3" variant="card">
              {{ t('billing.invoiceCreditTab.removeCreditFromInvoice') }}
            </UiHeading>
            <UiStack direction="row" gap="sm" align="center">
              <UiNumberInput v-model="removeAmount" :step="0.01" :min="0" placeholder="0.00" data-testid="billing-invoice-credit-remove-amount" />
              <UiButton
                variant="secondary"
                :disabled="!canRemove"
                data-testid="billing-invoice-credit-remove"
                @click="askRemove"
              >
                {{ t('shared.go') }}
              </UiButton>
            </UiStack>
            <UiText tone="success" size="sm">{{ availableText }}</UiText>
          </UiStack>
        </UiGrid>
        <UiStatusMessage :message="feedback.message.value" :tone="feedbackTone" data-testid="billing-invoice-credit-feedback" />
      </UiStack>
    </UiCard>

    <UiDialog
      :open="confirmingRemove"
      :variant="UiDialogVariant.Confirm"
      :title="t('billing.invoiceCreditTab.confirmRemoveCreditTitle')"
      :message="t('billing.invoiceCreditTab.confirmRemoveCreditMessage', { amount: formatPlainAmount(removeAmount, invoice.currency), id: invoice.id })"
      :confirm-label="t('billing.invoiceCreditTab.removeCredit')"
      :loading="processing"
      :tone="UiTone.Danger"
      @confirm="removeCredit"
      @close="dismissRemove"
    />

    <InvoiceItemsSection :invoice="invoice" :readonly="readonly" />
    <InvoiceTransactionsSection :invoice="invoice" />
  </UiStack>
</template>
