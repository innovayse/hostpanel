<script setup lang="ts">
/**
 * "Add payment" tab of the invoice screen: records a payment received outside the gateway, with
 * the line items and the transaction ledgers below. A draft or cancelled invoice takes no
 * payment, so the form is replaced by the reason. The outcome of recording a payment (success, or
 * the API's sentence) shows under the form.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import InvoiceItemsSection from '@/components/billing/InvoiceItemsSection.vue'
import InvoiceTransactionsSection from '@/components/billing/InvoiceTransactionsSection.vue'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiDatePicker from '@/components/ui/UiDatePicker.vue'
import UiField from '@/components/ui/UiField.vue'
import UiForm from '@/components/ui/UiForm.vue'
import UiFormActions from '@/components/ui/UiFormActions.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiNumberInput from '@/components/ui/UiNumberInput.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatusMessage from '@/components/ui/UiStatusMessage.vue'
import UiText from '@/components/ui/UiText.vue'
import { useInvoicePaymentForm } from '@/composables/useInvoicePaymentForm'
import { InvoiceStatus } from '@/types/invoice'
import { useBillingStore } from '@/stores/billing'
import type { InvoiceDetail } from '@/types/invoice'

const { t } = useI18n()

const props = defineProps<{
  /** The invoice to add payments to. */
  invoice: InvoiceDetail
  /** True to show the line items without editing controls (the form stays usable). */
  readonly?: boolean
}>()

const store = useBillingStore()

/** The shared payment form; recording reloads the invoice so the ledgers below show the payment. */
const form = useInvoicePaymentForm({
  record: store.addPayment,
  /**
   * Reads the store's last refusal sentence.
   *
   * @returns The sentence, or null when there is none.
   */
  failure: () => store.error,
  /**
   * Builds the sentence shown after a successful payment.
   *
   * @returns The localised sentence.
   */
  successMessage: () => t('billing.invoicePaymentTab.paymentAdded'),
})

/** True when the invoice is a draft, which takes no payment. */
const isDraft = computed<boolean>(() => props.invoice.status === InvoiceStatus.Draft)

/** True when the invoice is cancelled, which takes no payment. */
const isCancelled = computed<boolean>(() => props.invoice.status === InvoiceStatus.Cancelled)

/**
 * Records the payment against this invoice; the form clears itself on success.
 *
 * @returns Resolves when the request has settled.
 */
const submit = async (): Promise<void> => {
  await form.submit(props.invoice.id)
}
</script>

<template>
  <UiStack data-testid="billing-invoice-payment">
    <UiCard v-if="isDraft" padding="md">
      <UiText tone="muted" size="md" data-testid="billing-invoice-payment-draft">
        {{ t('billing.invoicePaymentTab.publishTheInvoiceBeforeAdding') }}
      </UiText>
    </UiCard>

    <UiAlert v-else-if="isCancelled" variant="warning" data-testid="billing-invoice-payment-cancelled">
      {{ t('billing.invoicePaymentTab.invoiceIsCancelledPleaseMark') }}
    </UiAlert>

    <template v-else>
      <UiCard padding="md">
        <UiForm stacked data-testid="billing-invoice-payment-form" @submit="submit">
          <UiHeading :level="3" variant="card">
            {{ t('billing.invoicePaymentTab.addPayment') }}
          </UiHeading>
          <UiGrid :columns="2">
            <UiField :label="t('shared.date')" :error="store.fieldErrors.date">
              <UiDatePicker v-model="form.paymentDate.value" :placeholder="t('shared.selectDate')" />
            </UiField>
            <UiField :label="t('shared.amount')" :error="store.fieldErrors.amount">
              <template #default="{ id, invalid }">
                <UiNumberInput
                  :id="id"
                  v-model="form.amount.value"
                  :step="0.01"
                  :min="0"
                  :invalid="invalid"
                  placeholder="0.00"
                  data-testid="billing-invoice-payment-amount"
                />
              </template>
            </UiField>
            <UiField :label="t('shared.paymentMethod')" :error="store.fieldErrors.gateway">
              <template #default="{ id, invalid }">
                <UiSelect
                  :id="id"
                  v-model="form.gateway.value"
                  :options="form.gatewayOptions.value"
                  :invalid="invalid"
                  :placeholder="t('shared.paymentMethod')"
                />
              </template>
            </UiField>
            <UiField :label="t('shared.transactionFees')" :error="store.fieldErrors.fees">
              <template #default="{ id, invalid }">
                <UiNumberInput
                  :id="id"
                  v-model="form.fees.value"
                  :step="0.01"
                  :min="0"
                  :invalid="invalid"
                  placeholder="0.00"
                />
              </template>
            </UiField>
            <UiField :label="t('shared.transactionId')" :error="store.fieldErrors.transactionId">
              <template #default="{ id, invalid }">
                <UiInput
                  :id="id"
                  v-model="form.transactionId.value"
                  type="text"
                  :invalid="invalid"
                  :placeholder="t('billing.invoicePaymentTab.gatewayReference')"
                  data-testid="billing-invoice-payment-reference"
                />
              </template>
            </UiField>
          </UiGrid>
          <UiFormActions>
            <UiButton
              type="submit"
              variant="primary"
              :loading="form.submitting.value"
              :disabled="!form.canSubmit.value"
              data-testid="billing-invoice-payment-submit"
            >
              {{ t('billing.invoicePaymentTab.addPayment') }}
            </UiButton>
          </UiFormActions>
          <UiStatusMessage :message="form.feedback.message.value" :tone="form.feedbackTone.value" data-testid="billing-invoice-payment-feedback" />
        </UiForm>
      </UiCard>

      <InvoiceItemsSection :invoice="invoice" :readonly="readonly" />
      <InvoiceTransactionsSection :invoice="invoice" />
    </template>
  </UiStack>
</template>
