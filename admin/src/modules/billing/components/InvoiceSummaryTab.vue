<script setup lang="ts">
/**
 * Invoice summary tab — matches WHMCS Summary layout.
 * Top left: Invoice info (Client Name, Invoice Date, Due Date, Amount, Balance).
 * Top right: Status badge, payment method, action buttons (Attempt Capture, Mark Cancelled, Mark Paid).
 * Below: editable Invoice Items, Save/Cancel, Transactions + Transaction History.
 */
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useBillingStore } from '../stores/billingStore'
import { INVOICE_STATUS_STYLES } from '../../../utils/constants'
import { formatDate } from '../../../utils/format'
import AppNumberInput from '../../../components/AppNumberInput.vue'
import AppCheckbox from '../../../components/AppCheckbox.vue'
import AppSelect from '../../../components/AppSelect.vue'
import type { Invoice } from '../../../types/models'

const props = defineProps<{
  /** The invoice to display. */
  invoice: Invoice
  /** When true, all tables are read-only — no editing controls. */
  readonly?: boolean
}>()

const emit = defineEmits<{
  /** Emitted after any mutation (publish, pay, cancel, items saved). */
  updated: []
}>()

const router = useRouter()
const store = useBillingStore()

// ── Action state ──
/** True while publishing. */
const publishing = ref(false)
/** True while attempting capture. */
const capturing = ref(false)
/** True while cancelling. */
const cancelling = ref(false)
/** True while marking paid. */
const markingPaid = ref(false)

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

/** Payment transactions. */
const paymentTransactions = computed(() =>
  props.invoice.transactions?.filter(t => t.type === 'Payment') ?? [],
)

/** All transactions. */
const allTransactions = computed(() => props.invoice.transactions ?? [])

/** Sum of payments. */
const paymentsTotal = computed(() =>
  paymentTransactions.value.reduce((sum, t) => sum + t.amount, 0),
)

/** Balance due. */
const balance = computed(() =>
  Math.max(0, (props.invoice.total ?? 0) - paymentsTotal.value),
)

/** Calculated preview sub-total. */
const previewSubTotal = computed(() => editItems.value.reduce((sum, i) => sum + i.amount, 0))
/** Preview total. */
const previewTotal = computed(() => previewSubTotal.value - (props.invoice.credit ?? 0))

/** Whether invoice is draft. */
const isDraft = computed(() => props.invoice.status === 'Draft')
/** Whether invoice is unpaid/overdue (can be paid). */
const canPay = computed(() => ['Unpaid', 'Overdue'].includes(props.invoice.status))
/** Whether invoice can be cancelled. */
const canCancel = computed(() => ['Draft', 'Unpaid', 'Overdue'].includes(props.invoice.status))

/**
 * Returns status badge class.
 *
 * @param status - Invoice status string.
 * @returns Tailwind class string.
 */
function statusClass(status: string): string {
  return INVOICE_STATUS_STYLES[status] ?? 'text-text-muted bg-white/[0.04] border-border'
}

/** Publishes a draft invoice. */
async function handlePublish(): Promise<void> {
  publishing.value = true
  await store.publishInvoice(props.invoice.id)
  publishing.value = false
  emit('updated')
}

/** Attempts to capture payment via gateway. */
async function handleCapture(): Promise<void> {
  capturing.value = true
  await store.payInvoice(props.invoice.id)
  capturing.value = false
  emit('updated')
}

/** Cancels the invoice. */
async function handleCancel(): Promise<void> {
  cancelling.value = true
  await store.cancelInvoice(props.invoice.id)
  cancelling.value = false
  emit('updated')
}

