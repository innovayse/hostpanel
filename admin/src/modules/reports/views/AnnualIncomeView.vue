<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { Bar } from 'vue-chartjs'
import { Chart as ChartJS, CategoryScale, LinearScale, BarElement, Title, Tooltip, Legend } from 'chart.js'
import ReportPage from '../components/ReportPage.vue'
import AppSelect from '../../../components/AppSelect.vue'
import { useApi } from '../../../composables/useApi'

ChartJS.register(CategoryScale, LinearScale, BarElement, Title, Tooltip, Legend)

const { request } = useApi()
const loading = ref(false)
const error = ref<string | null>(null)
const selectedYear = ref(new Date().getFullYear())
const rows = ref<{ month: string; amount: number }[]>([])
const yearOptions = Array.from({ length: 5 }, (_, i) => {
  const y = new Date().getFullYear() - i
  return { value: y, label: String(y) }
})

const chartData = ref({
  labels: [] as string[],
  datasets: [{ label: 'Income', data: [] as number[], backgroundColor: '#ef4444', borderRadius: 6 }],
})

const chartOptions = {
  responsive: true, maintainAspectRatio: false,
  plugins: { legend: { display: false }, tooltip: { callbacks: { label: (c: any) => `$${c.raw.toFixed(2)}` } } },
  scales: {
    x: { grid: { color: 'rgba(255,255,255,0.05)' }, ticks: { color: '#64748b' } },
    y: { grid: { color: 'rgba(255,255,255,0.05)' }, ticks: { color: '#64748b', callback: (v: any) => `$${v}` } },
  },
}

async function load() {
  loading.value = true; error.value = null
  try {
    const data = await request<typeof rows.value>(`/reports/annual-income?year=${selectedYear.value}`)
    rows.value = data
    chartData.value.labels = data.map(r => r.month)
    chartData.value.datasets[0].data = data.map(r => r.amount)
  } catch { error.value = 'Failed to load report.' } finally { loading.value = false }
}

onMounted(load)
</script>

<template>
  <ReportPage title="Annual Income Report" description="Total income per month for the selected year" :loading :error>
    <template #filters>
      <div class="bg-surface-card border border-border rounded-2xl p-4 mb-6">
        <div class="flex items-end gap-4">
          <div class="w-32">
            <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Year</label>
            <AppSelect
              v-model="selectedYear"
              :options="yearOptions"
              @update:model-value="load"
            />
          </div>
        </div>
      </div>
    </template>

    <div class="bg-surface-card border border-border rounded-2xl p-6 mb-6">
      <div class="h-64"><Bar :data="chartData" :options="chartOptions" /></div>
    </div>
    <div class="bg-surface-card border border-border rounded-2xl overflow-hidden">
      <div class="grid grid-cols-2 gap-4 px-5 py-3 border-b border-border bg-white/[0.02]">
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Month</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Amount</span>
      </div>
      <div v-if="rows.length === 0" class="px-5 py-8 text-center text-text-secondary text-[0.82rem]">No data.</div>
      <div v-for="row in rows" :key="row.month" class="grid grid-cols-2 gap-4 px-5 py-3 border-b border-border last:border-0 hover:bg-white/[0.02] transition-colors text-[0.82rem]">
        <span class="text-text-primary">{{ row.month }}</span>
        <span class="text-status-green font-medium">${{ row.amount.toFixed(2) }}</span>
      </div>
    </div>
  </ReportPage>
</template>
