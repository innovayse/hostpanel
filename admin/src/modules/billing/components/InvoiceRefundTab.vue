<script setup lang="ts">
/**
 * Invoice refund tab — matches WHMCS Refund layout.
 * Top: Transactions dropdown, Amount, Refund Type, conditional Transaction ID,
 * Reverse Payment checkbox, Send Email checkbox, Refund button.
 * Below: editable Invoice Items, Save/Cancel, Transactions + Transaction History tables.
 */
import { ref, computed, watch, onMounted } from 'vue'
import { useBillingStore } from '../stores/billingStore'
import { GATEWAY_OPTIONS } from '../../../utils/constants'
import { formatDate } from '../../../utils/format'
import AppSelect from '../../../components/AppSelect.vue'
import AppNumberInput from '../../../components/AppNumberInput.vue'
import AppCheckbox from '../../../components/AppCheckbox.vue'
import AppAlert from '../../../components/AppAlert.vue'
import type { Invoice } from '../../../types/models'

const props = defineProps<{
  /** The invoice to process refunds for. */
  invoice: Invoice
  /** When true, Invoice Items table is read-only. */
  readonly?: boolean
}>()

const emit = defineEmits<{
  /** Emitted after a refund is processed or items are saved. */
  updated: []
}>()

const store = useBillingStore()

// ── Refund form state ──
/** Selected payment transaction ID. */
const selectedTransactionId = ref('')
/** Refund amount (0 = full refund). */
const refundAmount = ref(0)
/** Refund type. */
const refundType = ref('Gateway')
/** External transaction ID for manual refunds. */
const refundTransactionId = ref('')
/** Reverse payment checkbox. */
const reversePayment = ref(false)
/** Send email checkbox. */
const sendEmail = ref(true)
/** True while processing. */
const processing = ref(false)
/** Credit action choice: 'remove' or 'leave' — must be selected before refund. */
const creditAction = ref('')

/** Refund type options. */
const refundTypeOptions = [
  { value: 'Gateway', label: 'Refund through Gateway' },
  { value: 'Manual', label: 'Manual Refund Processed Externally' },
  { value: 'CreditBalance', label: "Add to Client's Credit Balance" },
]

/** Payment transactions available for refund. */
const paymentTransactions = computed(() =>
  props.invoice.transactions?.filter(t => t.type === 'Payment') ?? [],
)

/** Transaction selector options. */
const transactionOptions = computed(() => {
  if (paymentTransactions.value.length === 0) {
    return [{ value: '', label: 'No Transactions Applied To This Invoice Yet' }]
  }
  return [
    { value: '', label: 'Select a transaction...' },
    ...paymentTransactions.value.map(t => ({
      value: String(t.id),
      label: `#${t.id} — $${t.amount.toFixed(2)} (${t.gateway})`,
    })),
  ]
})

/** All transactions. */
const allTransactions = computed(() => props.invoice.transactions ?? [])

/** Whether manual refund type is selected. */
const isManual = computed(() => refundType.value === 'Manual')

/** Whether the invoice is already refunded (disable form). */
const isRefunded = computed(() => props.invoice.status === 'Refunded')

/** Whether the invoice is a draft (cannot refund). */
const isDraft = computed(() => props.invoice.status === 'Draft')

/** Whether the invoice is cancelled (cannot refund). */
const isCancelled = computed(() => props.invoice.status === 'Cancelled')

/** Whether the refund form should be hidden. */
const formBlocked = computed(() => isDraft.value || isCancelled.value || isRefunded.value)

/** The selected transaction object. */
const selectedTransaction = computed(() =>
  paymentTransactions.value.find(t => String(t.id) === selectedTransactionId.value),
)

/** Whether the credit warning should be shown (when a transaction is selected). */
const showCreditWarning = computed(() => !!selectedTransaction.value)

/** Whether the refund button is enabled — requires a transaction AND a credit action choice. */
const canRefund = computed(() =>
  !!selectedTransactionId.value && !!creditAction.value && !processing.value,
)

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

/** Auto-fill amount and reset credit action when a transaction is selected. */
watch(selectedTransactionId, (newId) => {
  creditAction.value = ''
  const txn = paymentTransactions.value.find(t => String(t.id) === newId)
  if (txn) {
    refundAmount.value = txn.amount
  }
})

/**
 * Processes the refund.
 *
 * @returns Promise that resolves when the refund is completed.
 */
async function handleRefund(): Promise<void> {
  if (!selectedTransactionId.value || !creditAction.value) return
  processing.value = true
  const effectiveRefundType = creditAction.value === 'remove' ? 'CreditBalance' : refundType.value
  await store.refundPayment(props.invoice.id, {
    transactionId: selectedTransactionId.value,
    amount: refundAmount.value,
    refundType: effectiveRefundType,
    gateway: props.invoice.paymentMethod ?? 'None',
    refundTransactionId: isManual.value ? refundTransactionId.value : undefined,
    notes: undefined,
  })
  processing.value = false
  selectedTransactionId.value = ''
  refundAmount.value = 0
  refundType.value = 'Gateway'
  refundTransactionId.value = ''
  reversePayment.value = false
  sendEmail.value = true
  creditAction.value = ''
  emit('updated')
}

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
    id: i.id ?? undefined,
    description: i.description,
    unitPrice: i.amount,
    quantity: 1,
  }))
  await store.updateItems(props.invoice.id, items)
  savingItems.value = false
  emit('updated')
}

