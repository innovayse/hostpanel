<script setup lang="ts">
<<<<<<< HEAD
import { onMounted, ref, computed } from 'vue'
import { useRouter } from 'vue-router'
import { useBillingStore } from '../stores/billingStore'
import AppSelect from '../../../components/AppSelect.vue'
import AppClientSelect from '../../../components/AppClientSelect.vue'
import AppSpinner from '../../../components/AppSpinner.vue'
import AppCheckbox from '../../../components/AppCheckbox.vue'
import DateRangePicker from '../../../components/DateRangePicker.vue'
=======
/**
 * Global invoices list view -- displays all billing invoices with filtering,
 * bulk actions, pagination, and navigation to detail view.
 */
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useBillingStore } from '../stores/billingStore'
import { INVOICE_STATUS_OPTIONS, INVOICE_STATUS_STYLES } from '../../../utils/constants'
import { formatDate, toDateInputValue } from '../../../utils/format'
import AppDatePicker from '../../../components/AppDatePicker.vue'
import AppNumberInput from '../../../components/AppNumberInput.vue'
import AppSelect from '../../../components/AppSelect.vue'
import AppCheckbox from '../../../components/AppCheckbox.vue'
import ConfirmModal from '../../../components/ConfirmModal.vue'
>>>>>>> origin/main

const router = useRouter()
const store = useBillingStore()
const router = useRouter()
const page = ref(1)
const isFilterOpen = ref(false)
const selectedInvoices = ref<Set<number>>(new Set())
const clients = ref<{ id: number; name: string; email?: string; status?: string }[]>([])

<<<<<<< HEAD
const statusOptions = [
  { value: 'all', label: 'All Statuses' },
  { value: 'Draft', label: 'Draft' },
  { value: 'Unpaid', label: 'Unpaid' },
  { value: 'Paid', label: 'Paid' },
  { value: 'Overdue', label: 'Overdue' },
  { value: 'Refunded', label: 'Refunded' },
  { value: 'Collections', label: 'Collections' },
  { value: 'PaymentPending', label: 'Payment Pending' },
]

const statusColorMap = {
  'Paid': 'bg-status-green/15 text-status-green',
  'Overdue': 'bg-status-red/15 text-status-red',
  'Unpaid': 'bg-status-yellow/15 text-status-yellow',
  'Draft': 'bg-primary-500/15 text-primary-400',
  'Cancelled': 'bg-text-muted/15 text-text-muted',
  'Refunded': 'bg-primary-500/15 text-primary-400',
  'Collections': 'bg-status-red/15 text-status-red',
  'PaymentPending': 'bg-status-yellow/15 text-status-yellow'
}

// Filters
const filterStatus = ref('all')
const filterClientName = ref('')
const filterInvoiceId = ref('')
const filterInvoiceDateRange = ref<[string, string] | null>(null)
const filterDueDateRange = ref<[string, string] | null>(null)
const filterDatePaidRange = ref<[string, string] | null>(null)
const filterDateRefundedRange = ref<[string, string] | null>(null)
const filterDateCancelledRange = ref<[string, string] | null>(null)
const filterLineItemDescription = ref('')
const filterPaymentMethod = ref('')
const filterLastCaptureAttemptRange = ref<[string, string] | null>(null)
const filterTotalDueFrom = ref(0)
const filterTotalDueTo = ref(0)

const totalPages = computed(() => Math.max(1, Math.ceil(store.totalCount / store.pageSize)))

const pageString = computed({
  get: () => String(page.value),
  set: (val) => { page.value = Number(val) }
})

const pageOptions = computed(() => {
  const options = []
  for (let i = 1; i <= totalPages.value; i++) {
    options.push({ value: String(i), label: String(i) })
  }
  return options
})

const stats = computed(() => {
  const unpaid = store.invoices
    .filter(inv => inv.status === 'Unpaid' || inv.status === 'PaymentPending')
    .reduce((sum, inv) => sum + inv.total, 0)

  const overdue = store.invoices
    .filter(inv => inv.status === 'Overdue')
    .reduce((sum, inv) => sum + inv.total, 0)

  const paid = store.invoices
    .filter(inv => inv.status === 'Paid')
    .reduce((sum, inv) => sum + inv.total, 0)

  return { unpaid, overdue, paid }
})

function applyFilters(): void {
  page.value = 1
  store.fetchAll(1, store.pageSize)
}

