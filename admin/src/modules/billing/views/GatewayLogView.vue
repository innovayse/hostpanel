<script setup lang="ts">
import { ref, computed } from 'vue'
import AppSelect from '../../../components/AppSelect.vue'
import DateRangePicker from '../../../components/DateRangePicker.vue'

const page = ref(1)
const pageSize = 20
const showFilters = ref(false)
const dateRange = ref<[string, string] | null>(null)
const debugData = ref('')
const gateway = ref('Any')
const result = ref('Any')

const gatewayOptions = [
  { value: 'Any', label: 'Any' },
  { value: 'Stripe', label: 'Stripe' },
  { value: 'PayPal', label: 'PayPal' },
]

const resultOptions = [
  { value: 'Any', label: 'Any' },
  { value: 'Refund Error', label: 'Refund Error' },
  { value: 'Remote Storage Success', label: 'Remote Storage Success' },
]

// Mock gateway logs
const logs = ref([
  {
    id: 1,
    dateTime: '2026-05-26T13:10:45Z',
    gateway: 'Stripe',
    description: 'Payment processing for invoice #101',
    result: 'Refund Error',
    details: {
      request: {
        type: 'refund',
        amount: 149.99,
        currency: 'USD',
        customerId: 'cus_U4L9TR92WG8I8'
      },
      response: `Refund Error
Status => error
error => No API key provided. (HINT: set your API key using "Stripe::setApiKey(<API-KEY>)". You can generate API keys from the Stripe web interface. See https://stripe.com/api for details, or email support@stripe.com if you have any questions.`
    }
  },
  {
    id: 2,
    dateTime: '2026-05-26T13:03:22Z',
    gateway: 'Stripe',
    description: 'Customer object updated',
    result: 'Remote Storage Success',
    details: {
      request: {
        type: 'customer_update',
        userId: 6,
        object: 'customer',
        address: 'updated'
      },
      response: `UserID => 6
Status => success
Customer ID => cus_U4L9Tl9i2WG8I8
id => cus_U4L9Tl9i2WG8I8
object => customer
address =>
    city =>
    country => AM
    line1 =>
    line2 =>
    postal_code =>
    state =>
balance => 0
created => 1772381044
currency =>
customer_account =>
default_source =>
delinquent =>
description => Customer for Touch Estate (touchestate01@gmail.com)
discount =>
email => touchestate01@gmail.com
invoice_prefix => ETXLMCI0
invoice_settings =>
    custom_fields =>
    default_payment_method =>
    footer =>
    rendering_options =>
livemode =>
metadata =>
    clientId => 6
    email => touchestate01@gmail.com
    fullName => Touch Estate
name => Touch Estate
next_invoice_sequence => 1
phone =>
preferred_locales =>
shipping =>
tax_exempt => none
test_clock =>`
    }
  },
  {
    id: 3,
    dateTime: '2026-05-26T13:02:15Z',
    gateway: 'Stripe',
    description: 'Charge creation with phone and locales',
    result: 'Remote Storage Success',
    details: {
      request: {
        phone: '555-1234',
        preferred_locales: ['en', 'fr'],
        shipping: 'added',
        test_clock: 'enabled'
      },
      response: `Remote Storage Success
Status => success
chargeId => ch_1234567890
amount => 299.00
currency => USD
created => 2026-05-26T13:02:15Z`
    }
  },
  {
    id: 4,
    dateTime: '2026-05-26T20:04:33Z',
    gateway: 'PayPal',
    description: 'Transaction capture initiated',
    result: 'Remote Storage Success',
    details: {
      request: {
        transactionId: 'txn_9876543210',
        amount: 500.00,
        action: 'capture'
      },
      response: `Remote Storage Success
Status => success
Transaction ID => txn_9876543210
Captured Amount => 500.00
Timestamp => 2026-05-26T20:04:33Z`
    }
  },
])

const filteredLogs = computed(() => {
  return logs.value.filter(log => {
    if (dateRange.value) {
      const [startStr, endStr] = dateRange.value
      const logDate = new Date(log.dateTime).toISOString().split('T')[0]
      if (logDate < startStr || logDate > endStr) return false
    }

    if (gateway.value !== 'Any' && log.gateway !== gateway.value) return false

    if (result.value !== 'Any' && log.result !== result.value) return false

    if (debugData.value && !JSON.stringify(log.details).toLowerCase().includes(debugData.value.toLowerCase())) return false

    return true
  })
})

const totalCount = computed(() => filteredLogs.value.length)
const totalPages = computed(() => Math.max(1, Math.ceil(totalCount.value / pageSize)))
const canGoPrevious = computed(() => page.value > 1)
const canGoNext = computed(() => page.value < totalPages.value)

const paginatedLogs = computed(() => {
  const start = (page.value - 1) * pageSize
  const end = start + pageSize
  return filteredLogs.value.slice(start, end)
})

function formatJson(obj: any): string {
  return JSON.stringify(obj, null, 2)
}

function goToPrevious(): void {
  if (canGoPrevious.value) page.value--
}

function goToNext(): void {
  if (canGoNext.value) page.value++
}

function toggleFilters(): void {
  showFilters.value = !showFilters.value
}

function clearFilters(): void {
  dateRange.value = null
  debugData.value = ''
  gateway.value = 'Any'
  result.value = 'Any'
  page.value = 1
  showFilters.value = false
}

function applyFilters(): void {
  page.value = 1
  showFilters.value = false
}
</script>