/** Cancels item changes. */
function cancelItems(): void {
  populateItems()
}

onMounted(() => populateItems())
</script>

<template>
  <div>
    <!-- View mode: warning if no eligible transactions -->
    <AppAlert v-if="readonly && paymentTransactions.length === 0" variant="warning" class="mb-5">
      There are no transactions that are eligible for a refund.
    </AppAlert>

    <!-- Blocked warnings -->
    <AppAlert v-if="!readonly && isDraft" variant="warning" class="mb-5">
      Publish the invoice before requesting a refund.
    </AppAlert>
    <AppAlert v-else-if="!readonly && isCancelled" variant="warning" class="mb-5">
      Cannot refund a cancelled invoice.
    </AppAlert>
    <AppAlert v-else-if="!readonly && isRefunded" variant="warning" class="mb-5">
      This invoice has already been refunded.
    </AppAlert>

    <!-- Refund form (edit mode only, hidden when blocked) -->
    <div v-if="!readonly && !formBlocked" class="bg-surface-card border border-border rounded-2xl p-5 mb-5">
      <div class="space-y-3">
        <div class="grid grid-cols-[140px_1fr] items-center gap-3">
          <label class="text-[0.82rem] text-text-secondary text-right">Transactions</label>
          <AppSelect v-model="selectedTransactionId" :options="transactionOptions" />
        </div>

        <div class="grid grid-cols-[140px_1fr] items-center gap-3">
          <label class="text-[0.82rem] text-text-secondary text-right">Amount</label>
          <div class="flex items-center gap-2">
            <div class="w-32">
              <AppNumberInput v-model="refundAmount" :step="0.01" :min="0" placeholder="0.00" />
            </div>
            <span class="text-[0.78rem] text-text-muted">Leave blank for full refund</span>
          </div>
        </div>

        <div class="grid grid-cols-[140px_1fr] items-center gap-3">
          <label class="text-[0.82rem] text-text-secondary text-right">Refund Type</label>
          <AppSelect v-model="refundType" :options="refundTypeOptions" />
        </div>

        <div v-if="isManual" class="grid grid-cols-[140px_1fr] items-center gap-3">
          <label class="text-[0.82rem] text-text-secondary text-right">Transaction ID</label>
          <input
            v-model="refundTransactionId"
            type="text"
            placeholder="External refund reference"
            class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2 text-[0.82rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
          />
        </div>

        <div class="grid grid-cols-[140px_1fr] items-center gap-3">
          <label class="text-[0.82rem] text-text-secondary text-right">Reverse Payment</label>
          <div class="flex items-center gap-2">
            <AppCheckbox v-model="reversePayment" />
            <span class="text-[0.78rem] text-text-muted">Undo automated actions triggered by this transaction - where possible.</span>
          </div>
        </div>

        <div class="grid grid-cols-[140px_1fr] items-center gap-3">
          <label class="text-[0.82rem] text-text-secondary text-right">Send Email</label>
          <div class="flex items-center gap-2">
            <AppCheckbox v-model="sendEmail" />
            <span class="text-[0.78rem] text-text-muted">Check to Send Confirmation Email</span>
          </div>
        </div>

        <!-- Credit warning with radio options -->
        <div v-if="showCreditWarning" class="grid grid-cols-[140px_1fr] items-start gap-3">
          <label class="text-[0.82rem] text-status-red text-right font-semibold pt-1">WARNING</label>
          <div class="space-y-2">
            <p class="text-[0.82rem] text-text-secondary">
              These transactions resulted in a credit being given to the user in the amount of
              <span class="font-medium text-text-primary">${{ (selectedTransaction?.amount ?? 0).toFixed(2) }} USD</span>.
              The current credit balance is
              <span class="font-medium text-text-primary">${{ (invoice.credit ?? 0).toFixed(2) }} USD</span>.
            </p>
            <p class="text-[0.82rem] text-text-muted">
              We can therefore automatically remove the amount from the user's credit balance.
            </p>
            <label class="flex items-center gap-2 cursor-pointer">
              <input
                v-model="creditAction"
                type="radio"
                value="remove"
                class="accent-primary-500"
              />
              <span class="text-[0.82rem] text-text-secondary">Click here to remove the amount from the credit balance.</span>
            </label>
            <label class="flex items-center gap-2 cursor-pointer">
              <input
                v-model="creditAction"
                type="radio"
                value="leave"
                class="accent-primary-500"
              />
              <span class="text-[0.82rem] text-text-secondary">Click here to leave the credit untouched.</span>
            </label>
          </div>
        </div>
      </div>

      <div class="flex justify-center mt-5">
        <button
          type="button"
          :disabled="!canRefund"
          class="px-6 py-2 text-[0.84rem] font-medium text-text-secondary hover:text-text-primary bg-white/[0.04] border border-border rounded-[10px] transition-colors disabled:opacity-50 disabled:pointer-events-none"
          @click="handleRefund"
        >
          {{ processing ? 'Processing...' : 'Refund' }}
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
              class="px-3 py-1.5 text-[0.78rem] font-medium text-white gradient-brand rounded-[9px] transition-opacity hover:opacity-90"
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
      <div v-else class="px-5 py-5 text-center text-[0.82rem] text-text-muted">No Records Found</div>
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
      <div v-else class="px-5 py-5 text-center text-[0.82rem] text-text-muted">No Records Found</div>
    </div>
  </div>
</template>
