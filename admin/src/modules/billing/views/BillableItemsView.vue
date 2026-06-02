<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import AppSelect from '../../../components/AppSelect.vue'
import AppClientSelect from '../../../components/AppClientSelect.vue'
import AppSpinner from '../../../components/AppSpinner.vue'
import AppCheckbox from '../../../components/AppCheckbox.vue'
import { useBillableItemsStore } from '../stores/billableItemsStore'

const store = useBillableItemsStore()
const router = useRouter()

const clients = ref<{ id: number; name: string; email?: string; status?: string }[]>([])
const selectedItems = ref<Set<number>>(new Set())
const showFilters = ref(false)

const filters = ref({
  clientId: '',
  description: '',
  amount: 0,
  status: 'any',
})

const statusOptions = [
  { value: 'any', label: 'Any' },
  { value: 'uninvoiced', label: 'Uninvoiced' },
  { value: 'invoiced', label: 'Invoiced' },
  { value: 'recurring', label: 'Recurring' },
  { value: 'active-recurring', label: 'Active Recurring' },
  { value: 'completed-recurring', label: 'Completed Recurring' },
]

const filteredItems = computed(() => {
  return store.items.filter(item => {
    if (filters.value.clientId && String(item.clientId) !== String(filters.value.clientId)) return false
    if (filters.value.description && !item.description.toLowerCase().includes(filters.value.description.toLowerCase())) return false
    if (filters.value.amount > 0 && item.amount !== filters.value.amount) return false

    if (filters.value.status !== 'any') {
      switch (filters.value.status) {
        case 'uninvoiced':
          if (item.isInvoiced) return false
          break
        case 'invoiced':
          if (!item.isInvoiced) return false
          break
        case 'recurring':
          if (item.type !== 'Recurring') return false
          break
        case 'active-recurring':
          if (item.type !== 'Recurring' || item.isInvoiced) return false
          break
        case 'completed-recurring':
          if (item.type !== 'Recurring' || !item.isInvoiced) return false
          break
      }
    }
    return true
  })
})

const toggleSelection = (id: number) => {
  if (selectedItems.value.has(id)) {
    selectedItems.value.delete(id)
  } else {
    selectedItems.value.add(id)
  }
}

