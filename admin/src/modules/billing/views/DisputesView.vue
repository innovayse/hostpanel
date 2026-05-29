<script setup lang="ts">
import { ref, computed } from 'vue'

const page = ref(1)
const pageSize = 20
const searchQuery = ref('')
const sortBy = ref<'gateway' | 'id' | 'createdAt' | 'respondBy' | 'amount' | 'transactionId' | 'reason' | 'status'>('createdAt')
const sortOrder = ref<'asc' | 'desc'>('desc')

// Mock disputes data
const disputes = ref([
  {
    id: 1,
    gateway: 'Stripe',
    disputeId: 'dp_1234567890',
    createdAt: '2026-05-20',
    respondBy: '2026-06-03',
    amount: 149.99,
    transactionId: 'txn_1234567890',
    reason: 'Fraudulent',
    status: 'open'
  },
  {
    id: 2,
    gateway: 'PayPal',
    disputeId: 'pp_dispute_789',
    createdAt: '2026-05-15',
    respondBy: '2026-05-29',
    amount: 299.00,
    transactionId: 'txn_9876543210',
    reason: 'Unrecognized Transaction',
    status: 'pending'
  },
])

const authenticationError = ref('Authentication failed for the following payment gateways: PayPal,Stripe')

const filteredDisputes = computed(() => {
  return disputes.value.filter(dispute => {
    if (!searchQuery.value) return true
    const query = searchQuery.value.toLowerCase()
    return (
      dispute.gateway.toLowerCase().includes(query) ||
      dispute.disputeId.toLowerCase().includes(query) ||
      dispute.transactionId.toLowerCase().includes(query) ||
      dispute.reason.toLowerCase().includes(query)
    )
  })
})

const sortedDisputes = computed(() => {
  const sorted = [...filteredDisputes.value].sort((a, b) => {
    const aVal = a[sortBy.value]
    const bVal = b[sortBy.value]
    const comparison = aVal < bVal ? -1 : aVal > bVal ? 1 : 0
    return sortOrder.value === 'asc' ? comparison : -comparison
  })
  return sorted
})

const totalCount = computed(() => sortedDisputes.value.length)
const totalPages = computed(() => Math.max(1, Math.ceil(totalCount.value / pageSize)))
const canGoPrevious = computed(() => page.value > 1)
const canGoNext = computed(() => page.value < totalPages.value)

const paginatedDisputes = computed(() => {
  const start = (page.value - 1) * pageSize
  const end = start + pageSize
  return sortedDisputes.value.slice(start, end)
})

function toggleSort(column: typeof sortBy.value): void {
  if (sortBy.value === column) {
    sortOrder.value = sortOrder.value === 'asc' ? 'desc' : 'asc'
  } else {
    sortBy.value = column
    sortOrder.value = 'asc'
  }
}

function getSortIcon(column: typeof sortBy.value): string {
  if (sortBy.value !== column) return '⇅'
  return sortOrder.value === 'asc' ? '↑' : '↓'
}

function goToPrevious(): void {
  if (canGoPrevious.value) page.value--
}

function goToNext(): void {
  if (canGoNext.value) page.value++
}
</script>

