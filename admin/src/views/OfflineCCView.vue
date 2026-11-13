<script setup lang="ts">
/**
 * Offline credit card processing: records a card payment taken by hand against an invoice.
 *
 * Only the invoice, date, amount, fees and the gateway reference are entered; card numbers are never
 * collected here. The outcome (success, or the API's sentence) shows under the form, and after a
 * successful payment the admin can open the invoice.
 */
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiDatePicker from '@/components/ui/UiDatePicker.vue'
import UiField from '@/components/ui/UiField.vue'
import UiForm from '@/components/ui/UiForm.vue'
import UiFormActions from '@/components/ui/UiFormActions.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiNumberInput from '@/components/ui/UiNumberInput.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatusMessage from '@/components/ui/UiStatusMessage.vue'
import { useInvoicePaymentForm } from '@/composables/useInvoicePaymentForm'
import { invoiceViewPath } from '@/lib/routes'
import { useBillingStore } from '@/stores/billing'

const { t } = useI18n()
const router = useRouter()
const store = useBillingStore()

/** The shared payment form; recording does not reload an invoice, this screen shows none. */
const form = useInvoicePaymentForm({
  record: store.recordPayment,
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
  successMessage: () => t('billing.offlineCC.paymentRecorded'),
  requireReference: true,
})

/** Id of the invoice being paid; 0 until entered. */
const invoiceId = ref(0)

/** Id of the invoice paid last, kept so the admin can open it; null before a success. */
const paidInvoiceId = ref<number | null>(null)

/** True when the form has what the API needs: the shared fields and an invoice to pay. */
const canSubmit = computed<boolean>(() => invoiceId.value > 0 && form.canSubmit.value)

/**
 * Records the payment and shows the outcome; on success the form is cleared.
 *
 * @returns Resolves when the request has settled.
 */
const submit = async (): Promise<void> => {
  if (!canSubmit.value) {
    return
  }
  const id = invoiceId.value
  if (await form.submit(id)) {
    paidInvoiceId.value = id
    invoiceId.value = 0
  }
}

/**
 * Opens the invoice that was paid last.
 *
 * @returns Resolves when the navigation has finished.
 */
const openInvoice = async (): Promise<void> => {
  if (paidInvoiceId.value === null) {
    return
  }
  await router.push(invoiceViewPath(paidInvoiceId.value))
}
</script>

<template>
  <UiStack>
    <UiPageHeader
      :title="t('billing.offlineCC.offlineCreditCardProcessing')"
      :subtitle="t('billing.offlineCC.processOfflineCreditCardPayments')"
      data-testid="offline-cc-header"
    />
    <UiCard padding="md">
      <UiForm stacked data-testid="offline-cc-form" @submit="submit">
        <UiGrid :columns="2">
          <UiField :label="t('shared.invoice')" :error="store.fieldErrors.invoiceId">
            <template #default="{ id, invalid }">
              <UiNumberInput
                :id="id"
                v-model="invoiceId"
                :step="1"
                :min="0"
                :invalid="invalid"
                placeholder="0"
                data-testid="offline-cc-invoice"
              />
            </template>
          </UiField>
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
                data-testid="offline-cc-amount"
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
                data-testid="offline-cc-fees"
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
          <UiField :label="t('shared.transactionId')" required :error="store.fieldErrors.transactionId">
            <template #default="{ id, invalid }">
              <UiInput
                :id="id"
                v-model="form.transactionId.value"
                type="text"
                :invalid="invalid"
                :placeholder="t('billing.invoicePaymentTab.gatewayReference')"
                data-testid="offline-cc-reference"
              />
            </template>
          </UiField>
        </UiGrid>
        <UiFormActions>
          <UiButton
            type="submit"
            variant="primary"
            :loading="form.submitting.value"
            :disabled="!canSubmit"
            data-testid="offline-cc-submit"
          >
            {{ t('billing.offlineCC.processPayment') }}
          </UiButton>
          <UiButton v-if="paidInvoiceId !== null" variant="secondary" data-testid="offline-cc-open-invoice" @click="openInvoice">
            {{ t('billing.offlineCC.viewInvoice', { id: paidInvoiceId }) }}
          </UiButton>
        </UiFormActions>
        <UiStatusMessage :message="form.feedback.message.value" :tone="form.feedbackTone.value" data-testid="offline-cc-feedback" />
      </UiForm>
    </UiCard>
  </UiStack>
</template>
