<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { Line } from 'vue-chartjs'
import { Chart as ChartJS, CategoryScale, LinearScale, PointElement, LineElement, Title, Tooltip, Legend, Filler } from 'chart.js'
import ReportPage from '../components/ReportPage.vue'
import ReportTimestamp from '../components/ReportTimestamp.vue'
import AppSelect from '../../../components/AppSelect.vue'
import { useApi } from '../../../composables/useApi'

ChartJS.register(CategoryScale, LinearScale, PointElement, LineElement, Title, Tooltip, Legend, Filler)

const { request } = useApi()
const loading = ref(false)
const error = ref<string | null>(null)

const now = new Date()
const selectedYear = ref(now.getFullYear())
const selectedMonth = ref(now.getMonth() + 1)
const selectedCurrency = ref('USD')

const monthNames = ['January','February','March','April','May','June','July','August','September','October','November','December']
const monthOptions = monthNames.map((name, i) => ({ value: i + 1, label: name }))
const yearOptions = Array.from({ length: 5 }, (_, i) => now.getFullYear() - 2 + i).map(y => ({ value: y, label: String(y) }))
const currencyOptions = [
  { value: 'USD', label: 'USD — US Dollar' },
  { value: 'EUR', label: 'EUR — Euro' },
  { value: 'RUB', label: 'RUB — Russian Ruble' },
  { value: 'AMD', label: 'AMD — Armenian Dram' },
]
const rates: Record<string, number> = { USD: 1, EUR: 0.92, RUB: 90.5, AMD: 387 }
const symbols: Record<string, string> = { USD: '$', EUR: '€', RUB: '₽', AMD: '֏' }

interface DailyRow { date: string; amountIn: number; fees: number; amountOut: number; balance: number }
interface ReportData { month: number; year: number; rows: DailyRow[]; totalAmountIn: number; totalFees: number; totalAmountOut: number }

const data = ref<ReportData | null>(null)

const tableRows = computed(() => data.value?.rows ?? [])

const reportTitle = computed(() => data.value
  ? `Monthly Transactions Report for ${monthNames[data.value.month - 1]} ${data.value.year}`
  : 'Monthly Transactions')

function fmt(n: number) {
  const v = n * rates[selectedCurrency.value]
  return `${symbols[selectedCurrency.value]}${v.toFixed(2)}`
}

const chartData = computed(() => {
  const rows = data.value?.rows ?? []
  const r = rates[selectedCurrency.value]
  return {
    labels: rows.map(r => r.date.slice(5)), // MM-DD
    datasets: [
      { label: 'Amount In',  data: rows.map(x => x.amountIn  * r), borderColor: '#10b981', backgroundColor: 'rgba(16,185,129,0.08)', fill: true, tension: 0.4, pointRadius: 3 },
      { label: 'Fees',       data: rows.map(x => x.fees      * r), borderColor: '#f59e0b', backgroundColor: 'rgba(245,158,11,0.08)',  fill: true, tension: 0.4, pointRadius: 3 },
      { label: 'Amount Out', data: rows.map(x => x.amountOut * r), borderColor: '#ef4444', backgroundColor: 'rgba(239,68,68,0.08)',   fill: true, tension: 0.4, pointRadius: 3 },
    ],
  }
})

const chartOptions = {
  responsive: true, maintainAspectRatio: false,
  plugins: {
    legend: { position: 'right' as const, labels: { color: '#94a3b8', boxWidth: 12, padding: 16 } },
    tooltip: { mode: 'index' as const, intersect: false },
  },
  scales: {
    x: { grid: { color: 'rgba(255,255,255,0.05)' }, ticks: { color: '#64748b' } },
    y: { grid: { color: 'rgba(255,255,255,0.05)' }, ticks: { color: '#64748b' } },
  },
}

async function load() {
  loading.value = true; error.value = null
  try {
    data.value = await request<ReportData>(`/reports/daily-transactions?year=${selectedYear.value}&month=${selectedMonth.value}`)
  } catch { error.value = 'Failed to load report.' } finally { loading.value = false }
}

onMounted(load)
</script>

