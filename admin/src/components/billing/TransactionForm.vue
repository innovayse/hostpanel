<script setup lang="ts">
/**
 * The form behind both "add transaction" and "edit transaction": date, client, amounts, fees,
 * description, gateway reference, payment method, linked invoice and the credit flag. The screen
 * owns the values (`v-model`), the submit and the cancel route.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiCheckbox from '@/components/ui/UiCheckbox.vue'
import UiClientSelect from '@/components/ui/UiClientSelect.vue'
import UiDatePicker from '@/components/ui/UiDatePicker.vue'
import UiField from '@/components/ui/UiField.vue'
import UiForm from '@/components/ui/UiForm.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiLabel from '@/components/ui/UiLabel.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiNumberInput from '@/components/ui/UiNumberInput.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { TRANSACTION_METHOD_OPTIONS } from '@/lib/constants'
import type { ClientOption } from '@/types/clientoption'
import type { TransactionFormValues } from '@/types/transaction'
import { AppRoute } from '@/lib/routes'

const { t } = useI18n()

const props = withDefaults(defineProps<{
  /** The clients the picker offers. */
  clients: ClientOption[]
  /** Caption of the submit button. */
  submitLabel: string
  /** True while the request is in flight. */
  loading?: boolean
  /** The sentence for a refused request, null when none. */
  error?: string | null
  /** Per-field messages of a refused request, keyed by field name. */
  fieldErrors?: Record<string, string>
  /** True to show the client as text instead of a picker (a transaction cannot change client). */
  lockClient?: boolean
}>(), {
  loading: false,
  error: null,
  /**
   * Default for the field errors.
   *
   * @returns A new empty map, so instances never share one.
   */
  fieldErrors: () => ({}),
  lockClient: false,
})

const emit = defineEmits<{
  /** Emitted when the operator submits the form. */
  submit: []
}>()

/** The form values; the form writes through this model. */
const values = defineModel<TransactionFormValues>({ required: true })

/** Options of the payment-method select: "none" plus the shared methods. */
const methodOptions = computed(() => [{ value: '', label: t('shared.none') }, ...TRANSACTION_METHOD_OPTIONS])

/** Name of the client shown when the picker is locked. */
const lockedClientName = computed<string>(
  () => props.clients.find((client) => String(client.id) === values.value.clientId)?.name ?? values.value.clientId,
)

/**
 * Writes one field of the form.
 *
 * @param change - The fields to overwrite.
 */
const patch = (change: Partial<TransactionFormValues>): void => {
  values.value = { ...values.value, ...change }
}
</script>

<template>
  <UiCard padding="lg" class="max-w-3xl" data-testid="billing-transaction-form">
    <UiForm stacked @submit="emit('submit')">
      <UiAlert v-if="error" variant="error" data-testid="billing-transaction-error">
        {{ error }}
      </UiAlert>

      <UiGrid :columns="2">
        <UiField :label="t('shared.date')" :error="fieldErrors.date">
          <UiDatePicker
            :model-value="values.date"
            :placeholder="t('shared.selectDate')"
            @update:model-value="patch({ date: $event })"
          />
        </UiField>
        <UiField :label="t('billing.addTransaction.relatedClient')" :error="fieldErrors.clientId">
          <UiText v-if="lockClient" tone="primary" size="md" data-testid="billing-transaction-client-locked">
            {{ lockedClientName }}
          </UiText>
          <UiClientSelect
            v-else
            :model-value="values.clientId"
            :clients="clients"
            :placeholder="t('shared.selectAClient')"
            data-testid="billing-transaction-client"
            @update:model-value="patch({ clientId: $event })"
          />
        </UiField>
      </UiGrid>

      <UiGrid :columns="2">
        <UiField :label="t('shared.amountIn')" :error="fieldErrors.amountIn">
          <UiNumberInput
            :model-value="values.amountIn"
            :step="0.01"
            :min="0"
            placeholder="0.00"
            data-testid="billing-transaction-amount-in"
            @update:model-value="patch({ amountIn: $event ?? 0 })"
          />
        </UiField>
        <UiField :label="t('shared.amountOut')" :error="fieldErrors.amountOut">
          <UiNumberInput
            :model-value="values.amountOut"
            :step="0.01"
            :min="0"
            placeholder="0.00"
            data-testid="billing-transaction-amount-out"
            @update:model-value="patch({ amountOut: $event ?? 0 })"
          />
        </UiField>
      </UiGrid>

      <UiGrid :columns="2">
        <UiField :label="t('shared.description')" :error="fieldErrors.description">
          <template #default="{ id, invalid }">
            <UiInput
              :id="id"
              :model-value="values.description"
              type="text"
              :invalid="invalid"
              :placeholder="t('billing.addTransaction.whatIsThisTransactionFor')"
              data-testid="billing-transaction-description"
              @update:model-value="patch({ description: $event })"
            />
          </template>
        </UiField>
        <UiField :label="t('shared.fees')" :error="fieldErrors.fees">
          <UiNumberInput
            :model-value="values.fees"
            :step="0.01"
            :min="0"
            placeholder="0.00"
            @update:model-value="patch({ fees: $event ?? 0 })"
          />
        </UiField>
      </UiGrid>

      <UiGrid :columns="2">
        <UiField :label="t('shared.transactionId')" :error="fieldErrors.transactionId">
          <template #default="{ id, invalid }">
            <UiInput
              :id="id"
              :model-value="values.transactionId"
              type="text"
              :invalid="invalid"
              :placeholder="t('billing.addTransaction.externalTransactionId')"
              @update:model-value="patch({ transactionId: $event })"
            />
          </template>
        </UiField>
        <UiField :label="t('shared.paymentMethod')" :error="fieldErrors.paymentMethod">
          <UiSelect
            :model-value="values.paymentMethod"
            :options="methodOptions"
            :placeholder="t('shared.paymentMethod')"
            @update:model-value="patch({ paymentMethod: $event })"
          />
        </UiField>
      </UiGrid>

      <UiField :label="t('billing.addTransaction.invoiceIdsCommaSeparated')" :error="fieldErrors.invoiceId">
        <template #default="{ id, invalid }">
          <UiInput
            :id="id"
            :model-value="values.invoiceIds"
            type="text"
            :invalid="invalid"
            :placeholder="t('billing.addTransaction.invoiceIdsExample')"
            @update:model-value="patch({ invoiceIds: $event })"
          />
        </template>
      </UiField>

      <UiLabel variant="inline">
        <UiCheckbox
          :model-value="values.addToCredit"
          data-testid="billing-transaction-add-to-credit"
          @update:model-value="patch({ addToCredit: $event })"
        />
        {{ t('billing.addTransaction.addToClientSCredit') }}
      </UiLabel>

      <UiStack direction="row" gap="md" align="center">
        <UiButton
          type="submit"
          variant="primary"
          :loading="loading"
          data-testid="billing-transaction-submit"
        >
          {{ submitLabel }}
        </UiButton>
        <UiLink :to="AppRoute.BillingTransactions" variant="ghost" data-testid="billing-transaction-cancel">
          {{ t('shared.cancel') }}
        </UiLink>
      </UiStack>
    </UiForm>
  </UiCard>
</template>
