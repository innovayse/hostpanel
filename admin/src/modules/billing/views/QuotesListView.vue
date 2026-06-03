<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import AppSelect from '../../../components/AppSelect.vue'
import AppClientSelect from '../../../components/AppClientSelect.vue'
import AppSpinner from '../../../components/AppSpinner.vue'
import DateRangePicker from '../../../components/DateRangePicker.vue'
import { useQuoteStore } from '../stores/quoteStore'

const store = useQuoteStore()
const router = useRouter()
const page = ref(1)
const isFilterOpen = ref(false)
const clients = ref<{ id: number; name: string; email?: string; status?: string }[]>([])

const stageOptions = [
  { value: 'any', label: 'Any' },
  { value: 'Draft', label: 'Draft' },
  { value: 'Delivered', label: 'Delivered' },
  { value: 'OnHold', label: 'On Hold' },
  { value: 'Accepted', label: 'Accepted' },
  { value: 'Lost', label: 'Lost' },
  { value: 'Expired', label: 'Expired' },
  { value: 'Dead', label: 'Dead' },
]

const validityPeriodOptions = [
  { value: 'any', label: 'Any' },
  { value: 'valid', label: 'Valid' },
  { value: 'expired', label: 'Expired' },
]

const statusColorMap = {
  'Draft': 'bg-primary-500/15 text-primary-400',
  'Delivered': 'bg-status-blue/15 text-status-blue',
  'OnHold': 'bg-status-yellow/15 text-status-yellow',
  'Accepted': 'bg-status-green/15 text-status-green',
  'Lost': 'bg-status-red/15 text-status-red',
  'Expired': 'bg-text-muted/15 text-text-muted',
  'Dead': 'bg-text-muted/15 text-text-muted',
}

const filters = ref({
  clientId: '',
  subject: '',
  stage: 'any',
  validityPeriod: 'any',
})

const filteredQuotes = computed(() => {
  return store.quotes.filter(quote => {
    if (filters.value.clientId && String(quote.clientId) !== String(filters.value.clientId)) return false
    if (filters.value.subject && !quote.subject.toLowerCase().includes(filters.value.subject.toLowerCase())) return false
    if (filters.value.stage !== 'any' && quote.stage !== filters.value.stage) return false

    if (filters.value.validityPeriod !== 'any') {
      const expiryDate = new Date(quote.validUntil)
      const today = new Date()
      const isExpired = expiryDate < today
      if (filters.value.validityPeriod === 'expired' && !isExpired) return false
      if (filters.value.validityPeriod === 'valid' && isExpired) return false
    }

    return true
  })
})

const pageSize = 20
const currentPage = computed({
  get: () => page.value,
  set: (val) => { page.value = val }
})

const totalPages = computed(() => {
  return Math.max(1, Math.ceil(store.totalCount / pageSize))
})

const paginatedQuotes = computed(() => {
  const start = (currentPage.value - 1) * pageSize
  const end = start + pageSize
  return filteredQuotes.value.slice(start, end)
})

const pageOptions = computed(() => {
  const options = []
  for (let i = 1; i <= totalPages.value; i++) {
    options.push({ value: String(i), label: String(i) })
  }
  return options
})

const pageString = computed({
  get: () => String(page.value),
  set: (val) => { page.value = Number(val) }
})

function loadClients() {
  clients.value = [
    { id: 1, name: 'John Doe', email: 'john@example.com', status: 'active' },
    { id: 2, name: 'Jane Smith (Acme Corp)', email: 'jane@example.com', status: 'active' },
    { id: 3, name: 'Bob Johnson', email: 'bob@example.com', status: 'active' },
    { id: 4, name: 'Alice Williams (Tech Inc)', email: 'alice@example.com', status: 'inactive' },
  ]
}

function applyFilters(): void {
  page.value = 1
  store.fetchAll(1, store.pageSize)
}

function clearFilters(): void {
  filters.value.clientId = ''
  filters.value.subject = ''
  filters.value.stage = 'any'
  filters.value.validityPeriod = 'any'
  page.value = 1
  store.fetchAll(1, store.pageSize)
}

function goToPage(p: number): void {
  if (p < 1 || p > totalPages.value) return
  page.value = p
  store.fetchAll(p, store.pageSize)
}

onMounted(() => {
  store.fetchAll()
  loadClients()
})
</script>

