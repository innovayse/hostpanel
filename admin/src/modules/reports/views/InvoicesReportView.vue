<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import ReportPage from '../components/ReportPage.vue'
import ReportTimestamp from '../components/ReportTimestamp.vue'
import FilterCard from '../../../components/FilterCard.vue'
import AdvancedFilters, { type FilterRow, type FieldOption } from '../../../components/AdvancedFilters.vue'
import FieldSelector from '../../../components/FieldSelector.vue'
import DateRangePicker from '../../../components/DateRangePicker.vue'
import { useApi } from '../../../composables/useApi'

const { request } = useApi()
const loading = ref(false)
const error = ref<string | null>(null)

interface InvoiceRow {
  id: number; clientId: number; clientName: string; invoiceNumber: string | null
  createdDate: string; dueDate: string; datePaid: string | null; dateRefunded: string | null
  dateCancelled: string | null; subTotal: number; credit: number; tax: number; tax2: number
  taxRate: number; taxRate2: number; total: number; status: string; paymentMethod: string | null; notes: string | null
}
const rows = ref<InvoiceRow[]>([])
const totalCount = ref(0)

// Advanced filters
const activeFilters = ref<FilterRow[]>([])
const filterFields: FieldOption[] = [
  { value: 'id', label: 'ID' },
  { value: 'clientId', label: 'User ID' },
  { value: 'clientName', label: 'Client Name' },
  { value: 'invoiceNumber', label: 'Invoice Number' },
  { value: 'createdDate', label: 'Creation Date' },
  { value: 'dueDate', label: 'Due Date' },
  { value: 'datePaid', label: 'Date Paid' },
  { value: 'dateRefunded', label: 'Date Refunded' },
  { value: 'dateCancelled', label: 'Date Cancelled' },
  { value: 'subTotal', label: 'Subtotal' },
  { value: 'credit', label: 'Credit' },
  { value: 'tax', label: 'Tax' },
  { value: 'tax2', label: 'Tax2' },
  { value: 'total', label: 'Total' },
  { value: 'taxRate', label: 'Tax Rate' },
  { value: 'taxRate2', label: 'Tax Rate 2' },
  { value: 'status', label: 'Status' },
  { value: 'paymentMethod', label: 'Payment Method' },
  { value: 'notes', label: 'Notes' },
]

// Date range filters
const createdRange = ref<[string, string] | null>(null)
const dueRange = ref<[string, string] | null>(null)
const paidRange = ref<[string, string] | null>(null)
const refundedRange = ref<[string, string] | null>(null)
const cancelledRange = ref<[string, string] | null>(null)
const page = ref(1); const pageSize = 50

