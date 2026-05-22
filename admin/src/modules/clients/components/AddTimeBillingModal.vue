<script setup lang="ts">
/**
 * Modal for adding multiple time billing entries at once.
 * Displays a grid of 10 pre-created rows for quick data entry.
 */
import { ref, reactive, computed, onMounted } from 'vue'
import { useApi } from '../../../composables/useApi'
import AppNumberInput from '../../../components/AppNumberInput.vue'
import AppSelect from '../../../components/AppSelect.vue'
import type { ServiceListItem, PagedResult } from '../../../types/models'

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

const { request } = useApi()

/** True while save is in progress. */
const internalSaving = ref(false)

/** Error message from the save operation. */
const saveError = ref<string | null>(null)

/** Available services for the dropdown. */
const serviceOptions = ref<{ value: number; label: string }[]>([])

/** Single time billing row entry. */
interface TimeBillingRow {
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
    serviceId: 0,
    description: '',
    hours: 0,
    rate: 0,
  }))
)

/**
 * Computes the amount for a given row.
 *
 * @param row - The time billing row.
 * @returns Calculated amount (hours * rate).
 */
function rowAmount(row: TimeBillingRow): number {
  return row.hours * row.rate
}

/**
 * Fetches client services for the item dropdown.
 *
 * @returns Promise that resolves when services are loaded.
 */
async function fetchServices(): Promise<void> {
  try {
    const result = await request<PagedResult<ServiceListItem>>(
      `/services?clientId=${props.clientId}&pageSize=100`,
    )
    serviceOptions.value = result.items.map(s => ({ value: s.id, label: s.productName }))
  } catch {
    // Silently fail — dropdown will be empty
  }
}

/**
 * Filters non-empty rows and submits the time billing entries.
 *
 * @returns Promise that resolves when entries are saved.
 */
async function handleSave(): Promise<void> {
  const validRows = rows.filter(r => r.description.trim() !== '' && r.hours > 0)
  if (validRows.length === 0) {
    saveError.value = 'Please fill in at least one row with a description and hours.'
    return
  }

  internalSaving.value = true
  saveError.value = null
  try {
    await request(`/clients/${props.clientId}/billable-items/time-billing`, {
      method: 'POST',
      body: JSON.stringify({
        entries: validRows.map(r => ({
          serviceId: r.serviceId || null,
          description: r.description,
          hours: r.hours,
          rate: r.rate,
          amount: r.hours * r.rate,
        })),
      }),
    })
    emit('saved')
  } catch {
    saveError.value = 'Failed to save time billing entries.'
  } finally {
    internalSaving.value = false
  }
}

onMounted(() => fetchServices())
</script>

<template>
  <Teleport to="body">
    <div
      class="fixed inset-0 z-50 flex items-center justify-center bg-black/60"
      @click.self="emit('close')"
    >
      <div class="bg-zinc-900 border border-zinc-700 rounded-xl shadow-2xl w-full max-w-4xl p-6 space-y-4 max-h-[90vh] overflow-y-auto">
        <div class="flex items-center justify-between">
          <h2 class="text-white font-semibold text-lg">Add Time Billing Entries</h2>
          <button class="text-zinc-400 hover:text-white transition" @click="emit('close')">&#10005;</button>
        </div>

        <!-- Error -->
        <div v-if="saveError" class="text-sm text-status-red bg-status-red/10 border border-status-red/20 rounded-lg p-3">
          {{ saveError }}
        </div>

        <!-- Grid header -->
        <div class="hidden sm:grid grid-cols-[1.5fr_2fr_0.7fr_0.7fr_0.7fr] gap-2 text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-zinc-400">
          <span>Item</span>
          <span>Description</span>
          <span>Hours</span>
          <span>Rate</span>
          <span>Amount</span>
        </div>

        <!-- Rows -->
        <div class="space-y-2">
          <div
            v-for="(row, idx) in rows"
            :key="idx"
            class="grid grid-cols-1 sm:grid-cols-[1.5fr_2fr_0.7fr_0.7fr_0.7fr] gap-2 items-center"
          >
            <AppSelect
              v-model="row.serviceId"
              :options="[{ value: 0, label: 'None' }, ...serviceOptions]"
              placeholder="Service"
            />
            <input
              v-model="row.description"
              type="text"
              placeholder="Description"
              class="w-full bg-white/[0.04] border border-zinc-700 rounded-lg px-2.5 py-2 text-[0.82rem] text-white placeholder-zinc-500 focus:outline-none focus:border-primary-500/50 transition-colors"
            />
            <AppNumberInput v-model="row.hours" :step="0.25" :min="0" placeholder="0" />
            <AppNumberInput v-model="row.rate" :step="0.01" :min="0" placeholder="0.00" />
            <span class="text-[0.82rem] text-zinc-400 font-mono px-1">${{ rowAmount(row).toFixed(2) }}</span>
          </div>
        </div>

        <!-- Buttons -->
        <div class="flex justify-end gap-2 pt-2">
          <button
            type="button"
            class="px-4 py-2 bg-zinc-700 hover:bg-zinc-600 text-white text-sm rounded-lg transition"
            @click="emit('close')"
          >Cancel</button>
          <button
            type="button"
            :disabled="internalSaving"
            class="px-4 py-2 bg-blue-600 hover:bg-blue-500 text-white text-sm rounded-lg transition disabled:opacity-50"
            @click="handleSave"
          >{{ internalSaving ? 'Saving...' : 'Save Entries' }}</button>
        </div>
      </div>
    </div>
  </Teleport>
</template>