function clearFilters(): void {
  filterStatus.value = 'all'
  filterClientName.value = ''
  filterInvoiceId.value = ''
  filterInvoiceDateRange.value = null
  filterDueDateRange.value = null
  filterDatePaidRange.value = null
  filterDateRefundedRange.value = null
  filterDateCancelledRange.value = null
  filterLineItemDescription.value = ''
  filterPaymentMethod.value = ''
  filterLastCaptureAttemptRange.value = null
  filterTotalDueFrom.value = 0
  filterTotalDueTo.value = 0
  page.value = 1
  store.fetchAll(1, store.pageSize)
}

function goToPage(p: number): void {
  if (p < 1 || p > totalPages.value) return
  page.value = p
  store.fetchAll(p, store.pageSize)
}

function toggleInvoiceSelection(invoiceId: number): void {
  if (selectedInvoices.value.has(invoiceId)) {
    selectedInvoices.value.delete(invoiceId)
  } else {
    selectedInvoices.value.add(invoiceId)
  }
}

function toggleSelectAll(): void {
  if (selectedInvoices.value.size === store.invoices.length && store.invoices.length > 0) {
    selectedInvoices.value.clear()
  } else {
    store.invoices.forEach(inv => selectedInvoices.value.add(inv.id))
  }
}

function loadClients() {
  clients.value = [
    { id: 1, name: 'John Doe', email: 'john@example.com', status: 'active' },
    { id: 2, name: 'Jane Smith (Acme Corp)', email: 'jane@example.com', status: 'active' },
    { id: 3, name: 'Bob Johnson', email: 'bob@example.com', status: 'active' },
    { id: 4, name: 'Alice Williams (Tech Inc)', email: 'alice@example.com', status: 'inactive' },
  ]
}

onMounted(() => {
  store.fetchAll(1, store.pageSize)
  loadClients()
})
=======
/** Current page (1-based). */
const page = ref(1)

/** Items per page. */
const pageSize = ref(25)

/** Status filter value. */
const filterStatus = ref('')

/** Date from filter. */
const filterFrom = ref('')

/** Date to filter. */
const filterTo = ref('')

/** Whether the select-all checkbox is checked. */
const selectAll = ref(false)

/** Set of selected invoice IDs. */
const selectedIds = ref<Set<number>>(new Set())

/** Bulk action dropdown value. */
const bulkActionValue = ref('')

/** Whether the bulk confirm modal is visible. */
const showBulkConfirmModal = ref(false)

/** Whether the create invoice modal is visible. */
const showCreateModal = ref(false)

/** New invoice client ID. */
const newClientId = ref<number | null>(null)

/** New invoice due date. */
const newDueDate = ref('')

/** New invoice items. */
const newItems = ref<Array<{ description: string; unitPrice: number; quantity: number }>>([
  { description: '', unitPrice: 0, quantity: 1 },
])

/** Whether to create as draft. */
const newIsDraft = ref(true)

/** True while creating an invoice. */
const creating = ref(false)

/** Total pages based on total count and page size. */
const totalPages = computed(() => Math.max(1, Math.ceil(store.totalCount / pageSize.value)))

/** Whether any items are selected. */
const hasSelection = computed(() => selectedIds.value.size > 0)

/** Bulk action options for the dropdown. */
const bulkActions = [
  { value: '', label: 'Bulk Actions...' },
  { value: 'MarkPaid', label: 'Mark Paid' },
  { value: 'MarkUnpaid', label: 'Mark Unpaid' },
  { value: 'MarkCancelled', label: 'Mark Cancelled' },
  { value: 'Duplicate', label: 'Duplicate' },
  { value: 'Delete', label: 'Delete' },
]

/**
 * Fetches invoices with current filters and pagination.
 *
 * @returns Promise that resolves when data is loaded.
 */
async function fetchInvoices(): Promise<void> {
  await store.fetchAll(page.value, pageSize.value, filterStatus.value, filterFrom.value, filterTo.value)
}

/** Applies filters and resets to page 1. */
function applyFilters(): void {
  page.value = 1
  selectedIds.value = new Set()
  selectAll.value = false
  fetchInvoices()
}

/**
 * Navigates to a specific page.
 *
 * @param p - Target page number.
 */
function goToPage(p: number): void {
  if (p < 1 || p > totalPages.value) return
  page.value = p
  selectedIds.value = new Set()
  selectAll.value = false
  fetchInvoices()
}

/** Toggles selection of all invoices on the current page. */
function toggleSelectAll(): void {
  if (selectAll.value) {
    selectedIds.value = new Set()
    selectAll.value = false
  } else {
    selectedIds.value = new Set(store.invoices.map(i => i.id))
    selectAll.value = true
  }
}