<template>
  <div class="p-4 sm:p-6 lg:p-8 w-full">

    <!-- Header with Add Button -->
    <div class="flex items-start justify-between mb-5">
      <div>
        <h1 class="font-display font-bold text-[1.25rem] text-text-primary leading-none">Quotes</h1>
        <p class="text-[0.78rem] text-text-secondary mt-1">Customer quotes and proposals</p>
      </div>
      <button
        class="gradient-brand flex items-center gap-1.5 px-4 py-2 text-[0.82rem] font-semibold text-white rounded-[9px] transition-opacity hover:opacity-90 shrink-0"
        @click="router.push('/billing/quotes/add')"
      >
        <svg class="w-3.5 h-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round">
          <line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/>
        </svg>
        Create Quote
      </button>
    </div>

    <!-- Search/Filter Button -->
    <div class="mb-4">
      <button
        type="button"
        @click="isFilterOpen = !isFilterOpen"
        class="px-4 py-2 text-[0.82rem] font-semibold text-text-primary border border-border rounded-[9px] hover:bg-white/[0.02] transition-colors cursor-pointer"
      >
        {{ isFilterOpen ? 'Hide Filters' : 'Search/Filter' }}
      </button>
    </div>

    <!-- Filters Panel -->
    <div v-if="isFilterOpen" class="border border-border rounded-2xl p-6 mb-4 bg-white/[0.02]">
      <div class="grid grid-cols-2 gap-6 mb-6">
        <!-- Left Column -->
        <div class="space-y-4">
          <div>
            <label class="block text-[0.82rem] font-medium text-text-primary mb-2">Subject</label>
            <input
              v-model="filters.subject"
              type="text"
              placeholder=""
              class="w-full px-3 py-2 text-[0.82rem] text-text-primary bg-white/[0.05] border border-border rounded-[4px] focus:outline-none focus:border-primary-500/40 transition-colors"
            />
          </div>
          <div>
            <label class="block text-[0.82rem] font-medium text-text-primary mb-2">Stage</label>
            <AppSelect
              v-model="filters.stage"
              :options="stageOptions"
            />
          </div>
        </div>

        <!-- Right Column -->
        <div class="space-y-4">
          <div>
            <label class="block text-[0.82rem] font-medium text-text-primary mb-2">Client</label>
            <AppClientSelect
              v-model="filters.clientId"
              :clients="clients"
              placeholder="Start Typing to Search Clients"
            />
          </div>
          <div>
            <label class="block text-[0.82rem] font-medium text-text-primary mb-2">Validity Period</label>
            <AppSelect
              v-model="filters.validityPeriod"
              :options="validityPeriodOptions"
            />
          </div>
        </div>
      </div>

      <!-- Filter Buttons -->
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
    <div v-if="store.loading && store.quotes.length === 0" class="flex items-center gap-3 text-text-secondary text-sm">
      <span class="w-4 h-4 rounded-full border-2 border-primary-500/20 border-t-primary-500 animate-spin" />
      Loading quotes…
    </div>

    <!-- Error -->
    <div v-else-if="store.error" class="text-sm text-status-red bg-status-red/8 border border-status-red/20 rounded-xl p-4">
      {{ store.error }}
    </div>

    <!-- Pagination Info -->
    <div v-else class="flex items-center justify-between mb-4">
      <div class="text-[0.82rem] text-text-muted">
        {{ store.totalCount }} Records Found<span v-if="store.totalCount > 0">, Showing {{ (currentPage - 1) * pageSize + 1 }} to {{ Math.min(currentPage * pageSize, store.totalCount) }}</span>
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
    <div v-if="store.loading === false && store.error === null" class="bg-surface-card border border-border rounded-2xl overflow-hidden">

      <!-- Header row -->
      <div class="hidden sm:grid grid-cols-[0.5fr_1.5fr_2fr_1fr_1.5fr_1.5fr_1.2fr] gap-4 px-5 py-3 border-b border-border bg-white/[0.02]">
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">ID</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Client Name</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Subject</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Status</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Total</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Expiry Date</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Created</span>
      </div>

      <!-- Rows -->
      <template v-if="paginatedQuotes.length > 0">
        <div
          v-for="quote in paginatedQuotes"
          :key="quote.id"
          class="grid grid-cols-1 sm:grid-cols-[0.5fr_1.5fr_2fr_1fr_1.5fr_1.5fr_1.2fr] gap-2 sm:gap-4 px-5 py-3.5 border-b border-border last:border-0 hover:bg-white/[0.02] transition-colors items-center"
        >
          <span class="text-[0.82rem] text-text-muted font-mono hidden sm:block">{{ quote.id }}</span>

          <span class="text-[0.82rem] text-text-secondary">
            {{ quote.clientName }}
          </span>

          <span class="text-[0.82rem] font-medium text-text-primary truncate">{{ quote.subject }}</span>

          <span>
            <span
              :class="statusColorMap[quote.stage as keyof typeof statusColorMap] || 'bg-text-muted/15 text-text-muted'"
              class="px-2 py-1 rounded-full text-[0.72rem] font-medium inline-block"
            >
              {{ quote.stage }}
            </span>
          </span>

          <span class="text-[0.82rem] font-medium text-text-primary">{{ quote.total.toFixed(2) }} USD</span>

          <span class="text-[0.82rem] text-text-muted">{{ new Date(quote.validUntil).toLocaleDateString() }}</span>

          <span class="text-[0.82rem] text-text-muted">{{ new Date(quote.dateCreated).toLocaleDateString() }}</span>
        </div>
      </template>

      <!-- Empty state -->
      <div v-else class="px-5 py-8 text-center text-text-secondary">
        <p class="text-[0.82rem]">No quotes found.</p>
      </div>
    </div>

  </div>
</template>
