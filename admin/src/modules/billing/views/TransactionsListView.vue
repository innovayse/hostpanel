<script setup lang="ts">
import { onMounted, ref, computed } from 'vue'
import { useRouter } from 'vue-router'
import AppSelect from '../../../components/AppSelect.vue'
import AppSpinner from '../../../components/AppSpinner.vue'
import DateRangePicker from '../../../components/DateRangePicker.vue'
import TransactionChart from '../components/TransactionChart.vue'
import { useTransactionsStore } from '../stores/transactionsStore'

const store = useTransactionsStore()
const router = useRouter()
const page = ref(1)
const isFilterOpen = ref(false)

const showOptions = [
  { value: 'all', label: 'All Activity' },
  { value: 'in', label: 'Payments Received' },
  { value: 'out', label: 'Payments Sent' },
]

const paymentMethodOptions = [
  { value: '', label: 'Any' },
  { value: 'Stripe', label: 'Stripe' },
  { value: 'PayPal', label: 'PayPal' },
  { value: 'Credit/Debit Card', label: 'Credit/Debit Card' },
]

// Filter state (UI inputs)
const filterShow = ref('all')
const filterDateRange = ref<[string, string] | null>(null)
const filterDescription = ref('')
const filterAmount = ref(0)
const filterTransactionId = ref('')
const filterPaymentMethod = ref('')

// Applied filters (only updated when Search is clicked)
const appliedFilters = ref({
  show: 'all',
  dateRange: null as [string, string] | null,
  description: '',
  amount: 0,
  transactionId: '',
  paymentMethod: ''
})

const totalPages = computed(() => Math.max(1, Math.ceil(store.totalCount / store.pageSize)))

const canGoPrevious = computed(() => page.value > 1)
const canGoNext = computed(() => page.value < totalPages.value)

const pageString = computed({
  get: () => String(page.value),
  set: (val) => { page.value = Number(val) }
})

// Calculate date range duration in days
const dateRangeDuration = computed(() => {
  if (!appliedFilters.value.dateRange) return null
  const [startStr, endStr] = appliedFilters.value.dateRange
  const startDate = new Date(startStr)
  const endDate = new Date(endStr)
  const durationMs = endDate.getTime() - startDate.getTime()
  const durationDays = Math.ceil(durationMs / (1000 * 60 * 60 * 24)) + 1
  return durationDays
})

const pageOptions = computed(() => {
  const options = []
  for (let i = 1; i <= totalPages.value; i++) {
    options.push({ value: String(i), label: String(i) })
  }
  return options
})

const filteredTransactions = computed(() => {
  const transactions = store.transactions || []
  return transactions.filter(tx => {
    if (appliedFilters.value.show !== 'all') {
      if (appliedFilters.value.show === 'in' && !(tx.amountIn > 0)) return false
      if (appliedFilters.value.show === 'out' && !(tx.amountOut > 0)) return false
    }

    if (appliedFilters.value.paymentMethod && tx.paymentMethod !== appliedFilters.value.paymentMethod) return false

    if (appliedFilters.value.dateRange) {
      const [startStr, endStr] = appliedFilters.value.dateRange
      const txDate = new Date(tx.date).toISOString().split('T')[0]
      if (txDate < startStr || txDate > endStr) return false
    }

    if (appliedFilters.value.description && !tx.description.toLowerCase().includes(appliedFilters.value.description.toLowerCase())) return false

    if (appliedFilters.value.amount > 0 && tx.amountIn !== appliedFilters.value.amount && tx.amountOut !== appliedFilters.value.amount) return false

    if (appliedFilters.value.transactionId && !tx.transactionId?.toLowerCase().includes(appliedFilters.value.transactionId.toLowerCase())) return false

    return true
  })
})