<template>
  <ReportPage :title="reportTitle" description="This report provides a summary of daily pay transactions for a given month. The Amount Out figure includes both expenditure transactions and refunds." :loading :error>
    <template #filters>
      <div class="bg-surface-card border border-border rounded-2xl p-4 mb-6">
        <div class="flex items-end gap-3 flex-wrap">
          <div class="w-[160px]">
            <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1">Month</label>
            <AppSelect v-model="selectedMonth" :options="monthOptions" />
          </div>
          <div class="w-[110px]">
            <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1">Year</label>
            <AppSelect v-model="selectedYear" :options="yearOptions" />
          </div>
          <div class="w-[190px]">
            <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1">Choose Currency</label>
            <AppSelect v-model="selectedCurrency" :options="currencyOptions" />
          </div>
          <button class="px-5 py-2 gradient-brand text-white text-[0.82rem] font-semibold rounded-[9px] transition-opacity hover:opacity-90" @click="load">Generate</button>
        </div>
      </div>
    </template>

    <template v-if="data">
      <!-- Chart -->
      <div class="bg-surface-card border border-border rounded-2xl p-6 mb-6">
        <div v-if="data.rows.length > 0" class="h-72">
          <Line :data="chartData" :options="chartOptions" />
        </div>
        <div v-else class="h-32 flex items-center justify-center text-text-muted text-[0.82rem]">
          No transaction data for this month.
        </div>
      </div>

      <!-- Table -->
      <div class="bg-surface-card border border-border rounded-2xl overflow-hidden">
        <table class="w-full text-[0.82rem]">
          <thead>
            <tr class="border-b border-border bg-white/[0.02]">
              <th class="px-5 py-3 text-left   text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Date</th>
              <th class="px-5 py-3 text-right  text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Amount In</th>
              <th class="px-5 py-3 text-right  text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Fees</th>
              <th class="px-5 py-3 text-right  text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Amount Out</th>
              <th class="px-5 py-3 text-right  text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Balance</th>
            </tr>
          </thead>
          <tbody>
            <tr v-if="tableRows.length === 0">
              <td colspan="5" class="px-5 py-8 text-center text-text-secondary">No transactions found for this month.</td>
            </tr>
            <tr v-for="row in tableRows" :key="row.date"
              class="border-b border-border last:border-0 hover:bg-white/[0.02] transition-colors">
              <td class="px-5 py-2.5 font-mono text-text-muted">{{ row.date }}</td>
              <td class="px-5 py-2.5 text-right" :class="row.amountIn > 0 ? 'text-status-green font-medium' : 'text-text-muted'">{{ fmt(row.amountIn) }}</td>
              <td class="px-5 py-2.5 text-right" :class="row.fees > 0 ? 'text-status-yellow font-medium' : 'text-text-muted'">{{ fmt(row.fees) }}</td>
              <td class="px-5 py-2.5 text-right" :class="row.amountOut > 0 ? 'text-status-red font-medium' : 'text-text-muted'">{{ fmt(row.amountOut) }}</td>
              <td class="px-5 py-2.5 text-right font-medium" :class="row.balance >= 0 ? 'text-text-primary' : 'text-status-red'">{{ fmt(row.balance) }}</td>
            </tr>
          </tbody>
          <tfoot v-if="tableRows.length > 0">
            <tr class="border-t-2 border-border bg-white/[0.03]">
              <td class="px-5 py-3 text-[0.78rem] font-bold text-text-primary">Total</td>
              <td class="px-5 py-3 text-right text-[0.88rem] font-bold text-status-green">{{ fmt(data.totalAmountIn) }}</td>
              <td class="px-5 py-3 text-right text-[0.88rem] font-bold text-status-yellow">{{ fmt(data.totalFees) }}</td>
              <td class="px-5 py-3 text-right text-[0.88rem] font-bold text-status-red">{{ fmt(data.totalAmountOut) }}</td>
              <td class="px-5 py-3 text-right text-[0.88rem] font-bold text-text-primary">{{ fmt(data.totalAmountIn - data.totalFees - data.totalAmountOut) }}</td>
            </tr>
          </tfoot>
        </table>
      </div>
    </template>
    <ReportTimestamp />
</ReportPage>
</template>
