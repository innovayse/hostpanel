<script setup lang="ts">
/**
 * The transaction filter fields of a list's filter dialog: the direction and the payment method (each a `UiSelect`
 * whose first choice is "any"), the period (a date range picker) and the amount. Shared by the ledger and the
 * transactions tab of a client's profile, so both offer the same filters in the same words. The values are the draft the
 * dialog edits (text, an empty string meaning "any"; the period as a date pair or null); they reach the address only
 * when the reader applies the dialog.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import UiDateRangePicker from '@/components/ui/UiDateRangePicker.vue'
import UiField from '@/components/ui/UiField.vue'
import UiNumberInput from '@/components/ui/UiNumberInput.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import { TRANSACTION_DIRECTION_OPTIONS, TRANSACTION_METHOD_OPTIONS } from '@/lib/constants'

const { t } = useI18n()

/** Props for TransactionFilterFields. */
defineProps<{
  /** Prefix of the test ids; the fields are `<prefix>-direction`, `<prefix>-method`, `<prefix>-period` and `<prefix>-amount`. */
  testidPrefix: string
}>()

/** The direction being chosen, a transaction direction or an empty string for both. */
const direction = defineModel<string>('direction', { required: true })

/** The payment method being chosen, a payment method or an empty string for any. */
const paymentMethod = defineModel<string>('paymentMethod', { required: true })

/** The period being chosen as a `[from, to]` pair of ISO dates, or null for none; the screen maps it onto its two address keys. */
const period = defineModel<[string, string] | null>('period', { required: true })

/** The amount being looked for as text, an empty string for any; the address keeps it as text. */
const amount = defineModel<string>('amount', { required: true })

/** Direction choices; the empty value means both. */
const directionOptions = computed(() => [{ value: '', label: t('billing.transactionsList.allActivity') }, ...TRANSACTION_DIRECTION_OPTIONS])

/** Payment method choices; the empty value means any. */
const methodOptions = computed(() => [{ value: '', label: t('shared.any') }, ...TRANSACTION_METHOD_OPTIONS])

/** The amount as the number input holds it: null while no amount is set. */
const amountNumber = computed<number | null>({
  /**
   * Reads the draft's amount as a number.
   *
   * @returns The amount, null when none is set.
   */
  get: () => (amount.value === '' ? null : Number(amount.value)),
  /**
   * Writes the amount back into the draft as text, empty for none.
   *
   * @param value - The amount typed; null, 0 or less clears it.
   */
  set: (value) => {
    amount.value = value !== null && value > 0 ? String(value) : ''
  },
})
</script>

<template>
  <UiStack>
    <UiField :label="t('billing.transactionsList.show')">
      <template #default="{ id }">
        <UiSelect
          :id="id"
          v-model="direction"
          :options="directionOptions"
          :placeholder="t('billing.transactionsList.show')"
          :data-testid="`${testidPrefix}-direction`"
        />
      </template>
    </UiField>
    <UiField :label="t('shared.paymentMethod')">
      <template #default="{ id }">
        <UiSelect
          :id="id"
          v-model="paymentMethod"
          :options="methodOptions"
          :placeholder="t('shared.paymentMethod')"
          :data-testid="`${testidPrefix}-method`"
        />
      </template>
    </UiField>
    <UiField :label="t('shared.dateRange')">
      <UiDateRangePicker
        v-model="period"
        :placeholder="t('shared.selectDateRange')"
        :data-testid="`${testidPrefix}-period`"
      />
    </UiField>
    <UiField :label="t('shared.amount')">
      <template #default="{ id }">
        <UiNumberInput
          :id="id"
          v-model="amountNumber"
          nullable
          :step="0.01"
          :min="0"
          placeholder="0.00"
          :data-testid="`${testidPrefix}-amount`"
        />
      </template>
    </UiField>
  </UiStack>
</template>
