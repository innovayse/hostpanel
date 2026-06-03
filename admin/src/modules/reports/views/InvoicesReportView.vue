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

interface InvoiceRow {
  id: number; clientId: number; clientName: string; invoiceNumber: string | null
  createdDate: string; dueDate: string; datePaid: string | null; dateRefunded: string | null
  dateCancelled: string | null; subTotal: number; credit: number; tax: number; tax2: number
  taxRate: number; taxRate2: number; total: number; status: string; paymentMethod: string | null; notes: string | null
}
const rows = ref<InvoiceRow[]>([])
const totalCount = ref(0)

// Filters
const status = ref('')
const createdRange = ref<[string, string] | null>(null)
const dueRange = ref<[string, string] | null>(null)
const paidRange = ref<[string, string] | null>(null)
const refundedRange = ref<[string, string] | null>(null)
const cancelledRange = ref<[string, string] | null>(null)
const page = ref(1); const pageSize = 50

const statusOptions = [
  { value: '', label: 'All' },
  { value: 'Draft', label: 'Draft' },
  { value: 'Unpaid', label: 'Unpaid' },
  { value: 'Paid', label: 'Paid' },
  { value: 'Overdue', label: 'Overdue' },
  { value: 'Cancelled', label: 'Cancelled' },
  { value: 'Refunded', label: 'Refunded' },
]

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
const visibleCols = ref<Set<ColKey>>(new Set(['id', 'clientName', 'status', 'createdDate', 'dueDate', 'datePaid', 'subTotal', 'credit', 'tax', 'total']))

const activeCols = computed(() => allColumns.filter(c => visibleCols.value.has(c.key)))
const totalPages = computed(() => Math.ceil(totalCount.value / pageSize))

function isColVisible(key: ColKey) { return visibleCols.value.has(key) }
function toggleCol(key: ColKey) {
  if (visibleCols.value.has(key)) visibleCols.value.delete(key)
  else visibleCols.value.add(key)
}

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
  if (status.value) p.set('status', status.value)
  if (createdRange.value) { p.set('createdFrom', createdRange.value[0]); p.set('createdTo', createdRange.value[1]) }
  if (dueRange.value) { p.set('dueFrom', dueRange.value[0]); p.set('dueTo', dueRange.value[1]) }
  if (paidRange.value) { p.set('paidFrom', paidRange.value[0]); p.set('paidTo', paidRange.value[1]) }
  p.set('page', String(page.value))
  p.set('pageSize', String(pageSize))
  return p.toString()
}

async function load() {
  loading.value = true; error.value = null
  try {
    const data = await request<{ items: InvoiceRow[]; totalCount: number }>(`/reports/invoices?${buildParams()}`)
    rows.value = data.items; totalCount.value = data.totalCount
  } catch { error.value = 'Failed to load report.' } finally { loading.value = false }
}

function applyFilters() { page.value = 1; load() }
function goPage(p: number) { page.value = p; load() }

function exportCsv() {
  const p = new URLSearchParams()
  if (status.value) p.set('status', status.value)
  if (createdRange.value) { p.set('createdFrom', createdRange.value[0]); p.set('createdTo', createdRange.value[1]) }
  if (dueRange.value) { p.set('dueFrom', dueRange.value[0]); p.set('dueTo', dueRange.value[1]) }
  if (paidRange.value) { p.set('paidFrom', paidRange.value[0]); p.set('paidTo', paidRange.value[1]) }
  window.open(`/api/reports/invoices/export?${p.toString()}`, '_blank')
}

function printReport() { window.print() }

onMounted(load)
</script>

<template>
  <ReportPage title="Invoices" description="This report can be used to generate a custom export of invoices." :loading :error>
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

        <!-- Status -->
        <div class="grid grid-cols-2 md:grid-cols-4 gap-4">
          <div>
            <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Status</label>
            <AppSelect v-model="status" :options="statusOptions" />
          </div>
        </div>

        <!-- Date Range Filters -->
        <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
          <div>
            <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Creation Date Range</label>
            <DateRangePicker v-model="createdRange" />
          </div>
          <div>
            <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Due Date Range</label>
            <DateRangePicker v-model="dueRange" />
          </div>
          <div>
            <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Date Paid Range</label>
            <DateRangePicker v-model="paidRange" />
          </div>
          <div>
            <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Date Refunded Range</label>
            <DateRangePicker v-model="refundedRange" />
          </div>
          <div>
            <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Date Cancelled Range</label>
            <DateRangePicker v-model="cancelledRange" />
          </div>
        </div>

        <!-- Actions -->
        <div class="flex items-center gap-3">
          <button class="px-4 py-2 gradient-brand text-white text-[0.82rem] font-semibold rounded-[9px] transition-opacity hover:opacity-90" @click="applyFilters">Generate</button>
          <button class="px-4 py-2 bg-white/[0.04] border border-border text-text-secondary text-[0.82rem] font-medium rounded-[10px] hover:bg-white/[0.08] transition-colors" @click="exportCsv">Export CSV</button>
          <button class="px-4 py-2 bg-white/[0.04] border border-border text-text-secondary text-[0.82rem] font-medium rounded-[10px] hover:bg-white/[0.08] transition-colors" @click="printReport">Print</button>
        </div>
      </div>
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
  </ReportPage>
</template>
