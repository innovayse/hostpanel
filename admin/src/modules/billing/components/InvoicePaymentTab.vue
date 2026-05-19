<script setup lang="ts">
/**
 * Invoice Add Payment tab — records payments against an invoice,
 * displays invoice items, transactions, and transaction history.
 * Matches WHMCS layout: form + Invoice Items + Transactions + Transaction History.
 */
import { ref, computed, onMounted } from 'vue'
import { useBillingStore } from '../stores/billingStore'
import { GATEWAY_OPTIONS } from '../../../utils/constants'
import { formatDate } from '../../../utils/format'
import AppDatePicker from '../../../components/AppDatePicker.vue'
import AppNumberInput from '../../../components/AppNumberInput.vue'
import AppSelect from '../../../components/AppSelect.vue'
import AppCheckbox from '../../../components/AppCheckbox.vue'
import type { Invoice } from '../../../types/models'

const props = defineProps<{
  /** The invoice to add payments to. */
  invoice: Invoice
  /** When true, Invoice Items table is read-only (form stays functional). */
  readonly?: boolean
}>()

const emit = defineEmits<{
  /** Emitted after a payment is added or items are saved. */
  updated: []
}>()

const store = useBillingStore()

/** Payment date. */
const paymentDate = ref('')

/** Selected payment method / gateway. */
const paymentMethod = ref('Stripe')

/** Gateway transaction reference. */
const transactionId = ref('')

/** Payment amount. */
const amount = ref(0)

/** Transaction fees. */
const fees = ref(0)

/** Whether to send a confirmation email. */
const sendEmail = ref(true)

/** True while submitting. */
const submitting = ref(false)

/** Whether this invoice is a draft. */
const isDraft = computed(() => props.invoice.status === 'Draft')

/** Payment transactions from the invoice. */
const paymentTransactions = computed(() =>
  props.invoice.transactions?.filter(t => t.type === 'Payment') ?? [],
)

/** All transactions (payment + refund + credit). */
const allTransactions = computed(() => props.invoice.transactions ?? [])

// ── Items editing state ──
/** Editable items copy. */
const editItems = ref<Array<{ id?: number; description: string; amount: number; taxed: boolean; selected: boolean }>>([])
/** True while saving items. */
const savingItems = ref(false)
/** "With Selected" action value. */
const withSelectedAction = ref('')
/** Whether select-all is checked. */
const allSelected = computed(() => editItems.value.length > 0 && editItems.value.every(i => i.selected))

/** With Selected options. */
const withSelectedOptions = [
  { value: '', label: '- With Selected -' },
  { value: 'split', label: 'Split to New Invoice' },
  { value: 'delete', label: 'Delete' },
]

/** Calculated preview sub-total. */
const previewSubTotal = computed(() => editItems.value.reduce((sum, i) => sum + i.amount, 0))
/** Preview total. */
const previewTotal = computed(() => previewSubTotal.value - (props.invoice.credit ?? 0))

/** Populates editable items from the invoice. */
function populateItems(): void {
  editItems.value = (props.invoice.items ?? []).map(i => ({
    id: i.id,
    description: i.description,
    amount: i.amount,
    taxed: (props.invoice.taxRate ?? 0) > 0,
    selected: false,
  }))
}

/** Adds a new blank line item. */
function addItem(): void {
  editItems.value.push({ description: '', amount: 0, taxed: false, selected: false })
}

/**
 * Removes a line item by index.
 *
 * @param index - Index to remove.
 */
function removeItem(index: number): void {
  editItems.value.splice(index, 1)
}

/** Toggles select-all checkbox. */
function toggleSelectAll(): void {
  const newVal = !allSelected.value
  editItems.value.forEach(i => i.selected = newVal)
}

/** Handles "With Selected" action. */
function handleWithSelected(): void {
  if (!withSelectedAction.value) return
  if (editItems.value.filter(i => i.selected).length === 0) return
  if (withSelectedAction.value === 'delete') {
    editItems.value = editItems.value.filter(i => !i.selected)
  }
  withSelectedAction.value = ''
}

/**
 * Saves edited items.
 *
 * @returns Promise that resolves when saved.
 */
async function saveItems(): Promise<void> {
  savingItems.value = true
  const items = editItems.value.map(i => ({
    id: i.id ?? null,
    description: i.description,
    unitPrice: i.amount,
    quantity: 1,
    isDeleted: false,
  }))
  await store.updateItems(props.invoice.id, items)
  savingItems.value = false
  emit('updated')
}

