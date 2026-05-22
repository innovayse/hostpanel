<script setup lang="ts">
/**
 * Invoice options tab — matches WHMCS Options layout.
 * Top section: Invoice Date, Due Date, Payment Method, Tax Rate (dual fields),
 * Invoice #, Status dropdown, Save Changes button.
 * Below: editable Invoice Items with "With Selected" actions (Split / Delete),
 * Save Changes / Cancel Changes buttons, then Transactions + Transaction History tables.
 */
import { ref, computed, onMounted } from 'vue'
import { useBillingStore } from '../stores/billingStore'
import { GATEWAY_OPTIONS, INVOICE_STATUS_OPTIONS } from '../../../utils/constants'
import { formatDate, toDateInputValue } from '../../../utils/format'
import AppDatePicker from '../../../components/AppDatePicker.vue'
import AppNumberInput from '../../../components/AppNumberInput.vue'
import AppSelect from '../../../components/AppSelect.vue'
import AppCheckbox from '../../../components/AppCheckbox.vue'
import type { Invoice } from '../../../types/models'

const props = defineProps<{
  /** The invoice to update options for. */
  invoice: Invoice
}>()

const emit = defineEmits<{
  /** Emitted after options or items are saved. */
  updated: []
}>()

const store = useBillingStore()

// ── Options form state ──
/** Editable invoice date. */
const invoiceDate = ref('')
/** Editable due date. */
const dueDate = ref('')
/** Editable payment method. */
const paymentMethod = ref('')
/** Editable tax rate 1. */
const taxRate1 = ref(0)
/** Editable tax rate 2 (secondary tax). */
const taxRate2 = ref(0)
/** Editable invoice number (display only, not auto-assigned). */
const invoiceNumber = ref('')
/** Editable status. */
const status = ref('')
/** True while saving options. */
const savingOptions = ref(false)

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

/** Calculated preview. */
const previewSubTotal = computed(() => editItems.value.reduce((sum, i) => sum + i.amount, 0))
/** Calculated tax using combined rate. */
const combinedTaxRate = computed(() => taxRate1.value + taxRate2.value)
/** Tax on taxed items only. */
const previewTax = computed(() => {
  const taxableTotal = editItems.value.filter(i => i.taxed).reduce((sum, i) => sum + i.amount, 0)
  return taxableTotal * (combinedTaxRate.value / 100)
})
/** Preview total. */
const previewTotal = computed(() => previewSubTotal.value + previewTax.value - (props.invoice.credit ?? 0))

/** Populates all form fields from the invoice. */
function populateForm(): void {
  invoiceDate.value = toDateInputValue(props.invoice.invoiceDate)
  dueDate.value = toDateInputValue(props.invoice.dueDate)
  paymentMethod.value = props.invoice.paymentMethod ?? 'None'
  taxRate1.value = props.invoice.taxRate ?? 0
  taxRate2.value = 0
  invoiceNumber.value = String(props.invoice.id)
  status.value = props.invoice.status

  editItems.value = (props.invoice.items ?? []).map(i => ({
    id: i.id,
    description: i.description,
    amount: i.amount,
    taxed: (props.invoice.taxRate ?? 0) > 0,
    selected: false,
  }))
}

/**
 * Saves the options section.
 *
 * @returns Promise that resolves when saved.
 */