// Calculate statistics from filtered transactions with comparison
const stats = computed(() => {
  const transactions = filteredTransactions.value || []
  const allTransactions = store.transactions || []

  // Current period stats
  let totalIn = 0
  let totalOut = 0
  let totalFees = 0

  transactions.forEach(tx => {
    totalIn += tx.amountIn || 0
    totalOut += tx.amountOut || 0
    totalFees += tx.fees || 0
  })

  // Calculate previous period for comparison
  let prevTotalIn = 0
  let prevTotalOut = 0
  let prevTotalFees = 0

  if (appliedFilters.value.dateRange) {
    // If date range is selected, compare with same range 30 days earlier
    const [startStr, endStr] = appliedFilters.value.dateRange
    const startDate = new Date(startStr)
    const endDate = new Date(endStr)
    const rangeDuration = endDate.getTime() - startDate.getTime()

    const prevEndDate = new Date(startDate.getTime() - 1000 * 60 * 60 * 24) // 1 day before start
    const prevStartDate = new Date(prevEndDate.getTime() - rangeDuration)
    const prevStartStr = prevStartDate.toISOString().split('T')[0]
    const prevEndStr = prevEndDate.toISOString().split('T')[0]

    // Apply same filters but with previous date range
    allTransactions.forEach(tx => {
      const txDate = new Date(tx.date).toISOString().split('T')[0]
      if (txDate >= prevStartStr && txDate <= prevEndStr) {
        if (appliedFilters.value.show !== 'all') {
          if (appliedFilters.value.show === 'in' && !(tx.amountIn > 0)) return
          if (appliedFilters.value.show === 'out' && !(tx.amountOut > 0)) return
        }
        if (appliedFilters.value.paymentMethod && tx.paymentMethod !== appliedFilters.value.paymentMethod) return
        if (appliedFilters.value.description && !tx.description.toLowerCase().includes(appliedFilters.value.description.toLowerCase())) return
        prevTotalIn += tx.amountIn || 0
        prevTotalOut += tx.amountOut || 0
        prevTotalFees += tx.fees || 0
      }
    })
  } else {
    // No date range - compare last 30 days with previous 30 days
    const today = new Date()
    const thirtyDaysAgo = new Date(today)
    thirtyDaysAgo.setDate(thirtyDaysAgo.getDate() - 30)
    const sixtyDaysAgo = new Date(today)
    sixtyDaysAgo.setDate(sixtyDaysAgo.getDate() - 60)

    allTransactions.forEach(tx => {
      const txDate = new Date(tx.date)
      if (txDate >= sixtyDaysAgo && txDate < thirtyDaysAgo) {
        if (appliedFilters.value.show !== 'all') {
          if (appliedFilters.value.show === 'in' && !(tx.amountIn > 0)) return
          if (appliedFilters.value.show === 'out' && !(tx.amountOut > 0)) return
        }
        if (appliedFilters.value.paymentMethod && tx.paymentMethod !== appliedFilters.value.paymentMethod) return
        if (appliedFilters.value.description && !tx.description.toLowerCase().includes(appliedFilters.value.description.toLowerCase())) return
        prevTotalIn += tx.amountIn || 0
        prevTotalOut += tx.amountOut || 0
        prevTotalFees += tx.fees || 0
      }
    })
  }

  // Calculate percentage changes
  const incomeChange = prevTotalIn > 0 ? ((totalIn - prevTotalIn) / prevTotalIn) * 100 : (totalIn > 0 ? 100 : 0)
  const feesChange = prevTotalFees > 0 ? ((totalFees - prevTotalFees) / prevTotalFees) * 100 : (totalFees > 0 ? 100 : 0)
  const expenditureChange = prevTotalOut > 0 ? ((totalOut - prevTotalOut) / prevTotalOut) * 100 : (totalOut > 0 ? 100 : 0)

  return {
    totalIncome: totalIn || 0,
    totalExpense: totalOut || 0,
    totalFees: totalFees || 0,
    netFlow: (totalIn - totalOut) || 0,
    incomeChange: Math.round(incomeChange),
    feesChange: Math.round(feesChange),
    expenditureChange: Math.round(expenditureChange)
  }
})

function applyFilters(): void {
  appliedFilters.value = {
    show: filterShow.value,
    dateRange: filterDateRange.value,
    description: filterDescription.value,
    amount: filterAmount.value,
    transactionId: filterTransactionId.value,
    paymentMethod: filterPaymentMethod.value
  }
  page.value = 1
}