/** Marks the invoice as paid (manual, via bulk action). */
async function handleMarkPaid(): Promise<void> {
  markingPaid.value = true
  await store.bulkAction([props.invoice.id], 'MarkPaid')
  await store.fetchById(props.invoice.id)
  markingPaid.value = false
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

onMounted(() => populateItems())
</script>

<template>
  <div>
    <!-- Draft banner (edit mode only) -->
    <div v-if="isDraft && !readonly" class="bg-primary-500/10 border border-primary-500/20 rounded-2xl px-5 py-3 mb-5 flex items-center gap-3">
      <svg class="w-5 h-5 text-primary-400 shrink-0" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
        <circle cx="12" cy="12" r="10" /><line x1="12" y1="16" x2="12" y2="12" /><line x1="12" y1="8" x2="12.01" y2="8" />
      </svg>
      <span class="text-[0.82rem] text-primary-300">This is a Draft Invoice. The client is not able to see or access this invoice until it is published.</span>
    </div>

    <!-- Summary header -->
    <div class="bg-surface-card border border-border rounded-2xl p-5 mb-5">
      <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <!-- Left: Invoice info -->
        <div class="space-y-2.5">
          <div class="grid grid-cols-[140px_1fr] gap-2 items-center">
            <span class="text-[0.82rem] text-text-muted text-right">Client Name</span>
            <span class="text-[0.82rem] text-text-primary font-medium">
              {{ invoice.clientName }}
              <button
                type="button"
                class="text-primary-400 hover:underline ml-1 text-[0.78rem]"
                @click="router.push(`/clients/${invoice.clientId}/invoices`)"
              >
                ( View Invoices )
              </button>
            </span>
          </div>
          <div class="grid grid-cols-[140px_1fr] gap-2 items-center">
            <span class="text-[0.82rem] text-text-muted text-right">Invoice Date</span>
            <span class="text-[0.82rem] text-text-secondary">{{ formatDate(invoice.invoiceDate) }}</span>
          </div>
          <div class="grid grid-cols-[140px_1fr] gap-2 items-center">
            <span class="text-[0.82rem] text-text-muted text-right">Due Date</span>
            <span class="text-[0.82rem] text-text-secondary">{{ formatDate(invoice.dueDate) }}</span>
          </div>
          <div class="grid grid-cols-[140px_1fr] gap-2 items-center">
            <span class="text-[0.82rem] text-text-muted text-right">Invoice Amount</span>
            <span class="text-[0.82rem] text-text-secondary">${{ (invoice.total ?? 0).toFixed(2) }} USD</span>
          </div>
          <div class="grid grid-cols-[140px_1fr] gap-2 items-center">
            <span class="text-[0.82rem] text-text-muted text-right">Balance</span>
            <span class="text-[0.82rem] font-medium" :class="balance > 0 ? 'text-status-red' : 'text-status-green'">
              ${{ balance.toFixed(2) }} USD
            </span>
          </div>
        </div>

        <!-- Right: Status + Actions -->
        <div class="flex flex-col items-center gap-3">
          <span
            class="text-[1.2rem] font-bold uppercase tracking-wide"
            :class="statusClass(invoice.status).split(' ').filter(c => c.startsWith('text-'))[0]"
          >
            {{ invoice.status }}
          </span>

          <div class="text-[0.78rem] text-text-muted text-center space-y-0.5">
            <div>Last Capture Attempt: <span class="text-text-secondary">None</span></div>
            <div>Payment Method: <span class="text-text-secondary font-medium">{{ invoice.paymentMethod || 'None' }}</span></div>
          </div>

          <!-- Email action (placeholder, edit mode only) -->
          <div v-if="!readonly" class="flex items-center gap-2">
            <div class="w-44">
              <AppSelect
                model-value="InvoiceCreated"
                :options="[
                  { value: 'InvoiceCreated', label: 'Invoice Created' },
                  { value: 'PaymentReminder', label: 'Payment Reminder' },
                  { value: 'Overdue', label: 'Overdue Notice' },
                ]"
              />
            </div>
            <button
              type="button"
              class="px-3 py-2 text-[0.82rem] font-medium text-text-secondary hover:text-text-primary bg-white/[0.04] border border-border rounded-[10px] transition-colors"
            >
              Send Email
            </button>
          </div>

          <!-- Action buttons (edit mode only) -->
          <div v-if="!readonly" class="flex items-center gap-2 flex-wrap justify-center">
            <button
              v-if="isDraft"
              type="button"
              :disabled="publishing"
              class="gradient-brand px-4 py-2 text-[0.82rem] font-semibold text-white rounded-[10px] transition-opacity disabled:opacity-50"
              @click="handlePublish"
            >
              {{ publishing ? 'Publishing...' : 'Publish' }}
            </button>
            <button
              v-if="canPay"
              type="button"
              :disabled="capturing"
              class="px-4 py-2 text-[0.82rem] font-semibold text-white bg-status-green rounded-[10px] hover:bg-status-green/80 transition-colors disabled:opacity-50"
              @click="handleCapture"
            >
              {{ capturing ? 'Capturing...' : 'Attempt Capture' }}
            </button>
            <button
              v-if="canCancel"
              type="button"
              :disabled="cancelling"
              class="px-4 py-2 text-[0.82rem] font-medium text-text-secondary bg-white/[0.04] border border-border rounded-[10px] hover:text-text-primary transition-colors disabled:opacity-50"
              @click="handleCancel"
            >
              {{ cancelling ? 'Cancelling...' : 'Mark Cancelled' }}
            </button>
            <button
              v-if="canPay"
              type="button"
              :disabled="markingPaid"
              class="px-4 py-2 text-[0.82rem] font-semibold text-white gradient-brand rounded-[9px] transition-opacity hover:opacity-90 disabled:opacity-50"
              @click="handleMarkPaid"
            >
              {{ markingPaid ? 'Processing...' : 'Mark Paid' }}
            </button>
          </div>
        </div>
      </div>
    </div>

    <!-- Invoice Items (read-only in view mode) -->
    <div v-if="readonly" class="bg-surface-card border border-border rounded-2xl overflow-hidden mb-5">
      <h2 class="text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted px-5 py-3 border-b border-border bg-white/[0.02]">
        Invoice Items
      </h2>

      <div class="hidden sm:grid grid-cols-[1fr_auto_auto] gap-3 px-5 py-3 border-b border-border bg-white/[0.02] items-center">
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

      <div v-if="(invoice.items ?? []).length === 0" class="px-5 py-5 text-center text-[0.82rem] text-text-muted">
        No items.
      </div>

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