<template>
  <div class="p-4 sm:p-6 lg:p-8 w-full">

    <!-- Header -->
    <div class="mb-5">
      <h1 class="font-display font-bold text-[1.25rem] text-text-primary leading-none">Gateway Transaction Log</h1>
      <p class="text-[0.78rem] text-text-secondary mt-1">View payment gateway transaction logs</p>
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

    <!-- Filter Section -->
    <div v-if="showFilters" class="bg-white/[0.02] border border-border rounded-xl p-4 mb-4">
      <div class="flex items-start justify-between gap-6 mb-4">
        <!-- Left side: Date Range and Debug Data -->
        <div class="flex-1 space-y-3">
          <div class="flex items-center gap-4">
            <label class="text-[0.82rem] font-semibold text-text-muted w-32">Date Range</label>
            <div class="flex-1">
              <DateRangePicker
                v-model="dateRange"
                placeholder="Select date range..."
              />
            </div>
          </div>
          <div class="flex items-center gap-4">
            <label class="text-[0.82rem] font-semibold text-text-muted w-32">Debug Data</label>
            <input
              v-model="debugData"
              type="text"
              placeholder="Search in debug data..."
              class="flex-1 px-3 py-2 text-[0.82rem] text-text-primary bg-white/[0.05] border border-border rounded-[6px] focus:outline-none focus:border-primary-500/40 transition-colors"
            />
          </div>
        </div>

        <!-- Right side: Gateway and Result -->
        <div class="flex-1 space-y-3">
          <div class="flex items-center gap-4">
            <label class="text-[0.82rem] font-semibold text-text-muted w-20">Gateway</label>
            <AppSelect
              v-model="gateway"
              :options="gatewayOptions"
              class="flex-1"
            />
          </div>
          <div class="flex items-center gap-4">
            <label class="text-[0.82rem] font-semibold text-text-muted w-20">Result</label>
            <AppSelect
              v-model="result"
              :options="resultOptions"
              class="flex-1"
            />
          </div>
        </div>
      </div>

      <!-- Filter Buttons -->
      <div class="flex items-center justify-center gap-2 mt-4">
        <button
          @click="applyFilters"
          class="gradient-brand flex items-center gap-1.5 px-4 py-2 text-[0.82rem] font-semibold text-white rounded-[9px] transition-opacity hover:opacity-90"
        >
          <svg class="w-3.5 h-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round">
            <circle cx="11" cy="11" r="8"/><line x1="21" y1="21" x2="16.65" y2="16.65"/>
          </svg>
          Search
        </button>
        <button
          @click="clearFilters"
          class="px-3 py-2 text-[0.78rem] text-text-muted hover:text-text-primary transition-colors"
        >
          Clear
        </button>
      </div>
    </div>

    <!-- Record Count -->
    <div class="mb-4 text-[0.82rem] text-text-muted">
      {{ totalCount }} Records Found<span v-if="totalCount > 0">, Page {{ page }} of {{ totalPages }}</span>
    </div>

    <!-- Table -->
    <div class="bg-surface-card border border-border rounded-2xl overflow-hidden">

      <!-- Header row -->
      <div class="hidden sm:grid grid-cols-[1.2fr_1fr_3fr_1.2fr] gap-0 px-5 py-3 border-b border-border bg-white/[0.02] text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">
        <span>Date</span>
        <span>Gateway</span>
        <span>Debug Data</span>
        <span>Result</span>
      </div>

      <!-- Rows or Empty state -->
      <template v-if="paginatedLogs.length > 0">
        <div v-for="log in paginatedLogs" :key="log.id" class="hidden sm:grid grid-cols-[1.2fr_1fr_3fr_1.2fr] gap-0 border-b border-border last:border-0 hover:bg-white/[0.02] transition-colors items-stretch">
          <!-- Date -->
          <div class="px-5 py-4 flex items-center border-r border-border/30">
            <span class="text-[0.82rem] text-text-muted">{{ new Date(log.dateTime).toLocaleString() }}</span>
          </div>

          <!-- Gateway -->
          <div class="px-5 py-4 flex items-center border-r border-border/30">
            <span class="text-[0.82rem] text-text-secondary font-medium">{{ log.gateway }}</span>
          </div>

          <!-- Debug Data -->
          <div class="px-5 py-4 border-r border-border/30 flex items-stretch">
            <div class="text-[0.75rem] font-mono text-text-secondary whitespace-pre-wrap break-words bg-black/40 p-3 rounded max-h-40 overflow-y-auto border border-border/30 w-full">{{ typeof log.details.response === 'string' ? log.details.response : formatJson(log.details) }}</div>
          </div>

          <!-- Result -->
          <div class="px-5 py-4 flex items-center">
            <span
              :class="{
                'bg-status-red/15 text-status-red': log.result.includes('Error'),
                'bg-status-green/15 text-status-green': log.result.includes('Success'),
                'bg-status-yellow/15 text-status-yellow': log.result.includes('Pending')
              }"
              class="px-2 py-1 rounded-full text-[0.72rem] font-medium inline-block"
            >
              {{ log.result }}
            </span>
          </div>
        </div>
      </template>

      <!-- Empty state -->
      <div v-else class="px-5 py-8 text-center text-text-secondary">
        <p class="text-[0.82rem]">No Records Found</p>
      </div>
    </div>

    <!-- Pagination -->
    <div v-if="totalCount > 0" class="flex items-center justify-between mt-4">
      <button
        @click="goToPrevious"
        :disabled="!canGoPrevious"
        class="px-3 py-2 text-[0.82rem] font-semibold text-text-primary hover:text-primary-500 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
      >
        « Previous Page
      </button>
      <button
        @click="goToNext"
        :disabled="!canGoNext"
        class="px-3 py-2 text-[0.82rem] font-semibold text-text-primary hover:text-primary-500 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
      >
        Next Page »
      </button>
    </div>

  </div>
</template>