/**
 * Toggles selection of a single invoice.
 *
 * @param id - The invoice ID to toggle.
 */
function toggleSelect(id: number): void {
  const next = new Set(selectedIds.value)
  if (next.has(id)) {
    next.delete(id)
  } else {
    next.add(id)
  }
  selectedIds.value = next
  selectAll.value = next.size === store.invoices.length && store.invoices.length > 0
}

/** Confirms and executes the selected bulk action. */
async function executeBulkAction(): Promise<void> {
  showBulkConfirmModal.value = false
  if (!bulkActionValue.value || selectedIds.value.size === 0) return
  await store.bulkAction(Array.from(selectedIds.value), bulkActionValue.value)
  selectedIds.value = new Set()
  selectAll.value = false
  bulkActionValue.value = ''
  await fetchInvoices()
}

/** Opens the bulk action confirmation modal. */
function handleBulkApply(): void {
  if (!bulkActionValue.value || selectedIds.value.size === 0) return
  showBulkConfirmModal.value = true
}

/** Opens the create invoice modal. */
function openCreateModal(): void {
  newClientId.value = null
  newDueDate.value = ''
  newItems.value = [{ description: '', unitPrice: 0, quantity: 1 }]
  newIsDraft.value = true
  showCreateModal.value = true
}

/** Adds a blank line item row. */
function addNewItem(): void {
  newItems.value.push({ description: '', unitPrice: 0, quantity: 1 })
}

/**
 * Removes a line item row from the create form.
 *
 * @param index - Index of the item to remove.
 */
function removeNewItem(index: number): void {
  newItems.value.splice(index, 1)
}

/**
 * Creates the invoice and navigates to its detail page.
 *
 * @returns Promise that resolves when created.
 */
async function handleCreate(): Promise<void> {
  if (!newClientId.value) return
  creating.value = true
  const id = await store.createInvoice(newClientId.value, newDueDate.value, newItems.value, newIsDraft.value)
  creating.value = false
  if (id) {
    showCreateModal.value = false
    router.push(`/billing/${id}`)
  }
}

/** Invoice ID pending deletion. */
const deleteTargetId = ref<number | null>(null)

/** Whether the delete confirm modal is visible. */
const showDeleteModal = ref(false)

/**
 * Opens the delete confirmation modal for an invoice.
 *
 * @param id - The invoice ID to delete.
 */
function confirmDelete(id: number): void {
  deleteTargetId.value = id
  showDeleteModal.value = true
}

/**
 * Deletes the targeted invoice and refreshes the list.
 *
 * @returns Promise that resolves when deletion completes.
 */
async function executeDelete(): Promise<void> {
  if (!deleteTargetId.value) return
  await store.deleteInvoice(deleteTargetId.value)
  showDeleteModal.value = false
  deleteTargetId.value = null
  await fetchInvoices()
}

/**
 * Returns the CSS class string for a given invoice status badge.
 *
 * @param status - The invoice status string.
 * @returns Tailwind class string.
 */
function statusClass(status: string): string {
  return INVOICE_STATUS_STYLES[status] ?? 'text-text-muted bg-white/[0.04] border-border'
}

onMounted(() => fetchInvoices())
>>>>>>> origin/main
</script>

<template>
  <div class="p-4 sm:p-6 lg:p-8 w-full">

    <!-- Header -->