/** Cancels item changes. */
function cancelItems(): void {
  populateItems()
}

/**
 * Submits the payment form.
 *
 * @returns Promise that resolves when the payment is recorded.
 */
async function handleAddPayment(): Promise<void> {
  if (!paymentDate.value || amount.value <= 0) return
  submitting.value = true
  await store.addPayment(props.invoice.id, {
    date: new Date(paymentDate.value).toISOString(),
    gateway: paymentMethod.value,
    transactionId: transactionId.value,
    amount: amount.value,
    fees: fees.value,
  })
  submitting.value = false
  paymentDate.value = ''
  paymentMethod.value = 'Stripe'
  transactionId.value = ''
  amount.value = 0
  fees.value = 0
  sendEmail.value = true
  emit('updated')
}

onMounted(() => populateItems())
</script>

<template>
  <div>
    <!-- Draft blocked -->
    <div v-if="isDraft" class="bg-surface-card border border-border rounded-2xl p-5">
      <p class="text-[0.82rem] text-text-muted">Publish the invoice before adding payments.</p>
    </div>

    <template v-else>
      <!-- Add Payment form -->
      <div class="bg-surface-card border border-border rounded-2xl p-5 mb-5">
        <h2 class="text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-4">Add Payment</h2>

        <div class="grid grid-cols-1 sm:grid-cols-2 gap-3 mb-3">
          <div>
            <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Date</label>
            <AppDatePicker v-model="paymentDate" />
          </div>
          <div>
            <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Amount</label>
            <AppNumberInput v-model="amount" :step="0.01" :min="0" />
          </div>
          <div>
            <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Payment Method</label>
            <AppSelect v-model="paymentMethod" :options="GATEWAY_OPTIONS" />
          </div>
          <div>
            <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Transaction Fees</label>
            <AppNumberInput v-model="fees" :step="0.01" :min="0" />
          </div>
          <div>
            <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Transaction ID</label>
            <input
              v-model="transactionId"
              type="text"
              placeholder="Gateway reference"
              class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2 text-[0.82rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
            />
          </div>
          <div class="flex items-end pb-1">
            <label class="flex items-center gap-2 cursor-pointer select-none">
              <AppCheckbox v-model="sendEmail" />
              <span class="text-[0.82rem] text-text-secondary">Send Confirmation Email</span>
            </label>
          </div>
        </div>

        <div class="flex justify-center mt-4">
          <button
            type="button"
            :disabled="submitting || !paymentDate || amount <= 0"
            class="gradient-brand px-6 py-2 text-[0.84rem] font-semibold text-white rounded-[10px] transition-opacity disabled:opacity-50"
            @click="handleAddPayment"
          >
            {{ submitting ? 'Processing...' : 'Add Payment' }}
          </button>
        </div>
      </div>

      <!-- Invoice Items (read-only in view mode) -->
      <div v-if="readonly" class="bg-surface-card border border-border rounded-2xl overflow-hidden mb-5">
        <h2 class="text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted px-5 py-3 border-b border-border bg-white/[0.02]">
          Invoice Items
        </h2>
        <div class="hidden sm:grid grid-cols-[1fr_auto_auto] gap-3 px-5 py-3 border-b border-border bg-white/[0.02]">
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Description</span>
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted text-right w-28">Amount</span>
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted text-center w-16">Taxed</span>
        </div>
        <div
          v-for="item in invoice.items ?? []"
          :key="item.id"
          class="grid grid-cols-1 sm:grid-cols-[1fr_auto_auto] gap-2 sm:gap-3 px-5 py-3.5 border-b border-border last:border-0 items-center"
        >
          <span class="text-[0.82rem] text-text-primary">{{ item.description }}</span>
          <span class="text-[0.82rem] text-text-secondary text-right w-28">${{ item.amount.toFixed(2) }}</span>
          <span class="text-[0.82rem] text-text-muted text-center w-16">{{ (invoice.taxRate ?? 0) > 0 ? 'Yes' : 'No' }}</span>
        </div>
        <div v-if="(invoice.items ?? []).length === 0" class="px-5 py-5 text-center text-[0.82rem] text-text-muted">No items.</div>
        <div class="px-5 py-3 space-y-2">
          <div class="flex justify-end gap-4">
            <span class="text-[0.82rem] text-text-muted">Sub Total:</span>
            <span class="text-[0.82rem] text-text-primary font-medium w-28 text-right">${{ (invoice.subTotal ?? 0).toFixed(2) }}</span>
          </div>
          <div class="flex justify-end gap-4">
            <span class="text-[0.82rem] text-text-muted">Credit:</span>
            <span class="text-[0.82rem] text-text-primary w-28 text-right">${{ (invoice.credit ?? 0).toFixed(2) }}</span>
          </div>
          <div class="flex justify-end gap-4 pt-2 border-t border-border">
            <span class="text-[0.82rem] text-text-primary font-semibold">Total Due:</span>
            <span class="text-[0.82rem] text-text-primary font-bold w-28 text-right">${{ (invoice.total ?? 0).toFixed(2) }}</span>
          </div>
        </div>
      </div>

      <!-- Invoice Items (editable in edit mode) -->
      <template v-else>
        <div class="bg-surface-card border border-border rounded-2xl overflow-hidden mb-5">
          <h2 class="text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted px-5 py-3 border-b border-border bg-white/[0.02]">
            Invoice Items
          </h2>
          <div class="hidden sm:grid grid-cols-[24px_1fr_auto_auto_32px] gap-3 px-5 py-3 border-b border-border bg-white/[0.02] items-center">
            <AppCheckbox :model-value="allSelected" @update:model-value="toggleSelectAll()" />
            <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Description</span>
            <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted text-right w-28">Amount</span>
            <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted text-center w-16">Taxed</span>
            <span />
          </div>
          <div
            v-for="(item, idx) in editItems"
            :key="idx"
            class="grid grid-cols-1 sm:grid-cols-[24px_1fr_auto_auto_32px] gap-2 sm:gap-3 px-5 py-2.5 border-b border-border last:border-0 items-center"
          >
            <AppCheckbox v-model="item.selected" />
            <textarea
              v-model="item.description"
              rows="1"
              placeholder="Description"
              class="w-full bg-white/[0.04] border border-border rounded-lg px-2.5 py-2 text-[0.82rem] text-text-primary focus:outline-none focus:border-primary-500/50 transition-colors resize-y min-h-[36px]"
            />
            <div class="w-28">
              <AppNumberInput v-model="item.amount" :step="0.01" :min="0" />
            </div>
            <div class="flex items-center justify-center w-16">
              <AppCheckbox v-model="item.taxed" />
            </div>
            <button
              type="button"
              class="flex items-center justify-center text-status-red hover:text-status-red/80 transition-colors"
              @click="removeItem(idx)"
            >
              <svg class="w-4 h-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <circle cx="12" cy="12" r="10" /><line x1="15" y1="9" x2="9" y2="15" /><line x1="9" y1="9" x2="15" y2="15" />
              </svg>
            </button>
          </div>
          <div class="flex justify-end px-5 py-3 border-b border-border">
            <button
              type="button"
              class="px-4 py-1.5 text-[0.82rem] font-medium text-text-secondary hover:text-text-primary bg-white/[0.04] border border-border rounded-lg transition-colors"
              @click="addItem"
            >
              Add Item
            </button>
          </div>
          <div class="px-5 py-3 space-y-2">
            <div class="flex items-center gap-2">
              <div class="w-40">
                <AppSelect v-model="withSelectedAction" :options="withSelectedOptions" />
              </div>
              <button
                v-if="withSelectedAction"
                type="button"
                class="px-3 py-1.5 text-[0.78rem] font-medium text-white bg-primary-500 rounded-lg hover:bg-primary-400 transition-colors"
                @click="handleWithSelected"
              >
                Go
              </button>
            </div>
            <div class="flex justify-end gap-4">
              <span class="text-[0.82rem] text-text-muted">Sub Total:</span>
              <span class="text-[0.82rem] text-text-primary font-medium w-28 text-right">${{ previewSubTotal.toFixed(2) }}</span>
            </div>
            <div class="flex justify-end gap-4">
              <span class="text-[0.82rem] text-text-muted">Credit:</span>
              <span class="text-[0.82rem] text-text-primary w-28 text-right">${{ (invoice.credit ?? 0).toFixed(2) }}</span>
            </div>
            <div class="flex justify-end gap-4 pt-2 border-t border-border">
              <span class="text-[0.82rem] text-text-primary font-semibold">Total Due:</span>
              <span class="text-[0.82rem] text-text-primary font-bold w-28 text-right">${{ previewTotal.toFixed(2) }}</span>
            </div>
          </div>
        </div>

        <div class="flex justify-center gap-2.5 mb-5">
          <button
            type="button"
            :disabled="savingItems"
            class="gradient-brand px-6 py-2 text-[0.84rem] font-semibold text-white rounded-[10px] transition-opacity disabled:opacity-50"
            @click="saveItems"
          >
            {{ savingItems ? 'Saving...' : 'Save Changes' }}
          </button>
          <button
            type="button"
            class="px-5 py-2 text-[0.84rem] font-medium text-text-secondary hover:text-text-primary bg-white/[0.04] border border-border rounded-[10px] transition-colors"
            @click="cancelItems"
          >
            Cancel Changes
          </button>
        </div>
      </template>

      <!-- Transactions -->
      <div class="bg-surface-card border border-border rounded-2xl overflow-hidden mb-5">
        <h2 class="text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted px-5 py-3 border-b border-border bg-white/[0.02]">
          Transactions
        </h2>

        <div class="hidden sm:grid grid-cols-[1fr_1fr_1fr_0.8fr_0.8fr] gap-3 px-5 py-3 border-b border-border bg-white/[0.02]">
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Date</span>
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Payment Method</span>
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Transaction ID</span>
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Amount</span>
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Transaction Fees</span>
        </div>

        <template v-if="paymentTransactions.length > 0">
          <div
            v-for="txn in paymentTransactions"
            :key="txn.id"
            class="grid grid-cols-1 sm:grid-cols-[1fr_1fr_1fr_0.8fr_0.8fr] gap-2 sm:gap-3 px-5 py-3.5 border-b border-border last:border-0"
          >
            <span class="text-[0.82rem] text-text-secondary">{{ formatDate(txn.date) }}</span>
            <span class="text-[0.82rem] text-text-secondary">{{ txn.gateway }}</span>
            <span class="text-[0.82rem] text-text-muted font-mono truncate">{{ txn.transactionId || '—' }}</span>
            <span class="text-[0.82rem] text-status-green font-medium">${{ txn.amount.toFixed(2) }}</span>
            <span class="text-[0.82rem] text-text-muted">${{ (txn.fees ?? 0).toFixed(2) }}</span>
          </div>
        </template>

        <div v-else class="px-5 py-5 text-center text-[0.82rem] text-text-muted">
          No Records Found
        </div>
      </div>

      <!-- Transaction History -->
      <div class="bg-surface-card border border-border rounded-2xl overflow-hidden">
        <h2 class="text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted px-5 py-3 border-b border-border bg-white/[0.02]">
          Transaction History
        </h2>

        <div class="hidden sm:grid grid-cols-[0.8fr_1fr_1fr_0.7fr_1fr] gap-3 px-5 py-3 border-b border-border bg-white/[0.02]">
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Date</span>
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Payment Method</span>
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Transaction ID</span>
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Status</span>
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Description</span>
        </div>

        <template v-if="allTransactions.length > 0">
          <div
            v-for="txn in allTransactions"
            :key="txn.id"
            class="grid grid-cols-1 sm:grid-cols-[0.8fr_1fr_1fr_0.7fr_1fr] gap-2 sm:gap-3 px-5 py-3.5 border-b border-border last:border-0"
          >
            <span class="text-[0.82rem] text-text-secondary">{{ formatDate(txn.date) }}</span>
            <span class="text-[0.82rem] text-text-secondary">{{ txn.gateway }}</span>
            <span class="text-[0.82rem] text-text-muted font-mono truncate">{{ txn.transactionId || '—' }}</span>
            <span
              class="inline-block px-2 py-0.5 rounded-full text-[0.7rem] font-medium border w-fit"
              :class="txn.type === 'Payment' ? 'text-status-green bg-status-green/10 border-status-green/20'
                : txn.type === 'Refund' ? 'text-status-red bg-status-red/10 border-status-red/20'
                : 'text-status-yellow bg-status-yellow/10 border-status-yellow/20'"
            >
              {{ txn.type }}
            </span>
            <span class="text-[0.82rem] text-text-muted">{{ txn.notes || '—' }}</span>
          </div>
        </template>

        <div v-else class="px-5 py-5 text-center text-[0.82rem] text-text-muted">
          No Records Found
        </div>
      </div>
    </template>
  </div>
</template>