const toggleSelectAll = () => {
  if (selectedItems.value.size === filteredItems.value.length) {
    selectedItems.value.clear()
  } else {
    filteredItems.value.forEach(item => selectedItems.value.add(item.id))
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

async function deleteSelected() {
  const ids = Array.from(selectedItems.value)
  for (const id of ids) {
    await store.removeItem(id)
  }
  selectedItems.value.clear()
}

function toggleFilters() {
  showFilters.value = !showFilters.value
}

const pageSize = 20
const currentPage = ref(1)

const totalPages = computed(() => {
  return Math.max(1, Math.ceil(filteredItems.value.length / pageSize))
})

const canGoPrevious = computed(() => currentPage.value > 1)
const canGoNext = computed(() => currentPage.value < totalPages.value)

const paginatedItems = computed(() => {
  const start = (currentPage.value - 1) * pageSize
  const end = start + pageSize
  return filteredItems.value.slice(start, end)
})

const pageOptions = computed(() => {
  const options = []
  for (let i = 1; i <= totalPages.value; i++) {
    options.push({ value: String(i), label: String(i) })
  }
  return options
})

const currentPageString = computed({
  get: () => String(currentPage.value),
  set: (val) => { currentPage.value = Number(val) }
})

onMounted(() => {
  store.fetchAll()
  loadClients()
})
</script>

<template>
  <div class="p-4 sm:p-6 lg:p-8 w-full">

    <!-- Header -->
    <div class="flex items-start justify-between mb-5">
      <div>
        <h1 class="font-display font-bold text-[1.25rem] text-text-primary leading-none">Billable Items</h1>
        <p class="text-[0.78rem] text-text-secondary mt-1">Manage uninvoiced and recurring charges</p>
      </div>
      <button
        class="gradient-brand flex items-center gap-1.5 px-4 py-2 text-[0.82rem] font-semibold text-white rounded-[9px] transition-opacity hover:opacity-90 shrink-0"
        @click="router.push('/billing/billable-items/add')"
      >
        <svg class="w-3.5 h-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round">
          <line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/>
        </svg>
        Add Item
      </button>
    </div>

    <!-- Search/Filter Button -->
    <div class="mb-4">
      <button
        type="button"
        @click.stop="toggleFilters"
        class="px-4 py-2 text-[0.82rem] font-semibold text-text-primary border border-border rounded-[9px] hover:bg-white/[0.02] transition-colors cursor-pointer"
      >
        {{ showFilters ? 'Hide Filters' : 'Search/Filter' }}
      </button>
    </div>

    <!-- Filters Panel -->
    <div v-if="showFilters" class="bg-surface-card border border-border rounded-2xl p-4 mb-4">
      <div class="grid grid-cols-1 lg:grid-cols-2 gap-4">
        <div>
          <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Client</label>
          <AppClientSelect
            v-model="filters.clientId"
            :clients="clients"
            placeholder="Start Typing to Search Clients"
          />
        </div>
        <div>
          <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Description</label>
          <input
            v-model="filters.description"
            type="text"
            placeholder="Search description..."
            class="w-full px-3 py-2 text-[0.82rem] text-text-primary bg-white/[0.05] border border-border rounded-[9px] focus:outline-none focus:border-primary-500/40 transition-colors"
          />
        </div>
        <div>
          <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Amount</label>
          <AppSpinner
            v-model="filters.amount"
            :step="0.01"
            :min="0"
            placeholder="0.00"
          />
        </div>
        <div>
          <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Status</label>
          <AppSelect
            v-model="filters.status"
            :options="statusOptions"
          />
        </div>
      </div>

      <div class="flex items-center justify-center gap-2 mt-4">
        <button
          class="gradient-brand flex items-center gap-1.5 px-4 py-2 text-[0.82rem] font-semibold text-white rounded-[9px] transition-opacity hover:opacity-90"
          @click="showFilters = false"
        >
          <svg class="w-3.5 h-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round">
            <circle cx="11" cy="11" r="8"/><line x1="21" y1="21" x2="16.65" y2="16.65"/>
          </svg>
          Search
        </button>
        <button
          class="px-3 py-2 text-[0.78rem] text-text-muted hover:text-text-primary transition-colors"
          @click="() => { filters = { clientId: '', description: '', amount: 0, status: 'any' }; showFilters = false }"
        >
          Clear
        </button>
      </div>
    </div>

    <!-- Loading -->
    <div v-if="store.loading && store.items.length === 0" class="flex items-center gap-3 text-text-secondary text-sm">
      <span class="w-4 h-4 rounded-full border-2 border-primary-500/20 border-t-primary-500 animate-spin" />
      Loading items…
    </div>

    <!-- Error -->
    <div v-else-if="store.error" class="text-sm text-status-red bg-status-red/8 border border-status-red/20 rounded-xl p-4">
      {{ store.error }}
    </div>

    <!-- Pagination Info -->
    <div v-else class="flex items-center justify-between mb-4">
      <div class="text-[0.82rem] text-text-muted">
        {{ filteredItems.length }} Records Found<span v-if="filteredItems.length > 0">, Showing {{ (currentPage - 1) * pageSize + 1 }} to {{ Math.min(currentPage * pageSize, filteredItems.length) }}</span>
      </div>
      <div v-if="filteredItems.length > 0" class="flex items-center gap-2">
        <span class="text-[0.82rem] text-text-muted">Jump to Page:</span>
        <div class="w-20">
          <AppSelect
            v-model="currentPageString"
            :options="pageOptions"
          />
        </div>
      </div>
    </div>

    <!-- Table -->
    <div v-if="store.loading === false && store.error === null" class="bg-surface-card border border-border rounded-2xl overflow-hidden">

      <!-- Header row -->
      <div class="hidden sm:grid grid-cols-[40px_0.5fr_1.5fr_1.5fr_0.75fr_1fr_1.5fr_1fr] gap-4 px-5 py-3 border-b border-border bg-white/[0.02]">
        <AppCheckbox
          v-if="filteredItems.length > 0"
          :model-value="selectedItems.size === filteredItems.length && filteredItems.length > 0"
          @update:model-value="toggleSelectAll"
        />
        <div v-else></div>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">ID</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Client Name</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Description</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Hours</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Amount</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Invoice Action</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Invoiced</span>
      </div>

      <!-- Rows -->
      <template v-if="filteredItems.length > 0">
        <div
          v-for="item in paginatedItems"
          :key="item.id"
          class="grid grid-cols-1 sm:grid-cols-[40px_0.5fr_1.5fr_1.5fr_0.75fr_1fr_1.5fr_1fr] gap-2 sm:gap-4 px-5 py-3.5 border-b border-border last:border-0 hover:bg-white/[0.02] transition-colors items-center"
        >
          <AppCheckbox :model-value="selectedItems.has(item.id)" @update:model-value="toggleSelection(item.id)" />

          <span class="text-[0.82rem] text-text-muted font-mono hidden sm:block">{{ item.id }}</span>

          <span class="text-[0.82rem] text-text-secondary">
            {{ item.clientName }}
          </span>

          <span class="text-[0.82rem] text-text-primary font-medium truncate">{{ item.description }}</span>

          <span class="text-[0.82rem] text-text-secondary">-</span>

          <span class="text-[0.82rem] text-text-secondary">{{ item.currency }} {{ item.amount.toFixed(2) }}</span>

          <span class="text-[0.82rem] text-text-secondary">{{ item.type }}</span>

          <span class="text-[0.82rem]">
            <span
              :class="item.isInvoiced ? 'bg-status-green/15 text-status-green' : 'bg-status-red/15 text-status-red'"
              class="px-2 py-1 rounded-full text-[0.72rem] font-medium inline-block"
            >
              {{ item.isInvoiced ? 'Yes' : 'No' }}
            </span>
          </span>
        </div>
      </template>

      <!-- Empty state -->
      <div v-else class="px-5 py-8 text-center text-text-secondary">
        <p class="text-[0.82rem]">No items found.</p>
      </div>
    </div>

    <!-- Pagination Navigation -->
    <div v-if="filteredItems.length > 0" class="flex items-center justify-between mt-4">
      <button
        @click="currentPage = currentPage - 1"
        :disabled="!canGoPrevious"
        class="px-3 py-2 text-[0.82rem] font-semibold text-text-primary hover:text-primary-500 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
      >
        <svg class="w-4 h-4 inline mr-1" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <path d="M15 18l-6-6 6-6"></path>
        </svg>
        Previous Page
      </button>
      <button
        @click="currentPage = currentPage + 1"
        :disabled="!canGoNext"
        class="px-3 py-2 text-[0.82rem] font-semibold text-text-primary hover:text-primary-500 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
      >
        Next Page
        <svg class="w-4 h-4 inline ml-1" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <path d="M9 18l6-6-6-6"></path>
        </svg>
      </button>
    </div>

    <!-- Bulk Actions -->
    <div v-if="selectedItems.size > 0" class="mt-4 flex items-center gap-3 p-4 bg-surface-card border border-border rounded-2xl">
      <span class="text-[0.82rem] text-text-secondary">With Selected:</span>
      <button
        @click="deleteSelected"
        class="px-4 py-2 text-[0.82rem] font-semibold text-white bg-status-red hover:bg-status-red/90 rounded-[9px] transition-colors"
      >
        Delete
      </button>
    </div>

  </div>
</template>