<<<<<<< HEAD
    <div class="mb-5">
      <h1 class="font-display font-bold text-[1.25rem] text-text-primary leading-none">Invoices</h1>
      <p class="text-[0.78rem] text-text-secondary mt-1">Manage billing invoices</p>
    </div>

    <!-- Search/Filter Toggle -->
    <div class="mb-4">
      <button
        @click="isFilterOpen = !isFilterOpen"
        class="px-4 py-2 border border-border rounded-[9px] text-[0.82rem] font-semibold text-text-primary hover:bg-white/[0.05] transition-colors"
      >
        Search/Filter
      </button>
    </div>


    <!-- Filter Bar -->
    <div v-if="isFilterOpen" class="bg-surface-card border border-border rounded-2xl p-4 mb-4">
      <!-- Filter in 2 columns -->
      <div class="grid grid-cols-1 lg:grid-cols-2 gap-6 mb-4">
        <!-- Left Column -->
        <div class="space-y-4">
          <div>
            <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Client Name</label>
            <AppClientSelect
              v-model="filterClientName"
              :clients="clients"
              placeholder="Start Typing to Search Clients"
            />
          </div>

          <div>
            <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Invoice #</label>
            <input v-model="filterInvoiceId" type="text" placeholder="Search..." class="w-full px-3 py-2 text-[0.82rem] text-text-primary bg-white/[0.05] border border-border rounded-[9px] placeholder:text-text-muted focus:outline-none focus:border-primary-500/40 transition-colors" />
          </div>

          <div>
            <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Line Item Description</label>
            <input v-model="filterLineItemDescription" type="text" placeholder="Search..." class="w-full px-3 py-2 text-[0.82rem] text-text-primary bg-white/[0.05] border border-border rounded-[9px] placeholder:text-text-muted focus:outline-none focus:border-primary-500/40 transition-colors" />
          </div>

          <div>
            <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Payment Method</label>
            <AppSelect v-model="filterPaymentMethod" :options="[{ value: '', label: 'Any' }, { value: 'Stripe', label: 'Stripe' }, { value: 'PayPal', label: 'PayPal' }, { value: 'Credit/Debit Card', label: 'Credit/Debit Card' }]" />
          </div>

          <div>
            <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Status</label>
            <AppSelect v-model="filterStatus" :options="statusOptions" />
          </div>

          <div>
            <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Total Due</label>
            <div class="flex gap-2 items-center">
              <span class="text-[0.78rem] text-text-muted">From</span>
              <AppSpinner v-model="filterTotalDueFrom" :step="0.01" :min="0" placeholder="0.00" class="flex-1" />
              <span class="text-[0.78rem] text-text-muted">To</span>
              <AppSpinner v-model="filterTotalDueTo" :step="0.01" :min="0" placeholder="0.00" class="flex-1" />
            </div>
          </div>
        </div>

        <!-- Right Column -->
        <div class="space-y-4">
          <div>
            <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Invoice Date</label>
            <DateRangePicker v-model="filterInvoiceDateRange" placeholder="Select date range..." />
          </div>

          <div>
            <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Due Date</label>
            <DateRangePicker v-model="filterDueDateRange" placeholder="Select date range..." />
          </div>

          <div>
            <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Date Paid</label>
            <DateRangePicker v-model="filterDatePaidRange" placeholder="Select date range..." />
          </div>

          <div>
            <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Last Capture Attempt</label>
            <DateRangePicker v-model="filterLastCaptureAttemptRange" placeholder="Select date range..." />
          </div>

          <div>
            <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Date Refunded</label>
            <DateRangePicker v-model="filterDateRefundedRange" placeholder="Select date range..." />
          </div>

          <div>
            <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Date Cancelled</label>
            <DateRangePicker v-model="filterDateCancelledRange" placeholder="Select date range..." />
          </div>
        </div>
      </div>

      <div class="flex items-center justify-center gap-2">
        <button class="gradient-brand flex items-center gap-1.5 px-6 py-2 text-[0.82rem] font-semibold text-white rounded-[9px] transition-opacity hover:opacity-90" @click="applyFilters">
          <svg class="w-3.5 h-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round">
            <circle cx="11" cy="11" r="8"/><line x1="21" y1="21" x2="16.65" y2="16.65"/>
          </svg>
          Search
        </button>
        <button class="px-4 py-2 text-[0.78rem] text-text-muted hover:text-text-primary transition-colors" @click="clearFilters">
          Clear
        </button>
      </div>
=======
    <div class="flex items-center justify-between gap-2.5 mb-5">
      <h1 class="text-[0.875rem] font-semibold text-text-primary">Invoices</h1>
      <button
        type="button"
        class="gradient-brand px-5 py-2 text-[0.84rem] font-semibold text-white rounded-[10px] transition-opacity"
        @click="openCreateModal"
      >
        + Create Invoice
      </button>
    </div>

    <!-- Filter bar -->
    <div class="flex items-center gap-2.5 mb-5 flex-wrap">
      <AppSelect v-model="filterStatus" :options="INVOICE_STATUS_OPTIONS" placeholder="All Statuses" />
      <AppDatePicker v-model="filterFrom" placeholder="From" />
      <AppDatePicker v-model="filterTo" placeholder="To" />
      <button
        type="button"
        class="px-4 py-2.5 text-[0.82rem] font-medium text-text-secondary hover:text-text-primary bg-white/[0.04] border border-border rounded-[10px] transition-colors"
        @click="applyFilters"
      >
        Filter
      </button>
    </div>

    <!-- Error -->
    <div v-if="store.error" class="text-sm text-status-red bg-status-red/8 border border-status-red/20 rounded-xl p-4 mb-5">
      {{ store.error }}
