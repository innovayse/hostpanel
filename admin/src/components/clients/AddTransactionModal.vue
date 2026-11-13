<script setup lang="ts">
/**
 * Modal for adding a new financial transaction to a client account.
 * Two-column form with date, description, amounts, and payment method.
 */
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useBillingStore } from '@/stores/billing'
import { useClientTransactionsStore } from '@/stores/clientTransactions'
import { GATEWAY_OPTIONS } from '@/lib/constants'
import { toDateInputValue } from '@/lib/format'
import { formatPlainAmount } from '@/lib/money'
import type { Invoice } from '@/types/invoice'
import type { UiSelectOption } from '@/utils/ui/select'
import { PaymentGateway } from '@/types/gateway'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCheckbox from '@/components/ui/UiCheckbox.vue'
import UiDatePicker from '@/components/ui/UiDatePicker.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiField from '@/components/ui/UiField.vue'
import UiForm from '@/components/ui/UiForm.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiLabel from '@/components/ui/UiLabel.vue'
import UiNumberInput from '@/components/ui/UiNumberInput.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiAlignItems, UiJustify } from '@/utils/ui/layout'
import { UiLabelVariant } from '@/utils/ui/text'

const { t } = useI18n()

/** Props for AddTransactionModal. */
const props = defineProps<{
  /** Client ID for the transaction. */
  clientId: string
  /** External saving state indicator. */
  saving: boolean
}>()

const emit = defineEmits<{
  /** Emitted after a successful save. */
  saved: []
  /** Emitted when the user closes the modal. */
  close: []
}>()

/** Client transactions store, the only caller of the transaction endpoints. */
const store = useClientTransactionsStore()

/** Billing store, the only caller of the invoice endpoints. */
const billingStore = useBillingStore()

/** The client's latest invoices, the choices of the invoice picker; empty until loaded. */
const invoices = ref<Invoice[]>([])

/** True while save is in progress. */
const internalSaving = ref(false)

/** Error message from the save operation. */
const saveError = ref<string | null>(null)

/** Transaction date in YYYY-MM-DD format. */
const date = ref(toDateInputValue(new Date().toISOString()))

/** Transaction description. */
const description = ref('')

/** External gateway transaction reference. */
const transactionId = ref('')

/** The linked invoice id; null when the transaction belongs to no invoice. */
const invoiceId = ref<number | null>(null)

/** The empty row first, then one row per invoice of the client. */
const invoiceOptions = computed<UiSelectOption<number | null>[]>(() => [
  { value: null, label: t('clients.addTransaction.noInvoice') },
  ...invoices.value.map(invoice => ({
    value: invoice.id,
    label: `#${invoice.id}`,
    description: `${invoice.status} · ${formatPlainAmount(invoice.total, invoice.currency)}`,
    keywords: invoice.status,
  })),
])

/** Selected payment method. */
const paymentMethod = ref<string>(PaymentGateway.None)

/** Amount received. */
const amountIn = ref(0)

/** Amount sent out. */
const amountOut = ref(0)

/** Gateway fees. */
const fees = ref(0)

/** Whether the transaction affects client credit balance. */
const addedToCredit = ref(false)

/**
 * Submits the transaction to the backend.
 *
 * @returns Promise that resolves when the transaction is saved.
 */
const handleSave = async (): Promise<void> => {
  internalSaving.value = true
  saveError.value = null
  const success = await store.create({
    clientId: Number(props.clientId),
    date: date.value || new Date().toISOString(),
    description: description.value,
    transactionId: transactionId.value,
    invoiceId: invoiceId.value,
    paymentMethod: paymentMethod.value,
    amountIn: amountIn.value,
    amountOut: amountOut.value,
    fees: fees.value,
    addedToCredit: addedToCredit.value,
  })
  internalSaving.value = false
  if (success) {
    emit('saved')
  } else {
    saveError.value = store.error ?? t('clients.addTransaction.failedToSaveTransaction')
  }
}

onMounted(async () => {
  invoices.value = await billingStore.fetchClientInvoiceOptions(props.clientId)
})
</script>

<template>
  <UiDialog :title="t('clients.addTransaction.addNewTransaction')" size="lg" @close="emit('close')">
    <UiStack>
      <UiAlert v-if="saveError" variant="error" data-testid="clients-add-transaction-error">
        {{ saveError }}
      </UiAlert>

      <UiForm stacked @submit="handleSave">
        <UiGrid :columns="2">
          <UiStack>
            <UiField :label="t('shared.date')">
              <UiDatePicker v-model="date" />
            </UiField>
            <UiField :label="t('shared.description')">
              <template #default="{ id }">
                <UiInput
                  :id="id"
                  v-model="description"
                  type="text"
                  :placeholder="t('clients.addTransaction.transactionDescription')"
                  data-testid="clients-add-transaction-description"
                />
              </template>
            </UiField>
            <UiField :label="t('shared.transactionId')">
              <template #default="{ id }">
                <UiInput
                  :id="id"
                  v-model="transactionId"
                  type="text"
                  :placeholder="t('clients.addTransaction.externalReference')"
                  data-testid="clients-add-transaction-reference"
                />
              </template>
            </UiField>
            <UiField :label="t('clients.addTransaction.invoiceIdOptional')">
              <template #default="{ id }">
                <UiSelect
                  :id="id"
                  v-model="invoiceId"
                  :options="invoiceOptions"
                  :placeholder="t('clients.addTransaction.selectInvoice')"
                  searchable
                  data-testid="clients-add-transaction-invoice-id"
                />
              </template>
            </UiField>
            <UiField :label="t('shared.paymentMethod')">
              <UiSelect
                v-model="paymentMethod"
                :options="GATEWAY_OPTIONS"
                :placeholder="t('clients.addTransaction.selectGateway')"
              />
            </UiField>
          </UiStack>

          <UiStack>
            <UiField :label="t('shared.amountIn')">
              <UiNumberInput v-model="amountIn" :step="0.01" :min="0" placeholder="0.00" data-testid="clients-add-transaction-amount-in" />
            </UiField>
            <UiField :label="t('shared.fees')">
              <UiNumberInput v-model="fees" :step="0.01" :min="0" placeholder="0.00" data-testid="clients-add-transaction-fees" />
            </UiField>
            <UiField :label="t('shared.amountOut')">
              <UiNumberInput v-model="amountOut" :step="0.01" :min="0" placeholder="0.00" data-testid="clients-add-transaction-amount-out" />
            </UiField>
            <UiLabel :variant="UiLabelVariant.Inline">
              <UiCheckbox
                v-model="addedToCredit"
                :aria-label="t('clients.addTransaction.addToClientSCredit')"
                data-testid="clients-add-transaction-credit"
              />
              {{ t('clients.addTransaction.addToClientSCredit') }}
            </UiLabel>
          </UiStack>
        </UiGrid>

        <UiStack direction="row" :justify="UiJustify.End" :align="UiAlignItems.Center" gap="sm">
          <UiButton
            type="button"
            :variant="UiButtonVariant.Secondary"
            data-testid="clients-add-transaction-cancel"
            @click="emit('close')"
          >
            {{ t('shared.cancel') }}
          </UiButton>
          <UiButton
            type="submit"
            :variant="UiButtonVariant.Primary"
            :loading="internalSaving"
            data-testid="clients-add-transaction-save"
          >
            {{ t('shared.save') }}
          </UiButton>
        </UiStack>
      </UiForm>
    </UiStack>
  </UiDialog>
</template>
