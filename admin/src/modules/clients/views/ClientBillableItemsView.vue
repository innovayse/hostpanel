<script setup lang="ts">
/**
 * Client billable items page -- displays uninvoiced and invoiced billable items.
 * Allows adding new items, time billing entries, and invoicing selected items.
 */
import { ref, computed, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { useApi } from '../../../composables/useApi'
import AppCheckbox from '../../../components/AppCheckbox.vue'
import ConfirmModal from '../../../components/ConfirmModal.vue'
import AddBillableItemModal from '../components/AddBillableItemModal.vue'
import AddTimeBillingModal from '../components/AddTimeBillingModal.vue'
import type { BillableItem, BillableItemsResult } from '../../../types/models'

const route = useRoute()
const { request } = useApi()

/** Client ID from route params. */
const clientId = computed(() => route.params.id as string)

/** True while the initial load is in flight. */
const loading = ref(false)

/** Error message, null when no error. */
const error = ref<string | null>(null)

/** Uninvoiced billable items for this client. */
const uninvoicedItems = ref<BillableItem[]>([])

/** Invoiced billable items for this client (current page). */
const invoicedItems = ref<BillableItem[]>([])

/** Sum of all uninvoiced item amounts. */
const uninvoicedTotal = ref(0)

/** Current invoiced items page (1-based). */
const invoicedPage = ref(1)

/** Invoiced items per page. */
const invoicedPageSize = ref(20)

/** Total number of invoiced items across all pages. */
const invoicedTotalCount = ref(0)

/** Whether the add billable item modal is visible. */
const showAddItemModal = ref(false)

/** Whether the time billing modal is visible. */
const showTimeBillingModal = ref(false)

/** Set of selected uninvoiced item IDs. */
const selectedIds = ref<Set<number>>(new Set())

/** Whether all uninvoiced items are selected. */
const selectAll = ref(false)

/** Whether the invoice confirmation modal is visible. */
const showInvoiceConfirmModal = ref(false)

/** Whether the delete confirmation modal is visible. */
const showDeleteConfirmModal = ref(false)

/** ID of the item pending single deletion, or null for bulk delete. */
const pendingDeleteId = ref<number | null>(null)

/** True while invoicing is in progress. */
const invoicing = ref(false)

/** True while a delete is in progress. */
const deleting = ref(false)

/** Total number of invoiced pages based on totalCount and pageSize. */
const invoicedTotalPages = computed(() => Math.max(1, Math.ceil(invoicedTotalCount.value / invoicedPageSize.value)))

/**
 * Fetches billable items for this client from the backend.
 *
 * @returns Promise that resolves when data is loaded.
 */
async function fetchBillableItems(): Promise<void> {
  loading.value = true
  error.value = null
  try {
    const result = await request<BillableItemsResult>(
      `/clients/${clientId.value}/billable-items?invoicedPage=${invoicedPage.value}&invoicedPageSize=${invoicedPageSize.value}`,
    )
    uninvoicedItems.value = result.uninvoicedItems
    uninvoicedTotal.value = result.uninvoicedTotal
    invoicedItems.value = result.invoicedItems.items
    invoicedTotalCount.value = result.invoicedItems.totalCount
  } catch {
    error.value = 'Failed to load billable items.'
  } finally {
    loading.value = false
  }
}

/**
 * Invoices all selected uninvoiced items.
 *
 * @returns Promise that resolves when the operation completes.
 */
async function handleInvoiceSelected(): Promise<void> {
  invoicing.value = true
  try {
    await request(`/clients/${clientId.value}/billable-items/invoice-selected`, {
      method: 'POST',
      body: JSON.stringify({ billableItemIds: Array.from(selectedIds.value) }),
    })
    selectedIds.value = new Set()
    selectAll.value = false
    showInvoiceConfirmModal.value = false
    await fetchBillableItems()
  } catch {
    error.value = 'Failed to invoice selected items.'
  } finally {
    invoicing.value = false
  }
}

/**
 * Deletes a billable item by ID after confirmation.
 *
 * @param id - The billable item ID to delete.
 * @returns Promise that resolves when the item is deleted.
 */
async function handleDelete(id: number): Promise<void> {
  pendingDeleteId.value = id
  showDeleteConfirmModal.value = true
}

/**
 * Confirms the pending delete action (single or bulk).
 *
 * @returns Promise that resolves when deletion completes.
 */
async function confirmDelete(): Promise<void> {
  showDeleteConfirmModal.value = false
  deleting.value = true
  try {
    if (pendingDeleteId.value !== null) {
      await request(`/clients/${clientId.value}/billable-items/${pendingDeleteId.value}`, { method: 'DELETE' })
    } else {
      for (const id of selectedIds.value) {
        await request(`/clients/${clientId.value}/billable-items/${id}`, { method: 'DELETE' })
      }
      selectedIds.value = new Set()
      selectAll.value = false
    }
    await fetchBillableItems()
  } catch {
    error.value = 'Failed to delete billable item(s).'
  } finally {
    deleting.value = false
    pendingDeleteId.value = null
  }
}

/**
 * Deletes all selected uninvoiced items after confirmation.
 *
 * @returns Promise that resolves when all selected items are deleted.
 */
async function handleDeleteSelected(): Promise<void> {
  if (selectedIds.value.size === 0) return
  pendingDeleteId.value = null
  showDeleteConfirmModal.value = true
}

/** Toggles selection of all uninvoiced items. */
function toggleSelectAll(): void {
  if (selectAll.value) {
    selectedIds.value = new Set()
    selectAll.value = false
  } else {
    selectedIds.value = new Set(uninvoicedItems.value.map(i => i.id))
    selectAll.value = true
  }
}

/**
 * Toggles selection of a single item.
 *
 * @param id - The item ID to toggle.
 */
function toggleSelect(id: number): void {
  const next = new Set(selectedIds.value)
  if (next.has(id)) {
    next.delete(id)
  } else {
    next.add(id)
  }
  selectedIds.value = next
  selectAll.value = next.size === uninvoicedItems.value.length && uninvoicedItems.value.length > 0
}

/**
 * Navigate to a specific invoiced items page and re-fetch.
 *
 * @param p - Target page number.
 */
function goToInvoicedPage(p: number): void {
  if (p < 1 || p > invoicedTotalPages.value) return
  invoicedPage.value = p
  fetchBillableItems()
}

/** Handles successful save from the add billable item modal. */
function onItemSaved(): void {
  showAddItemModal.value = false
  fetchBillableItems()
}

/** Handles successful save from the time billing modal. */
function onTimeBillingSaved(): void {
  showTimeBillingModal.value = false
  fetchBillableItems()
}

onMounted(() => fetchBillableItems())
</script>

<template>
  <div class="p-4 sm:p-6 lg:p-8 w-full">

    <!-- Action bar -->
    <div class="flex items-center justify-between gap-2.5 mb-5">
      <h1 class="text-[0.875rem] font-semibold text-text-primary">Billable Items</h1>
      <div class="flex items-center gap-2.5">
        <button
          type="button"
          class="px-4 py-2 text-[0.84rem] font-medium text-text-secondary hover:text-text-primary bg-white/[0.04] border border-border rounded-[10px] transition-colors"
          @click="showTimeBillingModal = true"
        >
          Add Time Billing Entries
        </button>
        <button
          type="button"
          class="gradient-brand px-5 py-2 text-[0.84rem] font-semibold text-white rounded-[10px] transition-opacity"
          @click="showAddItemModal = true"
        >
          + Add Billable Item
        </button>
      </div>
    </div>

    <!-- Loading -->
    <div v-if="loading && uninvoicedItems.length === 0 && invoicedItems.length === 0" class="flex items-center gap-3 text-text-secondary text-sm">
      <span class="w-4 h-4 rounded-full border-2 border-primary-500/20 border-t-primary-500 animate-spin" />
      Loading billable items...
    </div>

    <!-- Error -->
    <div v-else-if="error" class="text-sm text-status-red bg-status-red/8 border border-status-red/20 rounded-xl p-4 mb-5">
      {{ error }}
    </div>

    <template v-else>

      <!-- Uninvoiced Items Section -->
      <div class="mb-6">
        <h2 class="text-[0.82rem] font-semibold text-text-primary mb-3">
          Uninvoiced Items &mdash; ${{ uninvoicedTotal.toFixed(2) }} USD ({{ uninvoicedItems.length }})
        </h2>

        <div v-if="uninvoicedItems.length > 0" class="bg-surface-card border border-border rounded-2xl overflow-hidden">
          <!-- Header row -->
          <div class="hidden sm:grid grid-cols-[40px_0.4fr_2fr_0.8fr_0.8fr_1.2fr_60px] gap-3 px-5 py-3 border-b border-border bg-white/[0.02]">
            <span class="flex items-center">
              <AppCheckbox :model-value="selectAll" @update:model-value="toggleSelectAll" />
            </span>
            <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">ID</span>
            <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Description</span>
            <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Hours/Qty</span>
            <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Amount</span>
            <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Invoice Action</span>
            <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Actions</span>
          </div>

          <!-- Rows -->
          <div
            v-for="item in uninvoicedItems"
            :key="item.id"
            class="grid grid-cols-1 sm:grid-cols-[40px_0.4fr_2fr_0.8fr_0.8fr_1.2fr_60px] gap-2 sm:gap-3 px-5 py-3.5 border-b border-border last:border-0 hover:bg-white/[0.02] transition-colors"
          >
            <span class="flex items-center">
              <AppCheckbox :model-value="selectedIds.has(item.id)" @update:model-value="toggleSelect(item.id)" />
            </span>
            <span class="text-[0.82rem] text-text-muted font-mono hidden sm:block">{{ item.id }}</span>
            <span class="text-[0.82rem] text-text-secondary truncate">{{ item.description }}</span>
            <span class="text-[0.82rem] text-text-secondary">{{ item.hoursQty }} {{ item.isHours ? 'hrs' : 'qty' }}</span>
            <span class="text-[0.82rem] text-text-secondary">${{ item.amount.toFixed(2) }}</span>
            <span class="text-[0.82rem] text-text-muted">{{ item.invoiceAction }}</span>
            <span class="flex items-center">
              <button
                type="button"
                class="text-status-red hover:text-status-red/80 transition-colors"
                :disabled="deleting"
                @click="handleDelete(item.id)"
              >
                <svg class="w-4 h-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
                  <polyline points="3 6 5 6 21 6" /><path d="M19 6v14a2 2 0 01-2 2H7a2 2 0 01-2-2V6m3 0V4a2 2 0 012-2h4a2 2 0 012 2v2" />
                </svg>
              </button>
            </span>
          </div>

          <!-- With Selected bar -->
          <div class="flex items-center gap-3 px-5 py-3 border-t border-border bg-white/[0.02]">
            <span class="text-[0.75rem] text-text-muted">With Selected:</span>
            <button
              type="button"
              :disabled="selectedIds.size === 0 || invoicing"
              class="px-3 py-1.5 text-[0.75rem] font-medium text-primary-400 bg-primary-500/10 border border-primary-500/20 rounded-lg hover:bg-primary-500/20 transition-colors disabled:opacity-30 disabled:pointer-events-none"
              @click="showInvoiceConfirmModal = true"
            >
              Invoice Selected Items
            </button>
            <button
              type="button"
              :disabled="selectedIds.size === 0 || deleting"
              class="px-3 py-1.5 text-[0.75rem] font-medium text-status-red bg-status-red/10 border border-status-red/20 rounded-lg hover:bg-status-red/20 transition-colors disabled:opacity-30 disabled:pointer-events-none"
              @click="handleDeleteSelected"
            >
              Delete
            </button>
          </div>
        </div>

        <!-- Empty state for uninvoiced -->
        <div v-else class="bg-surface-card border border-border rounded-2xl flex flex-col items-center justify-center py-10 gap-2">
          <p class="text-[0.82rem] text-text-muted">No uninvoiced billable items.</p>
        </div>
      </div>

      <!-- Invoiced Items Section -->
      <div>
        <h2 class="text-[0.82rem] font-semibold text-text-primary mb-3">Invoiced Items</h2>

        <div v-if="invoicedItems.length > 0" class="bg-surface-card border border-border rounded-2xl overflow-hidden">
          <!-- Header row -->
          <div class="hidden sm:grid grid-cols-[0.4fr_2fr_0.8fr_0.8fr_0.8fr] gap-3 px-5 py-3 border-b border-border bg-white/[0.02]">
            <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">ID</span>
            <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Description</span>
            <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Hours/Qty</span>
            <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Amount</span>
            <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Invoice #</span>
          </div>

          <!-- Rows -->
          <div
            v-for="item in invoicedItems"
            :key="item.id"
            class="grid grid-cols-1 sm:grid-cols-[0.4fr_2fr_0.8fr_0.8fr_0.8fr] gap-2 sm:gap-3 px-5 py-3.5 border-b border-border last:border-0 hover:bg-white/[0.02] transition-colors"
          >
            <span class="text-[0.82rem] text-text-muted font-mono hidden sm:block">{{ item.id }}</span>
            <span class="text-[0.82rem] text-text-secondary truncate">{{ item.description }}</span>
            <span class="text-[0.82rem] text-text-secondary">{{ item.hoursQty }} {{ item.isHours ? 'hrs' : 'qty' }}</span>
            <span class="text-[0.82rem] text-text-secondary">${{ item.amount.toFixed(2) }}</span>
            <span class="text-[0.82rem] text-primary-400">
              <template v-if="item.invoiceId">#{{ item.invoiceId }}</template>
              <template v-else>&mdash;</template>
            </span>
          </div>

          <!-- Pagination -->
          <div v-if="invoicedTotalPages > 1" class="flex items-center justify-between px-5 py-3 border-t border-border bg-white/[0.02]">
            <span class="text-[0.75rem] text-text-muted">
              {{ invoicedTotalCount }} item{{ invoicedTotalCount !== 1 ? 's' : '' }}
            </span>
            <div class="flex items-center gap-1">
              <button
                :disabled="invoicedPage <= 1"
                class="px-2.5 py-1 text-[0.75rem] rounded-lg text-text-secondary hover:text-text-primary hover:bg-white/[0.06] disabled:opacity-30 disabled:pointer-events-none transition-colors"
                @click="goToInvoicedPage(invoicedPage - 1)"
              >
                Prev
              </button>
              <span class="text-[0.75rem] text-text-muted px-2">{{ invoicedPage }} / {{ invoicedTotalPages }}</span>
              <button
                :disabled="invoicedPage >= invoicedTotalPages"
                class="px-2.5 py-1 text-[0.75rem] rounded-lg text-text-secondary hover:text-text-primary hover:bg-white/[0.06] disabled:opacity-30 disabled:pointer-events-none transition-colors"
                @click="goToInvoicedPage(invoicedPage + 1)"
              >
                Next
              </button>
            </div>
          </div>
        </div>

        <!-- Empty state for invoiced -->
        <div v-else class="bg-surface-card border border-border rounded-2xl flex flex-col items-center justify-center py-10 gap-2">
          <p class="text-[0.82rem] text-text-muted">No invoiced billable items.</p>
        </div>
      </div>

    </template>

    <!-- Invoice Confirmation Modal -->
    <ConfirmModal
      v-if="showInvoiceConfirmModal"
      title="Confirm Invoice"
      message="Are you sure you want to invoice the selected items immediately?"
      confirm-label="OK"
      loading-label="Processing..."
      :loading="invoicing"
      @confirm="handleInvoiceSelected"
      @close="showInvoiceConfirmModal = false"
    />

    <!-- Delete Confirmation Modal -->
    <ConfirmModal
      v-if="showDeleteConfirmModal"
      title="Confirm Delete"
      :message="pendingDeleteId !== null
        ? 'Are you sure you want to delete this billable item?'
        : `Are you sure you want to delete ${selectedIds.size} selected item(s)?`"
      confirm-label="Delete"
      loading-label="Deleting..."
      :loading="deleting"
      variant="danger"
      @confirm="confirmDelete"
      @close="showDeleteConfirmModal = false"
    />

    <!-- Add Billable Item Modal -->
    <AddBillableItemModal
      v-if="showAddItemModal"
      :client-id="clientId"
      :saving="false"
      @saved="onItemSaved"
      @close="showAddItemModal = false"
    />

    <!-- Add Time Billing Modal -->
    <AddTimeBillingModal
      v-if="showTimeBillingModal"
      :client-id="clientId"
      :saving="false"
      @saved="onTimeBillingSaved"
      @close="showTimeBillingModal = false"
    />
  </div>
</template>