<template>
  <div class="p-4 sm:p-6 lg:p-8 w-full">

    <!-- Header -->
    <div class="mb-6">
      <h1 class="font-display font-bold text-[1.25rem] text-text-primary leading-none mb-3">List Disputes</h1>

      <!-- Description -->
      <p class="text-[0.82rem] text-text-secondary mb-4">
        A dispute occurs when a customer questions your charge with their card issuer. When this happens, you're given the opportunity to respond to the dispute with evidence that shows that the charge is legitimate.
      </p>

      <!-- Authentication Error Banner -->
      <div v-if="authenticationError" class="bg-yellow-500/10 border border-yellow-500/30 rounded-lg p-4 mb-4">
        <div class="flex gap-3">
          <div class="w-6 h-6 rounded-full bg-yellow-500/20 flex items-center justify-center flex-shrink-0">
            <span class="text-yellow-600 text-sm font-bold">i</span>
          </div>
          <div>
            <h3 class="text-[0.82rem] font-semibold text-yellow-700 mb-1">Authentication Error</h3>
            <p class="text-[0.78rem] text-yellow-600">{{ authenticationError }}</p>
          </div>
        </div>
      </div>
    </div>

    <!-- Search Box -->
    <div class="mb-4 flex justify-end">
      <div class="flex items-center gap-2">
        <span class="text-[0.82rem] text-text-muted">Search:</span>
        <input
          v-model="searchQuery"
          type="text"
          placeholder="Search disputes..."
          class="px-3 py-2 text-[0.82rem] text-text-primary bg-white/[0.05] border border-border rounded-[9px] focus:outline-none focus:border-primary-500/40 transition-colors w-64"
        />
      </div>
    </div>

    <!-- Table -->
    <div class="bg-surface-card border border-border rounded-2xl overflow-hidden overflow-x-auto">

      <!-- Header row -->
      <div class="hidden sm:grid grid-cols-[1fr_1.2fr_1.2fr_1.2fr_0.9fr_1.2fr_1.2fr_1fr] gap-0 px-5 py-3 border-b border-border bg-white/[0.02] text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">
        <button @click="toggleSort('gateway')" class="text-left text-text-muted hover:text-text-primary transition-colors flex items-center gap-1">
          Gateway <span class="text-[0.7rem]">{{ sortBy === 'gateway' ? getSortIcon('gateway') : '⇅' }}</span>
        </button>
        <button @click="toggleSort('id')" class="text-left text-text-muted hover:text-text-primary transition-colors flex items-center gap-1">
          Dispute ID <span class="text-[0.7rem]">{{ sortBy === 'id' ? getSortIcon('id') : '⇅' }}</span>
        </button>
        <button @click="toggleSort('createdAt')" class="text-left text-text-muted hover:text-text-primary transition-colors flex items-center gap-1">
          Created At <span class="text-[0.7rem]">{{ sortBy === 'createdAt' ? getSortIcon('createdAt') : '⇅' }}</span>
        </button>
        <button @click="toggleSort('respondBy')" class="text-left text-text-muted hover:text-text-primary transition-colors flex items-center gap-1">
          Respond By <span class="text-[0.7rem]">{{ sortBy === 'respondBy' ? getSortIcon('respondBy') : '⇅' }}</span>
        </button>
        <button @click="toggleSort('amount')" class="text-left text-text-muted hover:text-text-primary transition-colors flex items-center gap-1">
          Amount <span class="text-[0.7rem]">{{ sortBy === 'amount' ? getSortIcon('amount') : '⇅' }}</span>
        </button>
        <button @click="toggleSort('transactionId')" class="text-left text-text-muted hover:text-text-primary transition-colors flex items-center gap-1">
          Transaction ID <span class="text-[0.7rem]">{{ sortBy === 'transactionId' ? getSortIcon('transactionId') : '⇅' }}</span>
        </button>
        <button @click="toggleSort('reason')" class="text-left text-text-muted hover:text-text-primary transition-colors flex items-center gap-1">
          Reason <span class="text-[0.7rem]">{{ sortBy === 'reason' ? getSortIcon('reason') : '⇅' }}</span>
        </button>
        <button @click="toggleSort('status')" class="text-left text-text-muted hover:text-text-primary transition-colors flex items-center gap-1">
          Status <span class="text-[0.7rem]">{{ sortBy === 'status' ? getSortIcon('status') : '⇅' }}</span>
        </button>
      </div>

      <!-- Rows or Empty state -->
      <template v-if="paginatedDisputes.length > 0">
        <div
          v-for="dispute in paginatedDisputes"
          :key="dispute.id"
          class="grid grid-cols-1 sm:grid-cols-[1fr_1.2fr_1.2fr_1.2fr_0.9fr_1.2fr_1.2fr_1fr] gap-2 sm:gap-0 px-5 py-3.5 border-b border-border last:border-0 hover:bg-white/[0.02] transition-colors items-center text-[0.82rem]"
        >
          <span class="text-text-muted font-medium">{{ dispute.gateway }}</span>
          <span class="text-text-secondary font-mono">{{ dispute.disputeId }}</span>
          <span class="text-text-muted">{{ new Date(dispute.createdAt).toLocaleDateString() }}</span>
          <span class="text-text-muted">{{ new Date(dispute.respondBy).toLocaleDateString() }}</span>
          <span class="text-text-primary font-medium">{{ dispute.amount.toFixed(2) }} USD</span>
          <span class="text-text-secondary font-mono">{{ dispute.transactionId }}</span>
          <span class="text-text-secondary">{{ dispute.reason }}</span>
          <span>
            <span
              :class="{
                'bg-status-red/15 text-status-red': dispute.status === 'open',
                'bg-status-yellow/15 text-status-yellow': dispute.status === 'pending',
                'bg-status-green/15 text-status-green': dispute.status === 'resolved'
              }"
              class="px-2 py-1 rounded-full text-[0.72rem] font-medium inline-block capitalize"
            >
              {{ dispute.status }}
            </span>
          </span>
        </div>
      </template>

      <!-- Empty state -->
      <div v-else class="px-5 py-8 text-center text-text-secondary">
        <p class="text-[0.82rem]">No Records Found</p>
      </div>
    </div>

    <!-- Pagination -->
    <div v-if="totalCount > 0" class="flex items-center justify-between mt-6">
      <button
        @click="goToPrevious"
        :disabled="!canGoPrevious"
        class="text-[0.82rem] font-semibold text-text-muted hover:text-text-primary disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
      >
        Previous
      </button>
      <button
        @click="goToNext"
        :disabled="!canGoNext"
        class="text-[0.82rem] font-semibold text-text-muted hover:text-text-primary disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
      >
        Next
      </button>
    </div>

  </div>
</template>
