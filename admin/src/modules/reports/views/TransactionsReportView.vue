<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import ReportPage from '../components/ReportPage.vue'
import FilterCard from '../../../components/FilterCard.vue'
import AdvancedFilters, { type FilterRow, type FieldOption } from '../../../components/AdvancedFilters.vue'
import FieldSelector from '../../../components/FieldSelector.vue'
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

// Advanced filters
const activeFilters = ref<FilterRow[]>([])
const filterFields: FieldOption[] = [
  { value: 'id', label: 'ID' },
  { value: 'clientId', label: 'User ID' },
  { value: 'clientName', label: 'Client Name' },
  { value: 'currency', label: 'Currency' },
  { value: 'paymentMethod', label: 'Payment Method' },
  { value: 'date', label: 'Date' },
  { value: 'description', label: 'Description' },
  { value: 'invoiceId', label: 'Invoice ID' },
  { value: 'transactionId', label: 'Transaction ID' },
  { value: 'amountIn', label: 'Amount In' },
  { value: 'fees', label: 'Fees' },
  { value: 'amountOut', label: 'Amount Out' },
]

const dateRange = ref<[string, string] | null>(null)
const page = ref(1); const pageSize = 50

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
const visibleCols = ref<Set<ColKey>>(new Set())

const activeCols = computed(() => allColumns.filter(c => visibleCols.value.has(c.key)))
const totalPages = computed(() => Math.ceil(totalCount.value / pageSize))

function toggleCol(key: string) {
  const k = key as ColKey
  if (visibleCols.value.has(k)) visibleCols.value.delete(k)
  else visibleCols.value.add(k)
}
function selectAllCols() { allColumns.forEach(c => visibleCols.value.add(c.key)) }
function clearAllCols() { visibleCols.value.clear() }

function cellValue(row: TxRow, key: ColKey): string {
  const v = row[key]
  if (v == null) return '—'
  if (['amountIn','fees','amountOut'].includes(key)) return `$${(v as number).toFixed(2)}`
  return String(v)
}

function buildParams(): string {
  const p = new URLSearchParams()
  if (dateRange.value) { p.set('dateFrom', dateRange.value[0]); p.set('dateTo', dateRange.value[1]) }
  const pmFilter = activeFilters.value.find(f => f.field === 'paymentMethod')
  if (pmFilter) p.set('paymentMethod', pmFilter.value)
  p.set('page', String(page.value))
  p.set('pageSize', String(pageSize))
  return p.toString()
}

function applyClientFilters(items: TxRow[]): TxRow[] {
  return items.filter(row => {
    for (const f of activeFilters.value) {
      if (f.field === 'paymentMethod') continue
      const rowVal = String(row[f.field as ColKey] ?? '').toLowerCase()
      const search = f.value.toLowerCase()
      if (f.condition === 'exact' && rowVal !== search) return false
      if (f.condition === 'contains' && !rowVal.includes(search)) return false
    }
    return true
  })
}

async function load() {
  loading.value = true; error.value = null
  try {
    const data = await request<{ items: TxRow[]; totalCount: number }>(`/reports/transactions?${buildParams()}`)
    rows.value = applyClientFilters(data.items); totalCount.value = data.totalCount
  } catch { error.value = 'Failed to load report.' } finally { loading.value = false }
}

function applyFilters() { page.value = 1; load() }
function goPage(p: number) { page.value = p; load() }

function exportCsv() {
  const p = new URLSearchParams()
  if (dateRange.value) { p.set('dateFrom', dateRange.value[0]); p.set('dateTo', dateRange.value[1]) }
  const pmFilter = activeFilters.value.find(f => f.field === 'paymentMethod')
  if (pmFilter) p.set('paymentMethod', pmFilter.value)
  window.open(`/api/reports/transactions/export?${p.toString()}`, '_blank')
}

onMounted(load)
</script>

<template>
  <ReportPage title="Transactions" description="This report can be used to generate a custom export of transactions by applying up to 5 filters." :loading :error>
    <template #filters>
      <FilterCard>
        <!-- Row 1: Field selector + Date range 50/50 -->
        <template #fields>
          <div class="grid grid-cols-2 gap-3 items-end">
            <div>
              <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1">Fields</label>
              <FieldSelector :fields="allColumns" :selected="visibleCols" @toggle="toggleCol" @select-all="selectAllCols" @clear-all="clearAllCols" />
            </div>
            <div>
              <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1">Date Range</label>
              <DateRangePicker v-model="dateRange" />
            </div>
          </div>
        </template>

        <!-- Row 2: Advanced filters -->
        <AdvancedFilters :fields="filterFields" @update:filters="activeFilters = $event" />

        <!-- Row 3: Action buttons centered -->
        <template #actions>
          <div class="flex items-center justify-center gap-3 pt-3 border-t border-border/50">
            <button class="px-5 py-2 gradient-brand text-white text-[0.82rem] font-semibold rounded-[9px] transition-opacity hover:opacity-90" @click="applyFilters">Generate</button>
            <button class="px-4 py-2 bg-white/[0.04] border border-border text-text-secondary text-[0.78rem] font-medium rounded-[9px] hover:bg-white/[0.08] transition-colors" @click="exportCsv">Export CSV</button>
          </div>
        </template>
      </FilterCard>
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