>>>>>>> origin/main
    </div>

    <!-- Loading -->
    <div v-if="store.loading && store.invoices.length === 0" class="flex items-center gap-3 text-text-secondary text-sm">
      <span class="w-4 h-4 rounded-full border-2 border-primary-500/20 border-t-primary-500 animate-spin" />
<<<<<<< HEAD
      Loading invoices…
    </div>

    <!-- Error -->
    <div v-else-if="store.error" class="text-sm text-status-red bg-status-red/8 border border-status-red/20 rounded-xl p-4">
      {{ store.error }}
    </div>

    <!-- Record Count & Pagination Info -->
    <div v-if="store.invoices.length > 0" class="flex items-center justify-between mb-4 text-[0.82rem] text-text-secondary">
      <div>
        {{ store.totalCount }} Records Found, Showing {{ (page - 1) * store.pageSize + 1 }} to {{ Math.min(page * store.pageSize, store.totalCount) }}
      </div>
      <div class="flex items-center gap-2">
        <span>Jump to Page:</span>
        <div class="w-20">
          <AppSelect
            v-model="pageString"
            :options="pageOptions"
            @update:modelValue="(val) => goToPage(Number(val))"
          />
        </div>
      </div>
    </div>

    <!-- Table -->
    <div class="bg-surface-card border border-border rounded-2xl overflow-hidden">

      <!-- Header row -->
      <div class="hidden sm:grid grid-cols-[0.3fr_0.8fr_1fr_0.9fr_0.9fr_0.9fr_0.8fr_0.9fr_0.8fr_1fr] gap-3 px-5 py-3 border-b border-border bg-white/[0.02]">
        <div class="flex items-center">
          <input type="checkbox" @change="toggleSelectAll" :checked="selectedInvoices.size === store.invoices.length && store.invoices.length > 0" class="w-4 h-4 rounded border border-border bg-white/[0.05] text-primary-500 cursor-pointer focus:ring-2 focus:ring-primary-500/30 transition-colors" />
        </div>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Invoice #</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Client Name</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Invoice Date</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Due Date</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Last Capture Attempt</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Total</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Payment Method</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Status</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Actions</span>
      </div>

      <!-- Rows or Empty state -->
      <template v-if="store.invoices.length > 0">
        <div
          v-for="invoice in store.invoices"
          :key="invoice.id"
          class="grid grid-cols-1 sm:grid-cols-[0.3fr_0.8fr_1fr_0.9fr_0.9fr_0.9fr_0.8fr_0.9fr_0.8fr_1fr] gap-2 sm:gap-3 px-5 py-3.5 border-b border-border last:border-0 hover:bg-white/[0.02] transition-colors items-center"
        >
          <div class="flex items-center">
            <input type="checkbox" @change="toggleInvoiceSelection(invoice.id)" :checked="selectedInvoices.has(invoice.id)" class="w-4 h-4 rounded border border-border bg-white/[0.05] text-primary-500 cursor-pointer focus:ring-2 focus:ring-primary-500/30 transition-colors" />
          </div>
          <span class="text-[0.82rem] text-text-muted font-mono">#{{ invoice.id }}</span>

          <span class="text-[0.82rem] text-text-secondary">{{ invoice.clientId }}</span>

          <span class="text-[0.82rem] text-text-muted">{{ new Date(invoice.createdAt).toLocaleDateString() }}</span>

          <span class="text-[0.82rem] text-text-muted">{{ new Date(invoice.dueDate).toLocaleDateString() }}</span>

          <span class="text-[0.82rem] text-text-muted">{{ new Date(invoice.updatedAt || invoice.createdAt).toLocaleDateString() }}</span>

          <span class="text-[0.82rem] font-medium text-text-primary">{{ invoice.total.toFixed(2) }} USD</span>

          <span class="text-[0.82rem] text-text-muted">{{ invoice.gateway || '—' }}</span>

          <span>
            <span
              :class="statusColorMap[invoice.status as keyof typeof statusColorMap] || 'bg-text-muted/15 text-text-muted'"
              class="px-2 py-1 rounded-full text-[0.72rem] font-medium inline-block capitalize"
