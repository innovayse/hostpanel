<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { Pie } from 'vue-chartjs'
import { Chart as ChartJS, ArcElement, Tooltip, Legend } from 'chart.js'
import ReportPage from '../components/ReportPage.vue'
import ReportTimestamp from '../components/ReportTimestamp.vue'
import { useApi } from '../../../composables/useApi'

ChartJS.register(ArcElement, Tooltip, Legend)

const { request } = useApi()
const loading = ref(false)
const error = ref<string | null>(null)

interface PeriodRow { period: string; amountsByCurrency: Record<string, number> }
interface SummaryData { periods: PeriodRow[]; totals: Record<string, number>; currencies: string[] }
const data = ref<SummaryData | null>(null)

const currencySymbols: Record<string, string> = {
  USD: '$', EUR: '€', GBP: '£', AMD: '֏', RUB: '₽', KRW: '₩', AUD: 'A$', CAD: 'C$', JPY: '¥',
}

function fmtCurrency(amount: number, currency: string): string {
  const sym = currencySymbols[currency] ?? ''
  return `${sym}${amount.toFixed(2)}${currency}`
}

const chartColors = ['#22c55e', '#3b82f6', '#f59e0b', '#ef4444', '#8b5cf6']

const chartData = computed(() => {
  if (!data.value) return { labels: [], datasets: [] }
  // Aggregate all currencies into one total per period for the chart
  const labels = data.value.periods.map(p => p.period)
  const values = data.value.periods.map(p =>
    Object.values(p.amountsByCurrency).reduce((a, b) => a + b, 0)
  )
  // Only show periods with values > 0
  const filtered = labels.map((l, i) => ({ label: l, value: values[i] })).filter(x => x.value > 0)
  return {
    labels: filtered.map(x => x.label),
    datasets: [{
      data: filtered.map(x => x.value),
      backgroundColor: filtered.map((_, i) => chartColors[i % chartColors.length]),
      borderWidth: 0,
    }],
  }
})

const chartOptions = {
  responsive: true,
  maintainAspectRatio: false,
  plugins: {
    legend: { position: 'right' as const, labels: { color: '#94a3b8', boxWidth: 12, padding: 16, font: { size: 12 } } },
    tooltip: {
      callbacks: {
        label: (c: any) => {
          const total = c.dataset.data.reduce((a: number, b: number) => a + b, 0)
          const pct = total > 0 ? ((c.raw / total) * 100).toFixed(0) : '0'
          return `${c.label}: $${c.raw.toFixed(2)} (${pct}%)`
        },
      },
    },
  },
}


async function load() {
  loading.value = true; error.value = null
  try { data.value = await request<SummaryData>('/reports/aging-invoices-summary') }
  catch { error.value = 'Failed to load report.' } finally { loading.value = false }
}

onMounted(load)
</script>

<template>
  <ReportPage title="Aging Invoices" description="A summary of outstanding invoices broken down into the period of which they are overdue" :loading :error>
    <template v-if="data">
      <!-- Summary Table -->
      <div class="bg-surface-card border border-border rounded-2xl overflow-hidden mb-6">
        <table class="w-full text-[0.82rem]">
          <thead>
            <tr class="border-b border-border bg-white/[0.02]">
              <th class="px-5 py-3 text-left text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Period</th>
              <th v-for="cur in data.currencies" :key="cur" class="px-5 py-3 text-right text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">{{ cur }} Amount</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="row in data.periods" :key="row.period" class="border-b border-border last:border-0 hover:bg-white/[0.02] transition-colors">
              <td class="px-5 py-3 text-primary-400 font-medium">{{ row.period }}</td>
              <td v-for="cur in data.currencies" :key="cur" class="px-5 py-3 text-right text-text-primary">
                {{ fmtCurrency(row.amountsByCurrency[cur] ?? 0, cur) }}
              </td>
            </tr>
          </tbody>
          <tfoot>
            <tr class="border-t-2 border-border bg-white/[0.02]">
              <td class="px-5 py-3 font-semibold text-text-primary">Total</td>
              <td v-for="cur in data.currencies" :key="cur" class="px-5 py-3 text-right font-semibold text-text-primary">
                {{ fmtCurrency(data.totals[cur] ?? 0, cur) }}
              </td>
            </tr>
          </tfoot>
        </table>
      </div>

      <!-- Pie Chart -->
      <div class="bg-surface-card border border-border rounded-2xl p-6 mb-6">
        <div class="h-72 flex items-center justify-center">
          <Pie v-if="chartData.labels.length > 0" :data="chartData" :options="chartOptions" />
          <span v-else class="text-text-secondary text-[0.82rem]">No overdue invoices</span>
        </div>
      </div>

      <ReportTimestamp />
    </template>
  </ReportPage>
</template>
