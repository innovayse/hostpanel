<script setup lang="ts">
import { ref, onMounted } from 'vue'
import ReportPage from '../components/ReportPage.vue'
import ReportTimestamp from '../components/ReportTimestamp.vue'
import AppSelect from '../../../components/AppSelect.vue'
import DateRangePicker from '../../../components/DateRangePicker.vue'
import { useApi } from '../../../composables/useApi'

const { request } = useApi()
const loading = ref(false)
const error = ref<string | null>(null)

const now = new Date()
const dateRange = ref<[string, string]>([
  `${now.getFullYear()}-01-01`,
  `${now.getFullYear()}-12-31`,
])
const selectedCurrency = ref('USD')

const currencyOptions = [
  { value: 'USD', label: 'USD — US Dollar' },
  { value: 'EUR', label: 'EUR — Euro' },
  { value: 'RUB', label: 'RUB — Russian Ruble' },
  { value: 'AMD', label: 'AMD — Armenian Dram' },
]
const rates: Record<string, number> = { USD: 1, EUR: 0.92, RUB: 90.5, AMD: 387 }
const symbols: Record<string, string> = { USD: '$', EUR: '€', RUB: '₽', AMD: '֏' }

interface SalesTaxRow { id: number; clientName: string; invoiceDate: string; datePaid: string | null; subTotal: number; tax: number; credit: number; total: number }
interface ReportData { totalInvoices: number; totalInvoiced: number; taxLevel1Liability: number; taxLevel2Liability: number; rows: SalesTaxRow[] }

const data = ref<ReportData | null>(null)

function fmt(n: number) {
  const v = n * rates[selectedCurrency.value]
  return `${symbols[selectedCurrency.value]}${v.toFixed(2)} ${selectedCurrency.value}`
}

async function load() {
  loading.value = true; error.value = null
  try {
    const params = new URLSearchParams()
    if (dateRange.value) { params.set('from', dateRange.value[0]); params.set('to', dateRange.value[1]) }
    data.value = await request<ReportData>(`/reports/sales-tax?${params}`)
  } catch { error.value = 'Failed to load report.' } finally { loading.value = false }
}

onMounted(load)
</script>

<template>
  <ReportPage title="Sales Tax Liability" description="This report shows sales tax liability for the selected period." :loading :error>
    <template #filters>
      <div class="bg-surface-card border border-border rounded-2xl p-4 mb-6">
        <div class="flex items-end gap-3 flex-wrap">
          <div class="flex-1 min-w-[220px]">
            <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1">Date Range</label>
            <DateRangePicker v-model="dateRange" />
          </div>
          <div class="w-[190px]">
            <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1">Choose Currency</label>
            <AppSelect v-model="selectedCurrency" :options="currencyOptions" />
          </div>
          <button class="px-5 py-2 gradient-brand text-white text-[0.82rem] font-semibold rounded-[9px] transition-opacity hover:opacity-90" @click="load">Generate Report</button>
        </div>
      </div>
    </template>

    <template v-if="data">
      <!-- Summary -->
      <div class="mb-4 text-[0.82rem] text-text-secondary space-y-1">
        <p><span class="text-text-primary font-semibold">{{ data.totalInvoices }}</span> Invoices Found</p>
        <p class="flex flex-wrap gap-4">
          <span>Total Invoiced: <span class="text-text-primary font-medium">{{ fmt(data.totalInvoiced) }}</span></span>
          <span>Tax Level 1 Liability: <span class="text-text-primary font-medium">{{ fmt(data.taxLevel1Liability) }}</span></span>
          <span>Tax Level 2 Liability: <span class="text-text-primary font-medium">{{ fmt(data.taxLevel2Liability) }}</span></span>
        </p>
      </div>

      <!-- Table -->
      <div class="bg-surface-card border border-border rounded-2xl overflow-hidden">
        <table class="w-full text-[0.82rem]">
          <thead>
            <tr class="border-b border-border bg-white/[0.02]">
              <th class="px-4 py-3 text-left   text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Invoice ID</th>
              <th class="px-4 py-3 text-left   text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Client Name</th>
              <th class="px-4 py-3 text-center text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Invoice Date</th>
              <th class="px-4 py-3 text-center text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Date Paid</th>
              <th class="px-4 py-3 text-right  text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Sub Total</th>
              <th class="px-4 py-3 text-right  text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Tax</th>
              <th class="px-4 py-3 text-right  text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Credit</th>
              <th class="px-4 py-3 text-right  text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Total</th>
            </tr>
          </thead>
          <tbody>
            <tr v-if="data.rows.length === 0">
              <td colspan="8" class="px-4 py-8 text-center text-text-secondary">No invoices found for this period.</td>
            </tr>
            <tr v-for="row in data.rows" :key="row.id"
              class="border-b border-border last:border-0 hover:bg-white/[0.02] transition-colors">
              <td class="px-4 py-2.5 text-text-muted font-mono">{{ row.id }}</td>
              <td class="px-4 py-2.5 text-text-primary">{{ row.clientName }}</td>
              <td class="px-4 py-2.5 text-center text-text-secondary font-mono">{{ row.invoiceDate }}</td>
              <td class="px-4 py-2.5 text-center font-mono" :class="row.datePaid ? 'text-text-secondary' : 'text-text-muted'">{{ row.datePaid ?? '—' }}</td>
              <td class="px-4 py-2.5 text-right text-text-primary">{{ fmt(row.subTotal) }}</td>
              <td class="px-4 py-2.5 text-right" :class="row.tax > 0 ? 'text-status-yellow font-medium' : 'text-text-muted'">{{ fmt(row.tax) }}</td>
              <td class="px-4 py-2.5 text-right" :class="row.credit > 0 ? 'text-status-red' : 'text-text-muted'">{{ fmt(row.credit) }}</td>
              <td class="px-4 py-2.5 text-right font-semibold text-status-green">{{ fmt(row.total) }}</td>
            </tr>
          </tbody>
          <tfoot v-if="data.rows.length > 0">
            <tr class="border-t-2 border-border bg-white/[0.03]">
              <td colspan="4" class="px-4 py-3 text-[0.78rem] font-bold text-text-primary">Total</td>
              <td class="px-4 py-3 text-right text-[0.88rem] font-bold text-text-primary">{{ fmt(data.rows.reduce((s, r) => s + r.subTotal, 0)) }}</td>
              <td class="px-4 py-3 text-right text-[0.88rem] font-bold text-status-yellow">{{ fmt(data.taxLevel1Liability) }}</td>
              <td class="px-4 py-3 text-right text-[0.88rem] font-bold text-status-red">{{ fmt(data.rows.reduce((s, r) => s + r.credit, 0)) }}</td>
              <td class="px-4 py-3 text-right text-[0.88rem] font-bold text-status-green">{{ fmt(data.totalInvoiced) }}</td>
            </tr>
          </tfoot>
        </table>
      </div>

      <!-- Footer note -->
      <p class="mt-4 text-[0.75rem] text-text-muted italic">
        This report excludes invoices that affect a client's credit balance since this income will be counted and reported when it is applied to invoices for products/services.
      </p>
    </template>
    <ReportTimestamp />
</ReportPage>
</template>