=======
      Loading invoices...
    </div>

    <template v-else>

      <!-- Bulk action bar -->
      <div v-if="hasSelection" class="flex items-center gap-2.5 mb-4 px-4 py-3 bg-surface-card border border-border rounded-xl">
        <span class="text-[0.75rem] text-text-muted">{{ selectedIds.size }} selected:</span>
        <AppSelect v-model="bulkActionValue" :options="bulkActions" placeholder="Bulk Actions..." />
        <button
          type="button"
          :disabled="!bulkActionValue"
          class="px-3 py-1.5 text-[0.75rem] font-medium text-primary-400 bg-primary-500/10 border border-primary-500/20 rounded-lg hover:bg-primary-500/20 transition-colors disabled:opacity-30 disabled:pointer-events-none"
          @click="handleBulkApply"
        >
          Apply
        </button>
      </div>

      <!-- Table -->
      <div class="bg-surface-card border border-border rounded-2xl overflow-hidden">
        <!-- Header row -->
        <div class="hidden sm:grid grid-cols-[40px_0.4fr_0.5fr_0.7fr_0.7fr_0.7fr_0.6fr_0.7fr_0.7fr_auto] gap-3 px-5 py-3 border-b border-border bg-white/[0.02]">
          <span class="flex items-center">
            <AppCheckbox :model-value="selectAll" @update:model-value="toggleSelectAll" />
          </span>
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">ID</span>
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Client ID</span>
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Invoice Date</span>
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Due Date</span>
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Date Paid</span>
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Total</span>
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Payment Method</span>
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Status</span>
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Actions</span>
        </div>

        <!-- Rows -->
        <div
          v-for="invoice in store.invoices"
          :key="invoice.id"
          class="grid grid-cols-1 sm:grid-cols-[40px_0.4fr_0.5fr_0.7fr_0.7fr_0.7fr_0.6fr_0.7fr_0.7fr_auto] gap-2 sm:gap-3 px-5 py-3.5 border-b border-border last:border-0 hover:bg-white/[0.02] transition-colors"
        >
          <span class="flex items-center">
            <AppCheckbox :model-value="selectedIds.has(invoice.id)" @update:model-value="toggleSelect(invoice.id)" />
          </span>
          <span class="text-[0.82rem] text-text-muted font-mono hidden sm:block">#{{ invoice.id }}</span>
          <span class="text-[0.82rem] text-text-secondary hidden sm:block">{{ invoice.clientId }}</span>
          <span class="text-[0.82rem] text-text-secondary hidden sm:block">{{ formatDate(invoice.invoiceDate) }}</span>
          <span class="text-[0.82rem] text-text-secondary hidden sm:block">{{ formatDate(invoice.dueDate) }}</span>
          <span class="text-[0.82rem] text-text-secondary hidden sm:block">{{ invoice.paidAt ? formatDate(invoice.paidAt) : '-' }}</span>
          <span class="text-[0.82rem] text-text-primary font-medium hidden sm:block">${{ invoice.total.toFixed(2) }}</span>
          <span class="text-[0.82rem] text-text-secondary hidden sm:block">{{ invoice.paymentMethod || '-' }}</span>
          <span class="hidden sm:block">
            <span
              class="inline-block px-2 py-0.5 rounded-full text-[0.7rem] font-medium border"
              :class="statusClass(invoice.status)"
>>>>>>> origin/main
            >
              {{ invoice.status }}
            </span>
          </span>
