<script setup lang="ts">
/**
 * Client-scoped invoices list -- shows invoices for a specific client
 * with filtering, bulk actions, pagination, and create functionality.
 */
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useBillingStore } from '../../billing/stores/billingStore'
import { INVOICE_STATUS_OPTIONS, INVOICE_STATUS_STYLES } from '../../../utils/constants'
import { formatDate } from '../../../utils/format'
import AppDatePicker from '../../../components/AppDatePicker.vue'
import AppNumberInput from '../../../components/AppNumberInput.vue'
import AppSelect from '../../../components/AppSelect.vue'
import AppCheckbox from '../../../components/AppCheckbox.vue'
import ConfirmModal from '../../../components/ConfirmModal.vue'

const route = useRoute()
const router = useRouter()
const store = useBillingStore()

/** Client ID from route params. */
const clientId = computed(() => route.params.id as string)

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

/** Total pages. */
const totalPages = computed(() => Math.max(1, Math.ceil(store.clientInvoicesTotal / pageSize.value)))

/** Whether any items are selected. */
const hasSelection = computed(() => selectedIds.value.size > 0)

/** Bulk action options. */
const bulkActions = [
  { value: '', label: 'Bulk Actions...' },
  { value: 'MarkPaid', label: 'Mark Paid' },
  { value: 'MarkUnpaid', label: 'Mark Unpaid' },
  { value: 'MarkCancelled', label: 'Mark Cancelled' },
  { value: 'Duplicate', label: 'Duplicate' },
  { value: 'Delete', label: 'Delete' },
]

/**
 * Fetches client invoices with current filters.
 *
 * @returns Promise that resolves when data is loaded.
 */
