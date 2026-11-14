<script setup lang="ts">
/**
 * The invoice filter fields of a list's filter dialog: the status, a `UiSelect` whose first choice is "any", and the
 * creation period, a date range picker. Shared by the staff invoice list and the invoices tab of a client's profile, so
 * both offer the same filters in the same words. The values are the draft the dialog edits (the status as text, an empty
 * string meaning "any"; the period as a date pair or null); they reach the address only when the reader applies the dialog.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import UiDateRangePicker from '@/components/ui/UiDateRangePicker.vue'
import UiField from '@/components/ui/UiField.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import { INVOICE_STATUS_OPTIONS } from '@/lib/constants'

const { t } = useI18n()

/** Props for InvoiceFilterFields. */
defineProps<{
  /** Prefix of the test ids; the fields are `<prefix>-status` and `<prefix>-period`. */
  testidPrefix: string
}>()

/** The status being chosen, an invoice status or an empty string for any. */
const status = defineModel<string>('status', { required: true })

/** The creation period being chosen as a `[from, to]` pair of ISO dates, or null for none; the screen maps it onto its two address keys. */
const period = defineModel<[string, string] | null>('period', { required: true })

/** Status choices; the empty value means any. */
const statusOptions = computed(() => [{ value: '', label: t('shared.any') }, ...INVOICE_STATUS_OPTIONS])
</script>

<template>
  <UiStack>
    <UiField :label="t('shared.status')">
      <template #default="{ id }">
        <UiSelect
          :id="id"
          v-model="status"
          :options="statusOptions"
          :placeholder="t('shared.any')"
          :data-testid="`${testidPrefix}-status`"
        />
      </template>
    </UiField>
    <UiField :label="t('shared.invoiceDate')">
      <UiDateRangePicker
        v-model="period"
        :placeholder="t('shared.selectDateRange')"
        :data-testid="`${testidPrefix}-period`"
      />
    </UiField>
  </UiStack>
</template>