<<<<<<< HEAD

          <div class="flex items-center gap-1">
            <button
              @click="router.push(`/billing/invoices/${invoice.id}/view`)"
              class="p-1.5 text-text-muted hover:text-text-primary hover:bg-white/[0.05] rounded transition-colors"
              title="View"
            >
              <svg class="w-4 h-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z" />
                <circle cx="12" cy="12" r="3" />
              </svg>
            </button>
            <button
              @click="router.push(`/billing/invoices/${invoice.id}/edit`)"
              class="p-1.5 text-text-muted hover:text-text-primary hover:bg-white/[0.05] rounded transition-colors"
              title="Edit"
            >
              <svg class="w-4 h-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                <path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7" />
                <path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z" />
              </svg>
            </button>
            <button
              class="p-1.5 text-text-muted hover:text-status-red hover:bg-white/[0.05] rounded transition-colors"
              title="Delete"
            >
              <svg class="w-4 h-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                <polyline points="3 6 5 6 21 6" />
                <path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2" />
                <line x1="10" y1="11" x2="10" y2="17" />
                <line x1="14" y1="11" x2="14" y2="17" />
              </svg>
            </button>
          </div>
        </div>
      </template>

      <!-- Empty state -->
      <div v-else class="px-5 py-8 text-center text-text-secondary">
        <p class="text-[0.82rem]">No invoices found.</p>
      </div>
    </div>

    <!-- Bulk Actions -->
    <div v-if="store.invoices.length > 0 && selectedInvoices.size > 0" class="fixed bottom-4 z-50 flex items-center gap-2 p-4 bg-surface-card border border-border rounded-2xl" :style="{ left: 'calc(213px + 2.5rem)', right: '2rem' }">
      <span class="text-[0.82rem] font-semibold text-text-primary">With Selected:</span>
      <button class="px-3 py-2 text-[0.78rem] text-white bg-status-green hover:bg-status-green/80 rounded-[6px] transition-colors">Mark Paid</button>
      <button class="px-3 py-2 text-[0.78rem] text-white bg-status-yellow hover:bg-status-yellow/80 rounded-[6px] transition-colors">Mark Unpaid</button>
      <button class="px-3 py-2 text-[0.78rem] text-white bg-text-muted hover:bg-text-muted/80 rounded-[6px] transition-colors">Mark Cancelled</button>
      <button class="px-3 py-2 text-[0.78rem] text-text-primary border border-border hover:bg-white/[0.05] rounded-[6px] transition-colors">Duplicate Invoice</button>
      <button class="px-3 py-2 text-[0.78rem] text-text-primary border border-border hover:bg-white/[0.05] rounded-[6px] transition-colors">Send Reminder</button>
      <button class="px-3 py-2 text-[0.78rem] text-white bg-status-red hover:bg-status-red/80 rounded-[6px] transition-colors ml-auto">Delete</button>
    </div>

    <!-- Pagination -->
    <div v-if="store.invoices.length > 0" class="flex items-center justify-between mt-6" :style="{ marginBottom: selectedInvoices.size > 0 ? '120px' : '0' }">
      <div class="text-[0.82rem] text-text-muted">
        Page {{ page }} of {{ totalPages }}
      </div>
      <div class="flex items-center gap-3">
        <button
          @click="goToPage(page - 1)"
          :disabled="page <= 1"
          class="px-4 py-2 text-[0.82rem] font-semibold text-text-secondary hover:text-text-primary disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
        >
          ← Previous Page
        </button>
        <button
          @click="goToPage(page + 1)"
          :disabled="page >= totalPages"
          class="px-4 py-2 text-[0.82rem] font-semibold text-text-secondary hover:text-text-primary disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
        >
          Next Page →
        </button>
      </div>
    </div>