async function fetchInvoices(): Promise<void> {
  await store.fetchClientInvoices(
    clientId.value, page.value, pageSize.value,
    filterStatus.value, filterFrom.value, filterTo.value,
  )
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

/** Toggles selection of all invoices. */
function toggleSelectAll(): void {
  if (selectAll.value) {
    selectedIds.value = new Set()
    selectAll.value = false
  } else {
    selectedIds.value = new Set(store.clientInvoices.map(i => i.id))
    selectAll.value = true
  }
}

/**
 * Toggles selection of a single invoice.
 *
 * @param id - The invoice ID.
 */
function toggleSelect(id: number): void {
  const next = new Set(selectedIds.value)
  if (next.has(id)) {
    next.delete(id)
  } else {
    next.add(id)
  }
  selectedIds.value = next
  selectAll.value = next.size === store.clientInvoices.length && store.clientInvoices.length > 0
}

/** Executes the selected bulk action. */
async function executeBulkAction(): Promise<void> {
  showBulkConfirmModal.value = false
  if (!bulkActionValue.value || selectedIds.value.size === 0) return
  await store.bulkAction(Array.from(selectedIds.value), bulkActionValue.value)
  selectedIds.value = new Set()
  selectAll.value = false
  bulkActionValue.value = ''
  await fetchInvoices()
}

/** Opens the bulk confirm modal. */
function handleBulkApply(): void {
  if (!bulkActionValue.value || selectedIds.value.size === 0) return
  showBulkConfirmModal.value = true
}

/** Opens the create modal pre-filled with client ID. */
function openCreateModal(): void {
  newDueDate.value = ''
  newItems.value = [{ description: '', unitPrice: 0, quantity: 1 }]
  newIsDraft.value = true
  showCreateModal.value = true
}

/** Adds a blank line item. */
function addNewItem(): void {
  newItems.value.push({ description: '', unitPrice: 0, quantity: 1 })
}

/**
 * Removes a line item.
 *
 * @param index - Index to remove.
 */
function removeNewItem(index: number): void {
  newItems.value.splice(index, 1)
}

/**
 * Creates the invoice and navigates to its detail.
 *
 * @returns Promise that resolves when created.
 */
async function handleCreate(): Promise<void> {
  creating.value = true
  const id = await store.createInvoice(Number(clientId.value), newDueDate.value, newItems.value, newIsDraft.value)
  creating.value = false
  if (id) {
    showCreateModal.value = false
    router.push(`/billing/${id}`)
  }
}

/**
 * Returns the CSS class string for a status badge.
 *
 * @param status - The invoice status.
 * @returns Tailwind class string.
 */
function statusClass(status: string): string {
  return INVOICE_STATUS_STYLES[status] ?? 'text-text-muted bg-white/[0.04] border-border'
}

onMounted(() => fetchInvoices())
</script>

<template>
  <div class="p-4 sm:p-6 lg:p-8 w-full">

    <!-- Header -->
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
    </div>

    <!-- Loading -->
    <div v-if="store.loading && store.clientInvoices.length === 0" class="flex items-center gap-3 text-text-secondary text-sm">
      <span class="w-4 h-4 rounded-full border-2 border-primary-500/20 border-t-primary-500 animate-spin" />
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
        <div class="hidden sm:grid grid-cols-[40px_0.4fr_0.8fr_0.8fr_0.6fr_0.6fr_0.7fr_0.6fr_auto] gap-3 px-5 py-3 border-b border-border bg-white/[0.02]">
          <span class="flex items-center">
            <AppCheckbox :model-value="selectAll" @update:model-value="toggleSelectAll" />
          </span>
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">ID</span>
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Invoice Date</span>
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Due Date</span>
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">SubTotal</span>
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Tax</span>
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Total</span>
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Status</span>
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Actions</span>
        </div>

        <!-- Rows -->
        <div
          v-for="invoice in store.clientInvoices"
          :key="invoice.id"
          class="grid grid-cols-1 sm:grid-cols-[40px_0.4fr_0.8fr_0.8fr_0.6fr_0.6fr_0.7fr_0.6fr_auto] gap-2 sm:gap-3 px-5 py-3.5 border-b border-border last:border-0 hover:bg-white/[0.02] transition-colors"
        >
          <span class="flex items-center">
            <AppCheckbox :model-value="selectedIds.has(invoice.id)" @update:model-value="toggleSelect(invoice.id)" />
          </span>
          <span class="text-[0.82rem] text-text-muted font-mono hidden sm:block">#{{ invoice.id }}</span>
          <span class="text-[0.82rem] text-text-secondary hidden sm:block">{{ formatDate(invoice.invoiceDate) }}</span>
          <span class="text-[0.82rem] text-text-secondary hidden sm:block">{{ formatDate(invoice.dueDate) }}</span>
          <span class="text-[0.82rem] text-text-secondary hidden sm:block">${{ invoice.subTotal.toFixed(2) }}</span>
          <span class="text-[0.82rem] text-text-secondary hidden sm:block">${{ invoice.tax.toFixed(2) }}</span>
          <span class="text-[0.82rem] text-text-primary font-medium hidden sm:block">${{ invoice.total.toFixed(2) }}</span>
          <span class="hidden sm:block">
            <span
              class="inline-block px-2 py-0.5 rounded-full text-[0.7rem] font-medium border"
              :class="statusClass(invoice.status)"
            >
              {{ invoice.status }}
            </span>
          </span>
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
          </span>
        </div>

        <!-- Empty state -->
        <div v-if="store.clientInvoices.length === 0" class="flex flex-col items-center justify-center py-10 gap-2">
          <p class="text-[0.82rem] text-text-muted">No invoices found.</p>
        </div>

        <!-- Pagination -->
        <div v-if="totalPages > 1" class="flex items-center justify-between px-5 py-3 border-t border-border bg-white/[0.02]">
          <span class="text-[0.75rem] text-text-muted">
            {{ store.clientInvoicesTotal }} invoice{{ store.clientInvoicesTotal !== 1 ? 's' : '' }}
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
            <div class="text-zinc-400 text-sm">
              Client #{{ clientId }}
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
              :disabled="creating"
              class="px-4 py-2 bg-blue-600 hover:bg-blue-500 text-white text-sm rounded-lg transition disabled:opacity-50"
              @click="handleCreate"
            >{{ creating ? 'Creating...' : 'Create Invoice' }}</button>
          </div>
        </div>
      </div>
    </Teleport>
  </div>
</template>
