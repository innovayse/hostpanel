<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import ReportPage from '../components/ReportPage.vue'
import AppSelect from '../../../components/AppSelect.vue'
import AppCheckbox from '../../../components/AppCheckbox.vue'
import DateRangePicker from '../../../components/DateRangePicker.vue'
import { useApi } from '../../../composables/useApi'

const { request } = useApi()
const loading = ref(false)
const error = ref<string | null>(null)

interface TxRow {
  id: number; clientId: number; clientName: string; currency: string; paymentMethod: string
  date: string; description: string; invoiceId: number | null; transactionId: string
  amountIn: number; fees: number; amountOut: number
}
const rows = ref<TxRow[]>([])
const totalCount = ref(0)

// Filters
const dateRange = ref<[string, string] | null>(null)
const paymentMethod = ref('')
const page = ref(1); const pageSize = 50

const pmOptions = [
  { value: '', label: 'All' },
  { value: 'Stripe', label: 'Stripe' },
  { value: 'PayPal', label: 'PayPal' },
  { value: 'Bank Transfer', label: 'Bank Transfer' },
]

// Dynamic columns
const allColumns = [
  { key: 'id', label: 'ID' },
  { key: 'clientId', label: 'User ID' },
  { key: 'clientName', label: 'Client Name' },
  { key: 'currency', label: 'Currency' },
  { key: 'paymentMethod', label: 'Payment Method' },
  { key: 'date', label: 'Date' },
  { key: 'description', label: 'Description' },
  { key: 'invoiceId', label: 'Invoice ID' },
  { key: 'transactionId', label: 'Transaction ID' },
  { key: 'amountIn', label: 'Amount In' },
  { key: 'fees', label: 'Fees' },
  { key: 'amountOut', label: 'Amount Out' },
] as const

type ColKey = typeof allColumns[number]['key']
const visibleCols = ref<Set<ColKey>>(new Set(['id', 'clientId', 'clientName', 'currency', 'date', 'description', 'invoiceId', 'transactionId']))

const activeCols = computed(() => allColumns.filter(c => visibleCols.value.has(c.key)))
const totalPages = computed(() => Math.ceil(totalCount.value / pageSize))

function isColVisible(key: ColKey) { return visibleCols.value.has(key) }
function toggleCol(key: ColKey) {
  if (visibleCols.value.has(key)) visibleCols.value.delete(key)
  else visibleCols.value.add(key)
}

function cellValue(row: TxRow, key: ColKey): string {
  const v = row[key]
  if (v == null) return '—'
  if (['amountIn','fees','amountOut'].includes(key)) return `$${(v as number).toFixed(2)}`
  return String(v)
}

function buildParams(): string {
  const p = new URLSearchParams()
  if (dateRange.value) { p.set('dateFrom', dateRange.value[0]); p.set('dateTo', dateRange.value[1]) }
  if (paymentMethod.value) p.set('paymentMethod', paymentMethod.value)
  p.set('page', String(page.value))
  p.set('pageSize', String(pageSize))
  return p.toString()
}

async function load() {
  loading.value = true; error.value = null
  try {
    const data = await request<{ items: TxRow[]; totalCount: number }>(`/reports/transactions?${buildParams()}`)
    rows.value = data.items; totalCount.value = data.totalCount
  } catch { error.value = 'Failed to load report.' } finally { loading.value = false }
}

function applyFilters() { page.value = 1; load() }
function goPage(p: number) { page.value = p; load() }

function exportCsv() {
  const p = new URLSearchParams()
  if (dateRange.value) { p.set('dateFrom', dateRange.value[0]); p.set('dateTo', dateRange.value[1]) }
  if (paymentMethod.value) p.set('paymentMethod', paymentMethod.value)
  window.open(`/api/reports/transactions/export?${p.toString()}`, '_blank')
}

onMounted(load)
</script>

<template>
  <ReportPage title="Transactions" description="This report can be used to generate a custom export of transactions." :loading :error>
    <template #filters>
      <div class="bg-surface-card border border-border rounded-2xl p-4 mb-6 space-y-4">
        <!-- Fields to Include -->
        <div>
          <span class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-2">Fields to Include</span>
          <div class="flex flex-wrap gap-x-4 gap-y-2">
            <label v-for="col in allColumns" :key="col.key" class="flex items-center gap-1.5 text-[0.78rem] text-text-secondary cursor-pointer select-none">
              <AppCheckbox :modelValue="isColVisible(col.key)" @update:modelValue="toggleCol(col.key)" />
              {{ col.label }}
            </label>
          </div>
        </div>

        <div class="grid grid-cols-2 md:grid-cols-3 gap-4">
          <div>
            <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Payment Method</label>
            <AppSelect v-model="paymentMethod" :options="pmOptions" />
          </div>
          <div>
            <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Date Range</label>
            <DateRangePicker v-model="dateRange" />
          </div>
        </div>

        <div class="flex items-center gap-3">
          <button class="px-4 py-2 gradient-brand text-white text-[0.82rem] font-semibold rounded-[9px] transition-opacity hover:opacity-90" @click="applyFilters">Generate</button>
          <button class="px-4 py-2 bg-white/[0.04] border border-border text-text-secondary text-[0.82rem] font-medium rounded-[10px] hover:bg-white/[0.08] transition-colors" @click="exportCsv">Export CSV</button>
        </div>
      </div>
    </template>

    <div class="text-[0.78rem] text-text-secondary mb-3">{{ totalCount }} transaction(s) found</div>
    <div class="bg-surface-card border border-border rounded-2xl overflow-x-auto">
      <table class="w-full text-[0.82rem]">
        <thead>
          <tr class="border-b border-border bg-white/[0.02]">
            <th v-for="col in activeCols" :key="col.key" class="px-4 py-3 text-left text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted whitespace-nowrap">{{ col.label }}</th>
          </tr>
        </thead>
        <tbody>
          <tr v-if="rows.length === 0"><td :colspan="activeCols.length" class="px-4 py-8 text-center text-text-secondary">No transactions found.</td></tr>
          <tr v-for="row in rows" :key="row.id" class="border-b border-border last:border-0 hover:bg-white/[0.02] transition-colors">
            <td v-for="col in activeCols" :key="col.key" class="px-4 py-3 whitespace-nowrap">
              <span v-if="col.key === 'id'" class="text-text-muted font-mono">#{{ row.id }}</span>
              <span v-else-if="col.key === 'amountIn'" class="text-status-green font-medium">{{ cellValue(row, col.key) }}</span>
              <span v-else-if="col.key === 'amountOut'" class="text-status-red font-medium">{{ cellValue(row, col.key) }}</span>
              <span v-else class="text-text-secondary">{{ cellValue(row, col.key) }}</span>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <div v-if="totalPages > 1" class="flex items-center justify-center gap-2 mt-4">
      <button v-for="p in totalPages" :key="p" class="px-3 py-1 rounded-lg text-[0.78rem] transition-colors" :class="p === page ? 'bg-primary-500 text-white' : 'bg-white/[0.04] text-text-secondary hover:bg-white/[0.08]'" @click="goPage(p)">{{ p }}</button>
    </div>

    <div class="text-[0.68rem] text-text-muted mt-4 text-right">Report Generated at {{ new Date().toLocaleString() }}</div>
  </ReportPage>
</template>
