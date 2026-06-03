<script setup lang="ts">
import { onMounted, ref, computed } from 'vue'
import { useRouter } from 'vue-router'
import AppSelect from '../../../components/AppSelect.vue'

const router = useRouter()
const page = ref(1)
const pageSize = 20

// Mock data for offline CC payments
const offlineCCPayments = ref([
  {
    id: 1,
    clientId: 1,
    clientName: 'John Doe',
    invoiceId: 101,
    invoiceDate: '2026-05-20',
    dueDate: '2026-06-20',
    total: 250.00,
    status: 'pending'
  },
  {
    id: 2,
    clientId: 2,
    clientName: 'Jane Smith',
    invoiceId: 102,
    invoiceDate: '2026-05-18',
    dueDate: '2026-06-18',
    total: 500.00,
    status: 'pending'
  },
])

const totalCount = computed(() => offlineCCPayments.value.length)
const totalPages = computed(() => Math.max(1, Math.ceil(totalCount.value / pageSize)))
const canGoPrevious = computed(() => page.value > 1)
const canGoNext = computed(() => page.value < totalPages.value)

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

const paginatedPayments = computed(() => {
  const start = (page.value - 1) * pageSize
  const end = start + pageSize
  return offlineCCPayments.value.slice(start, end)
})

function goToPage(p: number): void {
  if (p < 1 || p > totalPages.value) return
  page.value = p
}

function processPayment(id: number): void {
  console.log('Process payment:', id)
  // TODO: Implement payment processing
}

function viewInvoice(id: number): void {
  console.log('View invoice:', id)
  // TODO: Navigate to invoice detail
}

onMounted(() => {
  // Load offline CC payments
})
</script>

<template>
  <div class="p-4 sm:p-6 lg:p-8 w-full">

    <!-- Header -->
    <div class="mb-5">
      <h1 class="font-display font-bold text-[1.25rem] text-text-primary leading-none">Offline Credit Card Processing</h1>
      <p class="text-[0.78rem] text-text-secondary mt-1">Process offline credit card payments</p>
    </div>

    <!-- Record Count & Pagination -->
    <div v-if="totalCount > 0" class="flex items-center justify-between mb-4">
      <div class="text-[0.82rem] text-text-muted">
        {{ totalCount }} Records Found<span v-if="totalCount > 0">, Page {{ page }} of {{ totalPages }}</span>
      </div>
      <div class="flex items-center gap-2">
        <span class="text-[0.82rem] text-text-muted">Jump to Page:</span>
        <div class="w-20">
          <AppSelect
            v-model="pageString"
            :options="pageOptions"
            @update:modelValue="(val) => goToPage(Number(val))"
          />
        </div>
        <button class="px-3 py-1.5 text-[0.82rem] font-semibold text-white gradient-brand rounded-[9px] transition-opacity hover:opacity-90">
          Go
        </button>
      </div>
    </div>

    <!-- Table -->
    <div class="bg-surface-card border border-border rounded-2xl overflow-hidden">

      <!-- Header row -->
      <div class="hidden sm:grid grid-cols-[0.5fr_2fr_1.2fr_1.2fr_1fr_1fr] gap-4 px-5 py-3 border-b border-border bg-white/[0.02]">
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">ID</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Client Name</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Invoice Date</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Due Date</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Total</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Actions</span>
      </div>

      <!-- Rows or Empty state -->
      <template v-if="paginatedPayments.length > 0">
        <div
          v-for="payment in paginatedPayments"
          :key="payment.id"
          class="grid grid-cols-1 sm:grid-cols-[0.5fr_2fr_1.2fr_1.2fr_1fr_1fr] gap-2 sm:gap-4 px-5 py-3.5 border-b border-border last:border-0 hover:bg-white/[0.02] transition-colors items-center"
        >
          <span class="text-[0.82rem] text-text-muted font-mono">{{ payment.id }}</span>

          <span class="text-[0.82rem] text-text-secondary">{{ payment.clientName }}</span>

          <span class="text-[0.82rem] text-text-muted">{{ new Date(payment.invoiceDate).toLocaleDateString() }}</span>

          <span class="text-[0.82rem] text-text-muted">{{ new Date(payment.dueDate).toLocaleDateString() }}</span>

          <span class="text-[0.82rem] font-medium text-text-primary">{{ payment.total.toFixed(2) }} USD</span>

          <div class="flex items-center gap-1">
            <button
              @click="processPayment(payment.id)"
              class="p-1.5 text-text-muted hover:text-status-green hover:bg-white/[0.05] rounded transition-colors"
              title="Process"
            >
              <svg class="w-4 h-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                <path d="M12 5v14M5 12h14" />
              </svg>
            </button>
            <button
              @click="viewInvoice(payment.invoiceId)"
              class="p-1.5 text-text-muted hover:text-text-primary hover:bg-white/[0.05] rounded transition-colors"
              title="View"
            >
              <svg class="w-4 h-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z" />
                <circle cx="12" cy="12" r="3" />
              </svg>
            </button>
          </div>
        </div>
      </template>

      <!-- Empty state -->
      <div v-else class="px-5 py-8 text-center text-text-secondary">
        <p class="text-[0.82rem]">No Records Found</p>
      </div>
    </div>

    <!-- Pagination Navigation -->
    <div v-if="totalCount > 0" class="flex items-center justify-between mt-4">
      <button
        @click="goToPage(page - 1)"
        :disabled="!canGoPrevious"
        class="px-3 py-2 text-[0.82rem] font-semibold text-text-primary hover:text-primary-500 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
      >
        « Previous Page
      </button>
      <button
        @click="goToPage(page + 1)"
        :disabled="!canGoNext"
        class="px-3 py-2 text-[0.82rem] font-semibold text-text-primary hover:text-primary-500 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
      >
        Next Page »
      </button>
    </div>

  </div>
</template>
