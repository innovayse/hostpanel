<script setup lang="ts">
import { onMounted, ref, computed } from 'vue'
import { useRouter } from 'vue-router'
import { useBillingStore } from '../stores/billingStore'
import AppSelect from '../../../components/AppSelect.vue'
import AppClientSelect from '../../../components/AppClientSelect.vue'
import AppSpinner from '../../../components/AppSpinner.vue'
import AppCheckbox from '../../../components/AppCheckbox.vue'
import DateRangePicker from '../../../components/DateRangePicker.vue'

const store = useBillingStore()
const router = useRouter()
const page = ref(1)
const pageSize = ref(25)
const isFilterOpen = ref(false)
const selectedInvoices = ref<Set<number>>(new Set())
const clients = ref<{ id: number; name: string; email?: string; status?: string }[]>([])

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

const totalPages = computed(() => Math.max(1, Math.ceil(store.totalCount / pageSize.value)))

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
  store.fetchAll(1, pageSize.value)
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
  store.fetchAll(1, pageSize.value)
}

function goToPage(p: number): void {
  if (p < 1 || p > totalPages.value) return
  page.value = p
  store.fetchAll(p, pageSize.value)
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
  store.fetchAll(1, pageSize.value)
  loadClients()
})
</script>

<template>
  <div class="p-4 sm:p-6 lg:p-8 w-full">

    <!-- Header -->
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
    </div>

    <!-- Loading -->
    <div v-if="store.loading && store.invoices.length === 0" class="flex items-center gap-3 text-text-secondary text-sm">
      <span class="w-4 h-4 rounded-full border-2 border-primary-500/20 border-t-primary-500 animate-spin" />
      Loading invoices…
    </div>

    <!-- Error -->
    <div v-else-if="store.error" class="text-sm text-status-red bg-status-red/8 border border-status-red/20 rounded-xl p-4">
      {{ store.error }}
    </div>

    <!-- Record Count & Pagination Info -->
    <div v-if="store.invoices.length > 0" class="flex items-center justify-between mb-4 text-[0.82rem] text-text-secondary">
      <div>
        {{ store.totalCount }} Records Found, Showing {{ (page - 1) * pageSize + 1 }} to {{ Math.min(page * pageSize, store.totalCount) }}
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
          <AppCheckbox :model-value="selectedInvoices.size === store.invoices.length && store.invoices.length > 0" @update:model-value="toggleSelectAll" />
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
            <AppCheckbox :model-value="selectedInvoices.has(invoice.id)" @update:model-value="toggleInvoiceSelection(invoice.id)" />
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
            >
              {{ invoice.status }}
            </span>
          </span>

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

  </div>
</template>