async function handleSaveOptions(): Promise<void> {
  savingOptions.value = true
  await store.updateOptions(props.invoice.id, {
    invoiceDate: invoiceDate.value,
    dueDate: dueDate.value,
    paymentMethod: paymentMethod.value,
    taxRate: combinedTaxRate.value,
  })
  savingOptions.value = false
  emit('updated')
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
  const selected = editItems.value.filter(i => i.selected)
  if (selected.length === 0) return

  if (withSelectedAction.value === 'delete') {
    editItems.value = editItems.value.filter(i => !i.selected)
  }
  // "split" would be a future feature — for now just reset
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

/** Cancels item changes and reloads from invoice. */
function cancelItems(): void {
  populateForm()
}

onMounted(() => populateForm())
</script>

<template>
  <div>
    <!-- Draft banner -->
    <div v-if="invoice.status === 'Draft'" class="bg-primary-500/10 border border-primary-500/20 rounded-2xl px-5 py-3 mb-5 flex items-center gap-3">
      <svg class="w-5 h-5 text-primary-400 shrink-0" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
        <circle cx="12" cy="12" r="10" /><line x1="12" y1="16" x2="12" y2="12" /><line x1="12" y1="8" x2="12.01" y2="8" />
      </svg>
      <span class="text-[0.82rem] text-primary-300">This is a Draft Invoice. The client is not able to see or access this invoice until it is published.</span>
    </div>

    <!-- Options form -->
    <div class="bg-surface-card border border-border rounded-2xl p-5 mb-5">
      <div class="grid grid-cols-1 sm:grid-cols-2 gap-3 mb-4">
        <div>
          <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Invoice Date</label>
          <AppDatePicker v-model="invoiceDate" />
        </div>
        <div>
          <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Due Date</label>
          <AppDatePicker v-model="dueDate" />
        </div>
        <div>
          <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Payment Method</label>
          <AppSelect v-model="paymentMethod" :options="GATEWAY_OPTIONS" />
        </div>
        <div>
          <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Tax Rate</label>
          <div class="flex items-center gap-2">
            <div class="flex items-center gap-1">
              <span class="text-[0.78rem] text-text-muted">1</span>
              <div class="w-20">
                <AppNumberInput v-model="taxRate1" :step="0.01" :min="0" />
              </div>
              <span class="text-[0.78rem] text-text-muted">%</span>
            </div>
            <div class="flex items-center gap-1">
              <span class="text-[0.78rem] text-text-muted">2</span>
              <div class="w-20">
                <AppNumberInput v-model="taxRate2" :step="0.01" :min="0" />
              </div>
              <span class="text-[0.78rem] text-text-muted">%</span>
            </div>
          </div>
        </div>
        <div>
          <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Invoice #</label>
          <input
            v-model="invoiceNumber"
            type="text"
            class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2 text-[0.82rem] text-text-primary focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
          />
        </div>
        <div>
          <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Status</label>
          <AppSelect v-model="status" :options="INVOICE_STATUS_OPTIONS.filter(o => o.value !== '')" />
        </div>
      </div>

      <div class="flex justify-center">
        <button
          type="button"
          :disabled="savingOptions"
          class="gradient-brand px-6 py-2 text-[0.84rem] font-semibold text-white rounded-[10px] transition-opacity disabled:opacity-50"
          @click="handleSaveOptions"
        >
          {{ savingOptions ? 'Saving...' : 'Save Changes' }}
        </button>
      </div>
    </div>

    <!-- Invoice Items (editable) -->
    <div class="bg-surface-card border border-border rounded-2xl overflow-hidden mb-5">
      <h2 class="text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted px-5 py-3 border-b border-border bg-white/[0.02]">
        Invoice Items
      </h2>

      <!-- Header -->
      <div class="hidden sm:grid grid-cols-[24px_1fr_auto_auto_32px] gap-3 px-5 py-3 border-b border-border bg-white/[0.02] items-center">
        <AppCheckbox :model-value="allSelected" @update:model-value="toggleSelectAll" />
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Description</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted text-right w-28">Amount</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted text-center w-16">Taxed</span>
        <span />
      </div>

      <!-- Editable rows -->
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

      <!-- Add Item -->
      <div class="flex justify-end px-5 py-3 border-b border-border">
        <button
          type="button"
          class="px-4 py-1.5 text-[0.82rem] font-medium text-text-secondary hover:text-text-primary bg-white/[0.04] border border-border rounded-lg transition-colors"
          @click="addItem"
        >
          Add Item
        </button>
      </div>

      <!-- With Selected + Summary -->
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

    <!-- Items action buttons -->
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
  </div>
</template>
