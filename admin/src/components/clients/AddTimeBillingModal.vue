<script setup lang="ts">
/**
 * Modal for adding multiple time billing entries at once.
 * Displays a grid of 10 pre-created rows for quick data entry.
 */
import { ref, reactive, onMounted } from 'vue'
import { useI18n } from 'vue-i18n'
import { useClientBillableItemsStore } from '@/stores/clientBillableItems'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiNumberInput from '@/components/ui/UiNumberInput.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import { nextRowKey } from '@/lib/rowKey'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiTableDensity } from '@/utils/ui/injection'

const { t } = useI18n()

/** Props for AddTimeBillingModal. */
const props = defineProps<{
  /** Client ID for fetching services. */
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

/** Billable items store, the only caller of the time billing endpoints. */
const store = useClientBillableItemsStore()

/** True while save is in progress. */
const internalSaving = ref(false)

/** Error message from the save operation. */
const saveError = ref<string | null>(null)

/** Available services for the dropdown. */
const serviceOptions = ref<{ value: number; label: string }[]>([])

/** Single time billing row entry. */
interface TimeBillingRow {
  /** Stable key of the row for list rendering. */
  key: number
  /** Selected service ID. */
  serviceId: number
  /** Entry description. */
  description: string
  /** Hours worked. */
  hours: number
  /** Hourly rate. */
  rate: number
}

/** Pre-created array of 10 empty rows. */
const rows = reactive<TimeBillingRow[]>(
  Array.from({ length: 10 }, () => ({
    key: nextRowKey(),
    serviceId: 0,
    description: '',
    hours: 0,
    rate: 0,
  })),
)

/**
 * Computes the amount for a given row.
 *
 * @param row - The time billing row.
 * @returns Calculated amount (hours * rate).
 */
const rowAmount = (row: TimeBillingRow): number => row.hours * row.rate

/**
 * Fetches client services for the item dropdown.
 *
 * @returns Promise that resolves when services are loaded.
 */
const fetchServices = async (): Promise<void> => {
  serviceOptions.value = await store.fetchServiceOptions(props.clientId)
}

/**
 * Filters non-empty rows and submits the time billing entries.
 *
 * @returns Promise that resolves when entries are saved.
 */
const handleSave = async (): Promise<void> => {
  const validRows = rows.filter(r => r.description.trim() !== '' && r.hours > 0)
  if (validRows.length === 0) {
    saveError.value = t('clients.addTimeBilling.pleaseFillInAtLeastOne')
    return
  }

  internalSaving.value = true
  saveError.value = null
  const saved = await store.addTimeBilling(props.clientId, validRows.map(r => ({
    serviceId: r.serviceId || null,
    description: r.description,
    hours: r.hours,
    rate: r.rate,
    amount: r.hours * r.rate,
  })))
  internalSaving.value = false
  if (saved) {
    emit('saved')
  } else {
    saveError.value = store.saveError
  }
}

onMounted(() => fetchServices())
</script>

<template>
  <UiDialog :title="t('clients.addTimeBilling.addTimeBillingEntries')" size="xl" @close="emit('close')">
    <UiStack>
      <UiAlert v-if="saveError" variant="error" data-testid="clients-time-billing-error">
        {{ saveError }}
      </UiAlert>

      <UiTable :density="UiTableDensity.Compact" :scrollable="false" data-testid="clients-time-billing-table">
        <UiTableHead>
          <UiTableRow>
            <UiTableHeader>{{ t('clients.addTimeBilling.item') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.description') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.hours') }}</UiTableHeader>
            <UiTableHeader>{{ t('clients.addTimeBilling.rate') }}</UiTableHeader>
            <UiTableHeader numeric>{{ t('shared.amount') }}</UiTableHeader>
          </UiTableRow>
        </UiTableHead>
        <UiTableBody>
          <UiTableRow v-for="(row, idx) in rows" :key="row.key">
            <UiTableCell>
              <UiSelect
                v-model="row.serviceId"
                :options="[{ value: 0, label: t('shared.none') }, ...serviceOptions]"
                :placeholder="t('clients.addTimeBilling.service')"
              />
            </UiTableCell>
            <UiTableCell>
              <UiInput
                v-model="row.description"
                type="text"
                :placeholder="t('shared.description')"
                :data-testid="`clients-time-billing-description-${idx}`"
              />
            </UiTableCell>
            <UiTableCell>
              <UiNumberInput v-model="row.hours" :step="0.25" :min="0" placeholder="0" :data-testid="`clients-time-billing-hours-${idx}`" />
            </UiTableCell>
            <UiTableCell>
              <UiNumberInput v-model="row.rate" :step="0.01" :min="0" placeholder="0.00" :data-testid="`clients-time-billing-rate-${idx}`" />
            </UiTableCell>
            <UiTableCell muted numeric>${{ rowAmount(row).toFixed(2) }}</UiTableCell>
          </UiTableRow>
        </UiTableBody>
      </UiTable>
    </UiStack>
    <template #footer>
      <UiButton
        type="button"
        :variant="UiButtonVariant.Secondary"
        data-testid="clients-time-billing-cancel"
        @click="emit('close')"
      >
        {{ t('shared.cancel') }}
      </UiButton>
      <UiButton
        type="button"
        :variant="UiButtonVariant.Primary"
        :loading="internalSaving"
        data-testid="clients-time-billing-save"
        @click="handleSave"
      >
        {{ t('clients.addTimeBilling.saveEntries') }}
      </UiButton>
    </template>
  </UiDialog>
</template>
