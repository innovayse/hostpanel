<script setup lang="ts">
/**
 * Modal for adding a single billable item to a client.
 * Supports various invoice actions including recurring billing.
 */
import { ref, computed, onMounted } from 'vue'
import { useApi } from '../../../composables/useApi'
import { INVOICE_ACTION_OPTIONS, RECURRENCE_PERIOD_OPTIONS } from '../../../utils/constants'
import { toDateInputValue } from '../../../utils/format'
import AppDatePicker from '../../../components/AppDatePicker.vue'
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

// --- Form fields ---

/** Selected service ID. */
const serviceId = ref<number>(0)

/** Item description. */
const description = ref('')

/** Hours or quantity value. */
const hoursQty = ref(0)

/** Whether the hoursQty field represents hours. */
const isHours = ref(true)

/** Charge amount. */
const amount = ref(0)

/** Selected invoice action. */
const invoiceAction = ref('DontInvoice')

/** Due date in YYYY-MM-DD format. */
const dueDate = ref(toDateInputValue(new Date().toISOString()))

/** Invoice count (read-only for new items). */
const invoiceCount = ref(0)

/** Recurrence interval (every N). */
const recurrenceInterval = ref(1)

/** Recurrence period unit. */
const recurrencePeriod = ref('Months')

/** Recurrence limit (number of times). */
const recurrenceLimit = ref(0)

/** Whether a recurrence action is selected. */
const isRecurring = computed(() => invoiceAction.value === 'Recur')

/**
 * Fetches client services for the product/service dropdown.
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
 * Submits the billable item to the backend.
 *
 * @returns Promise that resolves when the item is saved.
 */
async function handleSave(): Promise<void> {
  internalSaving.value = true
  saveError.value = null
  try {
    await request(`/clients/${props.clientId}/billable-items`, {
      method: 'POST',
      body: JSON.stringify({
        serviceId: serviceId.value || null,
        description: description.value,
        hoursQty: hoursQty.value,
        isHours: isHours.value,
        amount: amount.value,
        invoiceAction: invoiceAction.value,
        dueDate: dueDate.value || null,
        invoiceCount: invoiceCount.value,
        recurrenceInterval: isRecurring.value ? recurrenceInterval.value : null,
        recurrencePeriod: isRecurring.value ? recurrencePeriod.value : null,
        recurrenceLimit: isRecurring.value ? (recurrenceLimit.value || null) : null,
      }),
    })
    emit('saved')
  } catch {
    saveError.value = 'Failed to save billable item.'
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
      <div class="bg-zinc-900 border border-zinc-700 rounded-xl shadow-2xl w-full max-w-lg p-6 space-y-4 max-h-[90vh] overflow-y-auto">
        <div class="flex items-center justify-between">
          <h2 class="text-white font-semibold text-lg">Add Billable Item</h2>
          <button class="text-zinc-400 hover:text-white transition" @click="emit('close')">&#10005;</button>
        </div>

        <!-- Error -->
        <div v-if="saveError" class="text-sm text-status-red bg-status-red/10 border border-status-red/20 rounded-lg p-3">
          {{ saveError }}
        </div>

        <form class="space-y-4" @submit.prevent="handleSave">
          <!-- Product/Service -->
          <div>
            <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-zinc-400 mb-1.5">Product/Service</label>
            <AppSelect
              v-model="serviceId"
              :options="[{ value: 0, label: 'None' }, ...serviceOptions]"
              placeholder="Select a service"
            />
          </div>

          <!-- Description -->
          <div>
            <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-zinc-400 mb-1.5">Description</label>
            <textarea
              v-model="description"
              rows="3"
              class="w-full bg-white/[0.04] border border-zinc-700 rounded-[10px] px-3 py-2 text-[0.82rem] text-white placeholder-zinc-500 focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors resize-none"
              placeholder="Describe the charge..."
            />
          </div>

          <!-- Hours/Qty + Amount -->
          <div class="grid grid-cols-2 gap-3">
            <div>
              <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-zinc-400 mb-1.5">Hours/Qty</label>
              <AppNumberInput v-model="hoursQty" :step="0.01" :min="0" />
              <div class="flex items-center gap-3 mt-2">
                <label class="flex items-center gap-1.5 text-[0.75rem] text-zinc-400 cursor-pointer">
                  <input v-model="isHours" type="radio" :value="true" name="hoursType" class="accent-primary-500" />
                  Hours
                </label>
                <label class="flex items-center gap-1.5 text-[0.75rem] text-zinc-400 cursor-pointer">
                  <input v-model="isHours" type="radio" :value="false" name="hoursType" class="accent-primary-500" />
                  Qty
                </label>
              </div>
            </div>
            <div>
              <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-zinc-400 mb-1.5">Amount</label>
              <AppNumberInput v-model="amount" :step="0.01" :min="0" />
            </div>
          </div>

          <!-- Invoice Action -->
          <div>
            <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-zinc-400 mb-1.5">Invoice Action</label>
            <div class="space-y-2">
              <label
                v-for="opt in INVOICE_ACTION_OPTIONS"
                :key="opt.value"
                class="flex items-center gap-2 text-[0.82rem] text-zinc-300 cursor-pointer"
              >
                <input
                  v-model="invoiceAction"
                  type="radio"
                  :value="opt.value"
                  name="invoiceAction"
                  class="accent-primary-500"
                />
                <span v-if="opt.value !== 'Recur'">{{ opt.label }}</span>
                <template v-else>
                  <span>Recur Every</span>
                  <div class="w-14">
                    <AppNumberInput v-model="recurrenceInterval" :min="1" :disabled="!isRecurring" />
                  </div>
                  <AppSelect
                    v-model="recurrencePeriod"
                    :options="RECURRENCE_PERIOD_OPTIONS"
                    :disabled="!isRecurring"
                  />
                  <span>for</span>
                  <div class="w-14">
                    <AppNumberInput v-model="recurrenceLimit" :min="0" :disabled="!isRecurring" placeholder="0" />
                  </div>
                  <span>Times</span>
                </template>
              </label>
            </div>
          </div>

          <!-- Due Date -->
          <div class="grid grid-cols-2 gap-3">
            <div>
              <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-zinc-400 mb-1.5">Due Date</label>
              <AppDatePicker v-model="dueDate" />
            </div>
            <div>
              <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-zinc-400 mb-1.5">Invoice Count</label>
              <AppNumberInput v-model="invoiceCount" :min="0" :disabled="true" />
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
              type="submit"
              :disabled="internalSaving"
              class="px-4 py-2 bg-blue-600 hover:bg-blue-500 text-white text-sm rounded-lg transition disabled:opacity-50"
            >{{ internalSaving ? 'Saving...' : 'Save' }}</button>
          </div>
        </form>
      </div>
    </div>
  </Teleport>
</template>
