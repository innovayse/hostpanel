<script setup lang="ts">
/**
 * Modal for adding a new financial transaction to a client account.
 * Two-column form with date, description, amounts, and payment method.
 */
import { ref } from 'vue'
import { useTransactionsStore } from '../stores/transactionsStore'
import { GATEWAY_OPTIONS } from '../../../utils/constants'
import { toDateInputValue } from '../../../utils/format'
import AppDatePicker from '../../../components/AppDatePicker.vue'
import AppNumberInput from '../../../components/AppNumberInput.vue'
import AppSelect from '../../../components/AppSelect.vue'
import AppCheckbox from '../../../components/AppCheckbox.vue'

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

const store = useTransactionsStore()

/** True while save is in progress. */
const internalSaving = ref(false)

/** Error message from the save operation. */
const saveError = ref<string | null>(null)

// --- Form fields ---

/** Transaction date in YYYY-MM-DD format. */
const date = ref(toDateInputValue(new Date().toISOString()))

/** Transaction description. */
const description = ref('')

/** External gateway transaction reference. */
const transactionId = ref('')

/** Optional linked invoice ID. */
const invoiceId = ref<number>(0)

/** Selected payment method. */
const paymentMethod = ref('None')

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
async function handleSave(): Promise<void> {
  internalSaving.value = true
  saveError.value = null
  try {
    const success = await store.create({
      clientId: Number(props.clientId),
      date: date.value || new Date().toISOString(),
      description: description.value,
      transactionId: transactionId.value,
      invoiceId: invoiceId.value || null,
      paymentMethod: paymentMethod.value,
      amountIn: amountIn.value,
      amountOut: amountOut.value,
      fees: fees.value,
      addedToCredit: addedToCredit.value,
    })
    if (success) {
      emit('saved')
    } else {
      saveError.value = store.error ?? 'Failed to save transaction.'
    }
  } catch {
    saveError.value = 'Failed to save transaction.'
  } finally {
    internalSaving.value = false
  }
}
</script>

<template>
  <Teleport to="body">
    <div
      class="fixed inset-0 z-50 flex items-center justify-center bg-black/60"
      @click.self="emit('close')"
    >
      <div class="bg-zinc-900 border border-zinc-700 rounded-xl shadow-2xl w-full max-w-2xl p-6 space-y-4 max-h-[90vh] overflow-y-auto">
        <div class="flex items-center justify-between">
          <h2 class="text-white font-semibold text-lg">Add New Transaction</h2>
          <button class="text-zinc-400 hover:text-white transition" @click="emit('close')">&#10005;</button>
        </div>

        <!-- Error -->
        <div v-if="saveError" class="text-sm text-status-red bg-status-red/10 border border-status-red/20 rounded-lg p-3">
          {{ saveError }}
        </div>

        <form class="space-y-4" @submit.prevent="handleSave">
          <div class="grid grid-cols-2 gap-4">
            <!-- Left column -->
            <div class="space-y-4">
              <!-- Date -->
              <div>
                <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-zinc-400 mb-1.5">Date</label>
                <AppDatePicker v-model="date" />
              </div>

              <!-- Description -->
              <div>
                <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-zinc-400 mb-1.5">Description</label>
                <input
                  v-model="description"
                  type="text"
                  class="w-full bg-white/[0.04] border border-zinc-700 rounded-[10px] px-3 py-2 text-[0.82rem] text-white placeholder-zinc-500 focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
                  placeholder="Transaction description..."
                />
              </div>

              <!-- Transaction ID -->
              <div>
                <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-zinc-400 mb-1.5">Transaction ID</label>
                <input
                  v-model="transactionId"
                  type="text"
                  class="w-full bg-white/[0.04] border border-zinc-700 rounded-[10px] px-3 py-2 text-[0.82rem] text-white placeholder-zinc-500 focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
                  placeholder="External reference..."
                />
              </div>

              <!-- Invoice ID -->
              <div>
                <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-zinc-400 mb-1.5">Invoice ID (optional)</label>
                <AppNumberInput v-model="invoiceId" :min="0" />
              </div>

              <!-- Payment Method -->
              <div>
                <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-zinc-400 mb-1.5">Payment Method</label>
                <AppSelect
                  v-model="paymentMethod"
                  :options="GATEWAY_OPTIONS"
                  placeholder="Select gateway"
                />
              </div>
            </div>

            <!-- Right column -->
            <div class="space-y-4">
              <!-- Amount In -->
              <div>
                <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-zinc-400 mb-1.5">Amount In</label>
                <AppNumberInput v-model="amountIn" :step="0.01" :min="0" />
              </div>

              <!-- Fees -->
              <div>
                <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-zinc-400 mb-1.5">Fees</label>
                <AppNumberInput v-model="fees" :step="0.01" :min="0" />
              </div>

              <!-- Amount Out -->
              <div>
                <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-zinc-400 mb-1.5">Amount Out</label>
                <AppNumberInput v-model="amountOut" :step="0.01" :min="0" />
              </div>

              <!-- Credit -->
              <div class="pt-2">
                <label class="flex items-center gap-2.5 text-[0.82rem] text-zinc-300 cursor-pointer">
                  <AppCheckbox v-model="addedToCredit" />
                  Add to Client's Credit Balance
                </label>
              </div>
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