// Dynamic column toggle
const allColumns = [
  { key: 'id', label: 'ID' },
  { key: 'clientId', label: 'User ID' },
  { key: 'clientName', label: 'Client Name' },
  { key: 'invoiceNumber', label: 'Invoice Number' },
  { key: 'createdDate', label: 'Creation Date' },
  { key: 'dueDate', label: 'Due Date' },
  { key: 'datePaid', label: 'Date Paid' },
  { key: 'dateRefunded', label: 'Date Refunded' },
  { key: 'dateCancelled', label: 'Date Cancelled' },
  { key: 'subTotal', label: 'Subtotal' },
  { key: 'credit', label: 'Credit' },
  { key: 'tax', label: 'Tax' },
  { key: 'tax2', label: 'Tax2' },
  { key: 'total', label: 'Total' },
  { key: 'taxRate', label: 'Tax Rate' },
  { key: 'taxRate2', label: 'Tax Rate 2' },
  { key: 'status', label: 'Status' },
  { key: 'paymentMethod', label: 'Payment Method' },
  { key: 'notes', label: 'Notes' },
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

function cellValue(row: InvoiceRow, key: ColKey): string {
  const v = row[key]
  if (v == null) return '—'
  if (typeof v === 'number') return ['subTotal','credit','tax','tax2','total'].includes(key) ? `$${v.toFixed(2)}` : ['taxRate','taxRate2'].includes(key) ? `${v}%` : String(v)
  return String(v)
}

const statusBg = (s: string) => ({
  Paid: 'bg-status-green/10 text-status-green',
  Unpaid: 'bg-status-yellow/10 text-status-yellow',
  Overdue: 'bg-status-red/10 text-status-red',
  Cancelled: 'bg-gray-500/10 text-gray-400',
  Refunded: 'bg-purple-500/10 text-purple-400',
  Draft: 'bg-blue-500/10 text-blue-400',
}[s] ?? 'text-text-secondary')

function buildParams(): string {
  const p = new URLSearchParams()
  if (createdRange.value) { p.set('createdFrom', createdRange.value[0]); p.set('createdTo', createdRange.value[1]) }
  if (dueRange.value) { p.set('dueFrom', dueRange.value[0]); p.set('dueTo', dueRange.value[1]) }
  if (paidRange.value) { p.set('paidFrom', paidRange.value[0]); p.set('paidTo', paidRange.value[1]) }
  const statusFilter = activeFilters.value.find(f => f.field === 'status')
  if (statusFilter) p.set('status', statusFilter.value)
  p.set('page', String(page.value))
  p.set('pageSize', String(pageSize))
  return p.toString()
}

function applyClientFilters(items: InvoiceRow[]): InvoiceRow[] {
  return items.filter(row => {
    for (const f of activeFilters.value) {
      if (f.field === 'status') continue
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
    const data = await request<{ items: InvoiceRow[]; totalCount: number }>(`/reports/invoices?${buildParams()}`)
    rows.value = applyClientFilters(data.items); totalCount.value = data.totalCount
  } catch { error.value = 'Failed to load report.' } finally { loading.value = false }
}

function applyFilters() { page.value = 1; load() }
function goPage(p: number) { page.value = p; load() }

function exportCsv() {
  const p = new URLSearchParams()
  const statusFilter = activeFilters.value.find(f => f.field === 'status')
  if (statusFilter) p.set('status', statusFilter.value)
  if (createdRange.value) { p.set('createdFrom', createdRange.value[0]); p.set('createdTo', createdRange.value[1]) }
  if (dueRange.value) { p.set('dueFrom', dueRange.value[0]); p.set('dueTo', dueRange.value[1]) }
  if (paidRange.value) { p.set('paidFrom', paidRange.value[0]); p.set('paidTo', paidRange.value[1]) }
  window.open(`/api/reports/invoices/export?${p.toString()}`, '_blank')
}

function printReport() { window.print() }

onMounted(load)
</script>

<template>
  <ReportPage title="Invoices" description="This report can be used to generate a custom export of invoices by applying up to 5 filters." :loading :error>
    <template #filters>
      <FilterCard>
        <!-- Row 1: Field selector + Date ranges responsive grid -->
        <template #fields>
          <div class="flex flex-wrap gap-3 items-end">
            <div class="min-w-[140px]">
              <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1">Fields to Include</label>
              <FieldSelector :fields="allColumns" :selected="visibleCols" @toggle="toggleCol" @select-all="selectAllCols" @clear-all="clearAllCols" />
            </div>
            <div class="min-w-[150px] flex-1">
              <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1">Creation Date</label>
              <DateRangePicker v-model="createdRange" />
            </div>
            <div class="min-w-[150px] flex-1">
              <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1">Due Date</label>
              <DateRangePicker v-model="dueRange" />
            </div>
            <div class="min-w-[150px] flex-1">
              <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1">Date Paid</label>
              <DateRangePicker v-model="paidRange" />
            </div>
            <div class="min-w-[150px] flex-1">
              <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1">Date Refunded</label>
              <DateRangePicker v-model="refundedRange" />
            </div>
            <div class="min-w-[150px] flex-1">
              <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1">Date Cancelled</label>
              <DateRangePicker v-model="cancelledRange" />
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
            <button class="px-4 py-2 bg-white/[0.04] border border-border text-text-secondary text-[0.78rem] font-medium rounded-[9px] hover:bg-white/[0.08] transition-colors" @click="printReport">Print</button>
          </div>
        </template>
      </FilterCard>
    </template>

    <!-- Results -->
    <div class="text-[0.78rem] text-text-secondary mb-3">{{ totalCount }} invoice(s) found</div>
    <div class="bg-surface-card border border-border rounded-2xl overflow-x-auto">
      <table class="w-full text-[0.82rem]">
        <thead>
          <tr class="border-b border-border bg-white/[0.02]">
            <th v-for="col in activeCols" :key="col.key" class="px-4 py-3 text-left text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted whitespace-nowrap">{{ col.label }}</th>
          </tr>
        </thead>
        <tbody>
          <tr v-if="rows.length === 0"><td :colspan="activeCols.length" class="px-4 py-8 text-center text-text-secondary">No invoices found.</td></tr>
          <tr v-for="row in rows" :key="row.id" class="border-b border-border last:border-0 hover:bg-white/[0.02] transition-colors">
            <td v-for="col in activeCols" :key="col.key" class="px-4 py-3 whitespace-nowrap">
              <span v-if="col.key === 'status'" :class="['text-[0.72rem] px-2 py-0.5 rounded-full font-medium', statusBg(row.status)]">{{ row.status }}</span>
              <span v-else-if="col.key === 'id'" class="text-text-muted font-mono">#{{ row.id }}</span>
              <span v-else :class="['total','subTotal'].includes(col.key) ? 'font-medium text-text-primary' : 'text-text-secondary'">{{ cellValue(row, col.key) }}</span>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <!-- Pagination -->
    <div v-if="totalPages > 1" class="flex items-center justify-center gap-2 mt-4">
      <button v-for="p in totalPages" :key="p" class="px-3 py-1 rounded-lg text-[0.78rem] transition-colors" :class="p === page ? 'bg-primary-500 text-white' : 'bg-white/[0.04] text-text-secondary hover:bg-white/[0.08]'" @click="goPage(p)">{{ p }}</button>
    </div>
    <ReportTimestamp />
</ReportPage>
</template>
