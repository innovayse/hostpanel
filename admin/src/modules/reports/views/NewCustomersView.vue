<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { Line } from 'vue-chartjs'
import { Chart as ChartJS, CategoryScale, LinearScale, PointElement, LineElement, Title, Tooltip, Legend, Filler } from 'chart.js'
import ReportPage from '../components/ReportPage.vue'
import AppSelect from '../../../components/AppSelect.vue'
import { useApi } from '../../../composables/useApi'

ChartJS.register(CategoryScale, LinearScale, PointElement, LineElement, Title, Tooltip, Legend, Filler)

const { request } = useApi()
const loading = ref(false)
const error = ref<string | null>(null)
const selectedYear = ref(new Date().getFullYear())
const rows = ref<{ month: string; newSignups: number; ordersPlaced: number; ordersCompleted: number }[]>([])

const yearOptions = Array.from({ length: 10 }, (_, i) => {
  const y = new Date().getFullYear() - i
  return { value: y, label: String(y) }
})

const chartData = ref({
  labels: [] as string[],
  datasets: [
    { label: 'New Signups', data: [] as number[], borderColor: '#10b981', backgroundColor: 'rgba(16,185,129,0.1)', fill: true, tension: 0.4 },
    { label: 'Orders Placed', data: [] as number[], borderColor: '#6366f1', backgroundColor: 'rgba(99,102,241,0.1)', fill: true, tension: 0.4 },
    { label: 'Orders Completed', data: [] as number[], borderColor: '#f59e0b', backgroundColor: 'rgba(245,158,11,0.1)', fill: true, tension: 0.4 },
  ],
})

const chartOptions = {
  responsive: true, maintainAspectRatio: false,
  plugins: { legend: { position: 'right' as const, labels: { color: '#94a3b8', boxWidth: 12 } } },
  scales: {
    x: { grid: { color: 'rgba(255,255,255,0.05)' }, ticks: { color: '#64748b' } },
    y: { grid: { color: 'rgba(255,255,255,0.05)' }, ticks: { color: '#64748b' } },
  },
}

// Totals
const totalSignups = ref(0)
const totalPlaced = ref(0)
const totalCompleted = ref(0)

async function load() {
  loading.value = true; error.value = null
  try {
    const data = await request<{ month: string; newSignups: number; ordersPlaced: number; ordersCompleted: number; conversionRate: number }[]>(`/reports/new-customers?year=${selectedYear.value}`)
    rows.value = data
    chartData.value.labels = data.map(r => r.month)
    chartData.value.datasets[0].data = data.map(r => r.newSignups)
    chartData.value.datasets[1].data = data.map(r => r.ordersPlaced)
    chartData.value.datasets[2].data = data.map(r => r.ordersCompleted)
    totalSignups.value = data.reduce((s, r) => s + r.newSignups, 0)
    totalPlaced.value = data.reduce((s, r) => s + r.ordersPlaced, 0)
    totalCompleted.value = data.reduce((s, r) => s + r.ordersCompleted, 0)
  } catch { error.value = 'Failed to load report.' } finally { loading.value = false }
}

onMounted(load)
</script>

<template>
  <ReportPage title="New Customers" description="Monthly new signups and order activity" :loading :error>
    <template #filters>
      <div class="bg-surface-card border border-border rounded-2xl p-4 mb-6">
        <div class="flex items-end gap-4">
          <div class="w-32">
            <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Year</label>
            <AppSelect v-model="selectedYear" :options="yearOptions" @update:model-value="load" />
          </div>
        </div>
      </div>
    </template>

    <div class="bg-surface-card border border-border rounded-2xl p-6 mb-6">
      <div class="h-64"><Line :data="chartData" :options="chartOptions" /></div>
    </div>

    <div class="bg-surface-card border border-border rounded-2xl overflow-hidden">
      <div class="grid grid-cols-4 gap-4 px-5 py-3 border-b border-border bg-white/[0.02]">
        <span v-for="h in ['Month', 'New Signups', 'Orders Placed', 'Orders Completed']" :key="h" class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">{{ h }}</span>
      </div>
      <div v-if="rows.length === 0" class="px-5 py-8 text-center text-text-secondary text-[0.82rem]">No data.</div>
      <div v-for="row in rows" :key="row.month" class="grid grid-cols-4 gap-4 px-5 py-3 border-b border-border last:border-0 hover:bg-white/[0.02] transition-colors text-[0.82rem]">
        <span class="text-text-primary">{{ row.month }}</span>
        <span class="text-status-green">{{ row.newSignups }}</span>
        <span class="text-text-secondary">{{ row.ordersPlaced }}</span>
        <span class="text-text-secondary">{{ row.ordersCompleted }}</span>
      </div>
      <!-- Totals -->
      <div class="grid grid-cols-4 gap-4 px-5 py-3 border-t-2 border-border bg-white/[0.02] text-[0.82rem]">
        <span class="font-semibold text-text-primary">Total</span>
        <span class="font-semibold text-status-green">{{ totalSignups }}</span>
        <span class="font-semibold text-text-primary">{{ totalPlaced }}</span>
        <span class="font-semibold text-text-primary">{{ totalCompleted }}</span>
      </div>
    </div>
  </ReportPage>
</template>