function clearFilters(): void {
  filterShow.value = 'all'
  filterDateRange.value = null
  filterDescription.value = ''
  filterAmount.value = 0
  filterTransactionId.value = ''
  filterPaymentMethod.value = ''
  appliedFilters.value = {
    show: 'all',
    dateRange: null,
    description: '',
    amount: 0,
    transactionId: '',
    paymentMethod: ''
  }
  page.value = 1
}

function goToPage(p: number): void {
  if (p < 1 || p > totalPages.value) return
  page.value = p
  store.fetchAll(p, store.pageSize)
}

onMounted(() => store.fetchAll())
</script>

<template>
  <div class="p-4 sm:p-6 lg:p-8 w-full">

    <!-- Header with Add Button -->
    <div class="flex items-start justify-between mb-5">
      <div>
        <h1 class="font-display font-bold text-[1.25rem] text-text-primary leading-none">Transactions</h1>
        <p class="text-[0.78rem] text-text-secondary mt-1">Payment transactions and records</p>
      </div>
      <button
        class="gradient-brand flex items-center gap-1.5 px-4 py-2 text-[0.82rem] font-semibold text-white rounded-[9px] transition-opacity hover:opacity-90 shrink-0"
        @click="router.push('/billing/add-transaction')"
      >
        <svg class="w-3.5 h-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round">
          <line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/>
        </svg>
        Add Transaction
      </button>
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
      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-3 mb-4">
        <div>
          <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Show</label>
          <AppSelect
            v-model="filterShow"
            :options="showOptions"
          />
        </div>

        <div>
          <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Payment Method</label>
          <AppSelect
            v-model="filterPaymentMethod"
            :options="paymentMethodOptions"
          />
        </div>

        <div class="lg:col-span-2">
          <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Date Range</label>
          <DateRangePicker
            v-model="filterDateRange"
            placeholder="Select date range..."
          />
        </div>

        <div>
          <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Description</label>
          <input
            v-model="filterDescription"
            type="text"
            placeholder="Search..."
            class="w-full px-3 py-2 text-[0.82rem] text-text-primary bg-white/[0.05] border border-border rounded-[9px] placeholder:text-text-muted focus:outline-none focus:border-primary-500/40 transition-colors"
          />
        </div>

        <div>
          <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Amount</label>
          <AppSpinner
            v-model="filterAmount"
            :step="0.01"
            :min="0"
            placeholder="0.00"
          />
        </div>

        <div>
          <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Transaction ID</label>
          <input
            v-model="filterTransactionId"
            type="text"
            placeholder="Search..."
            class="w-full px-3 py-2 text-[0.82rem] text-text-primary bg-white/[0.05] border border-border rounded-[9px] placeholder:text-text-muted focus:outline-none focus:border-primary-500/40 transition-colors"
          />
        </div>
      </div>

      <div class="flex items-center justify-center gap-2">
        <button
          class="gradient-brand flex items-center gap-1.5 px-4 py-2 text-[0.82rem] font-semibold text-white rounded-[9px] transition-opacity hover:opacity-90"
          @click="applyFilters"
        >
          <svg class="w-3.5 h-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round">
            <circle cx="11" cy="11" r="8"/><line x1="21" y1="21" x2="16.65" y2="16.65"/>
          </svg>
          Search
        </button>
        <button
          class="px-3 py-2 text-[0.78rem] text-text-muted hover:text-text-primary transition-colors"
          @click="clearFilters"
        >
          Clear
        </button>
      </div>
    </div>

    <!-- Loading -->
    <div v-if="store.loading && store.transactions.length === 0" class="flex items-center gap-3 text-text-secondary text-sm">
      <span class="w-4 h-4 rounded-full border-2 border-primary-500/20 border-t-primary-500 animate-spin" />
      Loading transactions…
    </div>

    <!-- Error -->
    <div v-else-if="store.error" class="text-sm text-status-red bg-status-red/8 border border-status-red/20 rounded-xl p-4">
      {{ store.error }}
    </div>

    <!-- Pagination Info and Table -->
    <div v-else>
      <!-- Chart Section -->
      <div v-if="stats" class="bg-surface-card border border-border rounded-2xl p-6 mb-6">
        <div class="grid grid-cols-1 lg:grid-cols-4 gap-6">
          <!-- Chart -->
          <div class="lg:col-span-3">
            <h3 class="text-[0.82rem] font-semibold text-text-muted mb-4 uppercase">Net Revenue (USD)</h3>
            <TransactionChart :transactions="filteredTransactions" />
          </div>

          <!-- Stats -->
          <div class="space-y-4">
            <div>
              <div class="flex items-center gap-2 mb-2">
                <div class="w-6 h-6 rounded bg-primary-500/20 flex items-center justify-center text-[0.65rem]">💰</div>
                <span class="text-[0.75rem] font-semibold text-text-muted uppercase">Total Income</span>
              </div>
              <p class="text-[1.1rem] font-bold text-status-green">{{ (stats?.totalIncome || 0).toFixed(2) }} USD</p>
              <p class="text-[0.7rem]" :class="(stats?.incomeChange || 0) >= 0 ? 'text-status-green' : 'text-status-red'" v-if="dateRangeDuration">
                {{ (stats?.incomeChange || 0) >= 0 ? '↑' : '↓' }} {{ Math.abs(stats?.incomeChange || 0) }}% from previous {{ dateRangeDuration }} days
              </p>
              <p class="text-[0.7rem] text-text-muted mt-1" v-else>
                {{ (stats?.incomeChange || 0) >= 0 ? '↑' : '↓' }} {{ Math.abs(stats?.incomeChange || 0) }}% from previous 30 days
              </p>
            </div>

            <div>
              <div class="flex items-center gap-2 mb-2">
                <div class="w-6 h-6 rounded bg-primary-500/20 flex items-center justify-center text-[0.65rem]">💵</div>
                <span class="text-[0.75rem] font-semibold text-text-muted uppercase">Total Fees</span>
              </div>
              <p class="text-[1.1rem] font-bold text-text-primary">{{ (stats?.totalFees || 0).toFixed(2) }} USD</p>
              <p class="text-[0.7rem]" :class="(stats?.feesChange || 0) >= 0 ? 'text-status-red' : 'text-status-green'" v-if="dateRangeDuration">
                {{ (stats?.feesChange || 0) >= 0 ? '↑' : '↓' }} {{ Math.abs(stats?.feesChange || 0) }}% from previous {{ dateRangeDuration }} days
              </p>
              <p class="text-[0.7rem] text-text-muted mt-1" v-else>
                {{ (stats?.feesChange || 0) >= 0 ? '↑' : '↓' }} {{ Math.abs(stats?.feesChange || 0) }}% from previous 30 days
              </p>
            </div>

            <div>
              <div class="flex items-center gap-2 mb-2">
                <div class="w-6 h-6 rounded bg-primary-500/20 flex items-center justify-center text-[0.65rem]">📊</div>
                <span class="text-[0.75rem] font-semibold text-text-muted uppercase">Total Expenditure</span>
              </div>
              <p class="text-[1.1rem] font-bold text-status-red">{{ (stats?.totalExpense || 0).toFixed(2) }} USD</p>
              <p class="text-[0.7rem]" :class="(stats?.expenditureChange || 0) >= 0 ? 'text-status-red' : 'text-status-green'" v-if="dateRangeDuration">
                {{ (stats?.expenditureChange || 0) >= 0 ? '↑' : '↓' }} {{ Math.abs(stats?.expenditureChange || 0) }}% from previous {{ dateRangeDuration }} days
              </p>
              <p class="text-[0.7rem] text-text-muted mt-1" v-else>
                {{ (stats?.expenditureChange || 0) >= 0 ? '↑' : '↓' }} {{ Math.abs(stats?.expenditureChange || 0) }}% from previous 30 days
              </p>
            </div>
          </div>
        </div>
      </div>

      <!-- Pagination Info -->
      <div class="flex items-center justify-between mb-4">
        <div class="text-[0.82rem] text-text-muted">
          {{ store.totalCount }} Records Found<span v-if="store.totalCount > 0">, Showing {{ (page - 1) * store.pageSize + 1 }} to {{ Math.min(page * store.pageSize, store.totalCount) }}</span>
        </div>
        <div v-if="store.totalCount > 0" class="flex items-center gap-2">
          <span class="text-[0.82rem] text-text-muted">Jump to Page:</span>
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
        <div class="hidden sm:grid grid-cols-[1.2fr_0.9fr_1.2fr_1.5fr_1fr_0.9fr_0.9fr_0.8fr] gap-4 px-5 py-3 border-b border-border bg-white/[0.02]">
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Client Name</span>
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Date</span>
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Payment Method</span>
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Description</span>
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Amount In</span>
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Fees</span>
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Amount Out</span>
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Actions</span>
        </div>

        <!-- Rows or Empty state -->
        <template v-if="filteredTransactions.length > 0">
          <div
            v-for="tx in filteredTransactions"
            :key="tx.id"
            class="grid grid-cols-1 sm:grid-cols-[1.2fr_0.9fr_1.2fr_1.5fr_1fr_0.9fr_0.9fr_0.8fr] gap-2 sm:gap-4 px-5 py-3.5 border-b border-border last:border-0 hover:bg-white/[0.02] transition-colors items-center"
          >
            <!-- Client Name -->
            <span class="text-[0.82rem] text-text-secondary">{{ tx.clientName }}</span>

            <!-- Date -->
            <span class="text-[0.82rem] text-text-muted">{{ new Date(tx.date).toLocaleDateString() }}</span>

            <!-- Payment Method -->
            <span class="text-[0.82rem] text-text-primary">{{ tx.paymentMethod || 'Manual' }}</span>

            <!-- Description -->
            <span class="text-[0.82rem] text-text-secondary truncate">{{ tx.description }}</span>

            <!-- Amount In -->
            <span class="text-[0.82rem] font-medium" :class="tx.amountIn > 0 ? 'text-status-green' : 'text-text-muted'">
              {{ tx.amountIn > 0 ? tx.amountIn.toFixed(2) : '—' }}
            </span>

            <!-- Fees -->
            <span class="text-[0.82rem] font-medium text-text-primary">{{ (tx.fees || 0).toFixed(2) }}</span>

            <!-- Amount Out -->
            <span class="text-[0.82rem] font-medium" :class="tx.amountOut > 0 ? 'text-status-red' : 'text-text-muted'">
              {{ tx.amountOut > 0 ? tx.amountOut.toFixed(2) : '—' }}
            </span>

            <!-- Actions -->
            <div class="flex items-center gap-2">
              <router-link
                :to="`/billing/transactions/${tx.id}/edit`"
                class="p-1.5 text-text-muted hover:text-text-primary hover:bg-white/[0.05] rounded transition-colors"
                title="Edit"
              >
                <svg class="w-4 h-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                  <path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7" />
                  <path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z" />
                </svg>
              </router-link>
              <button
                @click="() => {}"
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
          <p class="text-[0.82rem]">No transactions found.</p>
        </div>
      </div>

      <!-- Pagination Navigation -->
      <div v-if="store.totalCount > 0" class="flex items-center justify-between mt-4">
        <button
          @click="goToPage(page - 1)"
          :disabled="!canGoPrevious"
          class="px-3 py-2 text-[0.82rem] font-semibold text-text-primary hover:text-primary-500 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
        >
          <svg class="w-4 h-4 inline mr-1" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
            <path d="M15 18l-6-6 6-6"></path>
          </svg>
          Previous Page
        </button>
        <button
          @click="goToPage(page + 1)"
          :disabled="!canGoNext"
          class="px-3 py-2 text-[0.82rem] font-semibold text-text-primary hover:text-primary-500 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
        >
          Next Page
          <svg class="w-4 h-4 inline ml-1" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
            <path d="M9 18l6-6-6-6"></path>
          </svg>
        </button>
      </div>
    </div>

  </div>
</template>
