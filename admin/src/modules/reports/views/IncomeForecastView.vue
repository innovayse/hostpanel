<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { Line } from 'vue-chartjs'
import { Chart as ChartJS, CategoryScale, LinearScale, PointElement, LineElement, Title, Tooltip, Legend, Filler } from 'chart.js'
import ReportPage from '../components/ReportPage.vue'
import ReportTimestamp from '../components/ReportTimestamp.vue'
import { useApi } from '../../../composables/useApi'

ChartJS.register(CategoryScale, LinearScale, PointElement, LineElement, Title, Tooltip, Legend, Filler)

const { request } = useApi()
const loading = ref(false)
const error = ref<string | null>(null)
const rows = ref<{ month: string; monthly: number; quarterly: number; semiAnnual: number; annual: number; total: number }[]>([])

const chartData = ref({
  labels: [] as string[],
  datasets: [{ label: 'Cumulative Forecast', data: [] as number[], borderColor: '#6366f1', backgroundColor: 'rgba(99,102,241,0.15)', fill: true, tension: 0.4 }],
})

const chartOptions = {
  responsive: true, maintainAspectRatio: false,
  plugins: { legend: { labels: { color: '#94a3b8' } }, tooltip: { callbacks: { label: (c: any) => `$${c.raw.toFixed(2)}` } } },
  scales: {
    x: { grid: { color: 'rgba(255,255,255,0.05)' }, ticks: { color: '#64748b' } },
    y: { grid: { color: 'rgba(255,255,255,0.05)' }, ticks: { color: '#64748b', callback: (v: any) => `$${v}` } },
  },
}

async function load() {
  loading.value = true; error.value = null
  try {
    const data = await request<typeof rows.value>('/reports/income-forecast')
    rows.value = data
    chartData.value.labels = data.map(r => r.month)
    chartData.value.datasets[0].data = data.map(r => r.total)
  } catch { error.value = 'Failed to load report.' } finally { loading.value = false }
}

onMounted(load)
</script>

<template>
  <ReportPage title="Income Forecast" description="Projected income based on recurring billing cycles" :loading :error>
    <div class="bg-surface-card border border-border rounded-2xl p-6 mb-6">
      <div class="h-64"><Line :data="chartData" :options="chartOptions" /></div>
    </div>
    <div class="bg-surface-card border border-border rounded-2xl overflow-hidden">
      <div class="grid grid-cols-6 gap-4 px-5 py-3 border-b border-border bg-white/[0.02]">
        <span v-for="h in ['Month','Monthly','Quarterly','Semi Annual','Annual','Total']" :key="h" class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">{{ h }}</span>
      </div>
      <div v-if="rows.length === 0" class="px-5 py-8 text-center text-text-secondary text-[0.82rem]">No data.</div>
      <div v-for="row in rows" :key="row.month" class="grid grid-cols-6 gap-4 px-5 py-3 border-b border-border last:border-0 hover:bg-white/[0.02] transition-colors text-[0.82rem]">
        <span class="text-text-primary">{{ row.month }}</span>
        <span class="text-text-secondary">${{ row.monthly.toFixed(2) }}</span>
        <span class="text-text-secondary">${{ row.quarterly.toFixed(2) }}</span>
        <span class="text-text-secondary">${{ row.semiAnnual.toFixed(2) }}</span>
        <span class="text-text-secondary">${{ row.annual.toFixed(2) }}</span>
        <span class="text-status-green font-medium">${{ row.total.toFixed(2) }}</span>
      </div>
    </div>
    <ReportTimestamp />
</ReportPage>
</template>