=======
          <span class="hidden sm:flex items-center gap-1.5">
            <button
              type="button"
              class="px-2.5 py-1 text-[0.72rem] font-medium text-text-secondary bg-white/[0.04] border border-border rounded-lg hover:text-text-primary hover:bg-white/[0.08] transition-colors"
              @click="router.push(`/billing/${invoice.id}?mode=view`)"
            >
              View
            </button>
            <button
              type="button"
              class="px-2.5 py-1 text-[0.72rem] font-medium text-primary-400 bg-primary-500/10 border border-primary-500/20 rounded-lg hover:bg-primary-500/20 transition-colors"
              @click="router.push(`/billing/${invoice.id}?mode=edit`)"
            >
              Edit
            </button>
            <button
              type="button"
              class="px-2.5 py-1 text-[0.72rem] font-medium text-status-red bg-status-red/10 border border-status-red/20 rounded-lg hover:bg-status-red/20 transition-colors"
              @click="confirmDelete(invoice.id)"
            >
              Delete
            </button>
          </span>
        </div>

        <!-- Empty state -->
        <div v-if="store.invoices.length === 0" class="flex flex-col items-center justify-center py-10 gap-2">
          <p class="text-[0.82rem] text-text-muted">No invoices found.</p>
        </div>

        <!-- Pagination -->
        <div v-if="totalPages > 1" class="flex items-center justify-between px-5 py-3 border-t border-border bg-white/[0.02]">
          <span class="text-[0.75rem] text-text-muted">
            {{ store.totalCount }} invoice{{ store.totalCount !== 1 ? 's' : '' }}
          </span>
          <div class="flex items-center gap-1">
            <button
              :disabled="page <= 1"
              class="px-2.5 py-1 text-[0.75rem] rounded-lg text-text-secondary hover:text-text-primary hover:bg-white/[0.06] disabled:opacity-30 disabled:pointer-events-none transition-colors"
              @click="goToPage(page - 1)"
            >
              Prev
            </button>
            <span class="text-[0.75rem] text-text-muted px-2">{{ page }} / {{ totalPages }}</span>
            <button
              :disabled="page >= totalPages"
              class="px-2.5 py-1 text-[0.75rem] rounded-lg text-text-secondary hover:text-text-primary hover:bg-white/[0.06] disabled:opacity-30 disabled:pointer-events-none transition-colors"
              @click="goToPage(page + 1)"
            >
              Next
            </button>
          </div>
        </div>
      </div>

    </template>

    <!-- Delete Confirm Modal -->
    <ConfirmModal
      v-if="showDeleteModal"
      title="Delete Invoice"
      :message="`Are you sure you want to delete invoice #${deleteTargetId}?`"
      confirm-label="Delete"
      loading-label="Deleting..."
      :loading="store.loading"
      variant="danger"
      @confirm="executeDelete"
      @close="showDeleteModal = false"
    />

    <!-- Bulk Confirm Modal -->
    <ConfirmModal
      v-if="showBulkConfirmModal"
      title="Confirm Bulk Action"
      :message="`Apply '${bulkActionValue}' to ${selectedIds.size} selected invoice(s)?`"
      :confirm-label="bulkActionValue"
      loading-label="Processing..."
      :loading="store.loading"
      :variant="bulkActionValue === 'Delete' ? 'danger' : 'primary'"
      @confirm="executeBulkAction"
      @close="showBulkConfirmModal = false"
    />

    <!-- Create Invoice Modal -->
    <Teleport to="body">
      <div
        v-if="showCreateModal"
        class="fixed inset-0 z-50 flex items-center justify-center bg-black/60"
        @click.self="showCreateModal = false"
      >
        <div class="bg-zinc-900 border border-zinc-700 rounded-xl shadow-2xl w-full max-w-lg p-6 space-y-4 max-h-[90vh] overflow-y-auto">
          <div class="flex items-center justify-between">
            <h2 class="text-white font-semibold text-lg">Create Invoice</h2>
            <button class="text-zinc-400 hover:text-white transition" @click="showCreateModal = false">&#10005;</button>
          </div>

          <div class="space-y-3">
            <div>
              <label class="block text-zinc-400 text-sm mb-1">Client ID</label>
              <AppNumberInput v-model="newClientId" :min="1" placeholder="Enter client ID" />
            </div>
            <div>
              <label class="block text-zinc-400 text-sm mb-1">Due Date</label>
              <AppDatePicker v-model="newDueDate" />
            </div>
            <div>
              <label class="flex items-center gap-2 text-zinc-400 text-sm cursor-pointer">
                <AppCheckbox v-model="newIsDraft" />
                Create as Draft
              </label>
            </div>

            <div>
              <label class="block text-zinc-400 text-sm mb-2">Line Items</label>
              <div v-for="(item, idx) in newItems" :key="idx" class="flex gap-2 mb-2">
                <input
                  v-model="item.description"
                  type="text"
                  placeholder="Description"
                  class="flex-1 bg-zinc-800 border border-zinc-700 rounded-lg px-2 py-1.5 text-white text-sm focus:outline-none focus:ring-1 focus:ring-blue-500"
                />
                <div class="w-24">
                  <AppNumberInput v-model="item.unitPrice" :step="0.01" :min="0" placeholder="Price" />
                </div>
                <div class="w-16">
                  <AppNumberInput v-model="item.quantity" :min="1" placeholder="Qty" />
                </div>
                <button
                  v-if="newItems.length > 1"
                  type="button"
                  class="text-status-red hover:text-status-red/80 transition-colors px-1"
                  @click="removeNewItem(idx)"
                >
                  <svg class="w-4 h-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
                    <polyline points="3 6 5 6 21 6" /><path d="M19 6v14a2 2 0 01-2 2H7a2 2 0 01-2-2V6m3 0V4a2 2 0 012-2h4a2 2 0 012 2v2" />
                  </svg>
                </button>
              </div>
              <button
                type="button"
                class="text-primary-400 text-sm hover:underline"
                @click="addNewItem"
              >
                + Add Item
              </button>
            </div>
          </div>

          <div class="flex justify-end gap-2">
            <button
              class="px-4 py-2 bg-zinc-700 hover:bg-zinc-600 text-white text-sm rounded-lg transition"
              @click="showCreateModal = false"
            >Cancel</button>
            <button
              :disabled="creating || !newClientId"
              class="px-4 py-2 bg-blue-600 hover:bg-blue-500 text-white text-sm rounded-lg transition disabled:opacity-50"
              @click="handleCreate"
            >{{ creating ? 'Creating...' : 'Create Invoice' }}</button>
          </div>
        </div>
      </div>
    </Teleport>
>>>>>>> origin/main
  </div>
</template>
