<script setup lang="ts">
/**
 * Client transactions page -- displays financial ledger entries with summary cards.
 * Allows adding new transactions and deleting existing ones.
 */
import { ref, computed, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { useTransactionsStore } from '../stores/transactionsStore'
import { formatDate } from '../../../utils/format'
import ConfirmModal from '../../../components/ConfirmModal.vue'
import AddTransactionModal from '../components/AddTransactionModal.vue'

const route = useRoute()
const store = useTransactionsStore()

/** Client ID from route params. */
const clientId = computed(() => route.params.id as string)

/** Current page (1-based). */
const page = ref(1)

/** Items per page. */
const pageSize = ref(25)

/** Whether the add transaction modal is visible. */
const showAddModal = ref(false)

/** Whether the delete confirmation modal is visible. */
const showDeleteModal = ref(false)

/** ID of the transaction pending deletion. */
const deleteTargetId = ref<number | null>(null)

/** Total pages based on totalCount and pageSize. */
const totalPages = computed(() => Math.max(1, Math.ceil(store.totalCount / pageSize.value)))

/**
 * Fetches transactions for the current client.
 *
 * @returns Promise that resolves when data is loaded.
 */
async function fetchTransactions(): Promise<void> {
  await store.fetchByClient(clientId.value, page.value, pageSize.value)
}

/**
 * Navigates to a specific page and re-fetches.
 *
 * @param p - Target page number.
 */
function goToPage(p: number): void {
  if (p < 1 || p > totalPages.value) return
  page.value = p
  fetchTransactions()
}

/** Handles successful save from the add transaction modal. */
function onTransactionSaved(): void {
  showAddModal.value = false
  fetchTransactions()
}

/**
 * Opens the delete confirmation modal for a transaction.
 *
 * @param id - The transaction ID to delete.
 */
function confirmDelete(id: number): void {
  deleteTargetId.value = id
  showDeleteModal.value = true
}

/**
 * Executes the pending delete and refreshes the list.
 *
 * @returns Promise that resolves when deletion completes.
 */
async function executeDelete(): Promise<void> {
  if (!deleteTargetId.value) return
  await store.remove(deleteTargetId.value)
  showDeleteModal.value = false
  deleteTargetId.value = null
  await fetchTransactions()
}

onMounted(() => fetchTransactions())
</script>

<template>
  <div class="p-4 sm:p-6 lg:p-8 w-full">

    <!-- Header -->
    <div class="flex items-center justify-between gap-2.5 mb-5">
      <h1 class="text-[0.875rem] font-semibold text-text-primary">Transactions</h1>
      <button
        type="button"
        class="gradient-brand px-5 py-2 text-[0.84rem] font-semibold text-white rounded-[10px] transition-opacity"
        @click="showAddModal = true"
      >
        + Add New Transaction
      </button>
    </div>

    <!-- Error -->
    <div v-if="store.error" class="text-sm text-status-red bg-status-red/8 border border-status-red/20 rounded-xl p-4 mb-5">
      {{ store.error }}
    </div>

    <!-- Loading -->
    <div v-if="store.loading && store.transactions.length === 0" class="flex items-center gap-3 text-text-secondary text-sm">
      <span class="w-4 h-4 rounded-full border-2 border-primary-500/20 border-t-primary-500 animate-spin" />
      Loading transactions...
    </div>

    <template v-else>

      <!-- Summary cards -->
      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4 mb-6">
        <!-- Total In -->
        <div class="bg-surface-card border border-border rounded-2xl p-5 border-l-4 border-l-status-green">
          <p class="text-xl font-bold text-text-primary">${{ store.summary.totalIn.toFixed(2) }}</p>
          <p class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mt-1">Total In</p>
        </div>

        <!-- Total Fees -->
        <div class="bg-surface-card border border-border rounded-2xl p-5 border-l-4 border-l-status-yellow">
          <p class="text-xl font-bold text-text-primary">${{ store.summary.totalFees.toFixed(2) }}</p>
          <p class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mt-1">Total Fees</p>
        </div>

        <!-- Total Out -->
        <div class="bg-surface-card border border-border rounded-2xl p-5 border-l-4 border-l-status-red">
          <p class="text-xl font-bold text-text-primary">${{ store.summary.totalOut.toFixed(2) }}</p>
          <p class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mt-1">Total Out</p>
        </div>

        <!-- Balance -->
        <div class="bg-surface-card border border-border rounded-2xl p-5 border-l-4 border-l-primary-500">
          <p class="text-xl font-bold text-text-primary">${{ store.summary.balance.toFixed(2) }}</p>
          <p class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mt-1">Balance</p>
        </div>
      </div>

      <!-- Table -->
      <div class="bg-surface-card border border-border rounded-2xl overflow-hidden">
        <!-- Header row -->
        <div class="hidden sm:grid grid-cols-[0.8fr_0.8fr_1.5fr_0.7fr_0.7fr_0.5fr_60px] gap-3 px-5 py-3 border-b border-border bg-white/[0.02]">
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Date</span>
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Payment Method</span>
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Description</span>
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Amount In</span>
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Amount Out</span>
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Fees</span>
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Actions</span>
        </div>

        <!-- Rows -->
        <div
          v-for="tx in store.transactions"
          :key="tx.id"
          class="grid grid-cols-1 sm:grid-cols-[0.8fr_0.8fr_1.5fr_0.7fr_0.7fr_0.5fr_60px] gap-2 sm:gap-3 px-5 py-3.5 border-b border-border last:border-0 hover:bg-white/[0.02] transition-colors"
        >
          <span class="text-[0.82rem] text-text-secondary">{{ formatDate(tx.date) }}</span>
          <span class="text-[0.82rem] text-text-secondary">{{ tx.paymentMethod }}</span>
          <span class="text-[0.82rem] text-text-secondary truncate">{{ tx.description }}</span>
          <span class="text-[0.82rem] font-medium" :class="tx.amountIn > 0 ? 'text-status-green' : 'text-text-muted'">
            ${{ tx.amountIn.toFixed(2) }}
          </span>
          <span class="text-[0.82rem] font-medium" :class="tx.amountOut > 0 ? 'text-status-red' : 'text-text-muted'">
            ${{ tx.amountOut.toFixed(2) }}
          </span>
          <span class="text-[0.82rem] text-text-secondary">${{ tx.fees.toFixed(2) }}</span>
          <span class="flex items-center">
            <button
              type="button"
              class="text-status-red hover:text-status-red/80 transition-colors"
              :disabled="store.loading"
              @click="confirmDelete(tx.id)"
            >
              <svg class="w-4 h-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
                <polyline points="3 6 5 6 21 6" /><path d="M19 6v14a2 2 0 01-2 2H7a2 2 0 01-2-2V6m3 0V4a2 2 0 012-2h4a2 2 0 012 2v2" />
              </svg>
            </button>
          </span>
        </div>

        <!-- Empty state -->
        <div v-if="store.transactions.length === 0" class="flex flex-col items-center justify-center py-10 gap-2">
          <p class="text-[0.82rem] text-text-muted">No Records Found</p>
        </div>

        <!-- Pagination -->
        <div v-if="totalPages > 1" class="flex items-center justify-between px-5 py-3 border-t border-border bg-white/[0.02]">
          <span class="text-[0.75rem] text-text-muted">
            {{ store.totalCount }} transaction{{ store.totalCount !== 1 ? 's' : '' }}
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

    <!-- Delete Confirmation Modal -->
    <ConfirmModal
      v-if="showDeleteModal"
      title="Delete Transaction"
      :message="`Are you sure you want to delete transaction #${deleteTargetId}?`"
      confirm-label="Delete"
      loading-label="Deleting..."
      :loading="store.loading"
      variant="danger"
      @confirm="executeDelete"
      @close="showDeleteModal = false"
    />

    <!-- Add Transaction Modal -->
    <AddTransactionModal
      v-if="showAddModal"
      :client-id="clientId"
      :saving="false"
      @saved="onTransactionSaved"
      @close="showAddModal = false"
    />
  </div>
</template>
