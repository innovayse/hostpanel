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
import UiCheckbox from '@/components/ui/UiCheckbox.vue'
import UiClientSelect from '@/components/ui/UiClientSelect.vue'
import UiDatePicker from '@/components/ui/UiDatePicker.vue'
import UiFormActions from '@/components/ui/UiFormActions.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiNumberInput from '@/components/ui/UiNumberInput.vue'
import UiSchemaForm from '@/components/ui/UiSchemaForm.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiText from '@/components/ui/UiText.vue'
import { TRANSACTION_METHOD_OPTIONS } from '@/lib/constants'
import type { ClientOption } from '@/types/clientoption'
import type { TransactionFormValues } from '@/types/transaction'
import type { SkeletonSection } from '@/utils/ui/skeletonSchema'
import { AppRoute } from '@/lib/routes'
import { UiActionKind } from '@/utils/ui/actions'

const { t } = useI18n()

const props = withDefaults(defineProps<{
  /** The columns of sections of the screen's schema: the fields the form draws. */
  columns: readonly (readonly SkeletonSection[])[]
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
  <UiSchemaForm
    :columns="columns"
    :errors="fieldErrors"
    class="max-w-3xl"
    data-testid="billing-transaction-form"
    @submit="emit('submit')"
  >
    <template #field-date>
      <UiDatePicker
        :model-value="values.date"
        :placeholder="t('shared.selectDate')"
        @update:model-value="patch({ date: $event })"
      />
    </template>
    <template #field-clientId>
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
    </template>
    <template #field-amountIn>
      <UiNumberInput
        :model-value="values.amountIn"
        :step="0.01"
        :min="0"
        placeholder="0.00"
        data-testid="billing-transaction-amount-in"
        @update:model-value="patch({ amountIn: $event ?? 0 })"
      />
    </template>
    <template #field-amountOut>
      <UiNumberInput
        :model-value="values.amountOut"
        :step="0.01"
        :min="0"
        placeholder="0.00"
        data-testid="billing-transaction-amount-out"
        @update:model-value="patch({ amountOut: $event ?? 0 })"
      />
    </template>
    <template #field-description="{ id, invalid }">
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
    <template #field-fees>
      <UiNumberInput
        :model-value="values.fees"
        :step="0.01"
        :min="0"
        placeholder="0.00"
        @update:model-value="patch({ fees: $event ?? 0 })"
      />
    </template>
    <template #field-transactionId="{ id, invalid }">
      <UiInput
        :id="id"
        :model-value="values.transactionId"
        type="text"
        :invalid="invalid"
        :placeholder="t('billing.addTransaction.externalTransactionId')"
        @update:model-value="patch({ transactionId: $event })"
      />
    </template>
    <template #field-paymentMethod>
      <UiSelect
        :model-value="values.paymentMethod"
        :options="methodOptions"
        :placeholder="t('shared.paymentMethod')"
        @update:model-value="patch({ paymentMethod: $event })"
      />
    </template>
    <template #field-invoiceId="{ id, invalid }">
      <UiInput
        :id="id"
        :model-value="values.invoiceIds"
        type="text"
        :invalid="invalid"
        :placeholder="t('billing.addTransaction.invoiceIdsExample')"
        @update:model-value="patch({ invoiceIds: $event })"
      />
    </template>
    <template #field-addToCredit>
      <UiCheckbox
        :model-value="values.addToCredit"
        data-testid="billing-transaction-add-to-credit"
        @update:model-value="patch({ addToCredit: $event })"
      />
    </template>

    <UiAlert v-if="error" variant="error" data-testid="billing-transaction-error">
      {{ error }}
    </UiAlert>

    <UiFormActions>
      <UiLink :action="UiActionKind.Cancel" :to="AppRoute.BillingTransactions" variant="ghost" data-testid="billing-transaction-cancel">
        {{ t('shared.cancel') }}
      </UiLink>
      <UiButton :action="UiActionKind.Save"
        type="submit"
        variant="primary"
        :loading="loading"
        data-testid="billing-transaction-submit"
      >
        {{ submitLabel }}
      </UiButton>
    </UiFormActions>
  </UiSchemaForm>
</template>
