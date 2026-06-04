<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { Line } from 'vue-chartjs'
import { Chart as ChartJS, CategoryScale, LinearScale, PointElement, LineElement, Title, Tooltip, Legend, Filler } from 'chart.js'
import DateRangePicker from '../../../components/DateRangePicker.vue'
import ReportPage from '../components/ReportPage.vue'
import ReportTimestamp from '../components/ReportTimestamp.vue'
import { useApi } from '../../../composables/useApi'

ChartJS.register(CategoryScale, LinearScale, PointElement, LineElement, Title, Tooltip, Legend, Filler)

const { request } = useApi()
const loading = ref(false)
const error = ref<string | null>(null)
const dateRange = ref<[string, string] | null>(null)

const rows = ref<{ date: string; completedOrders: number; newInvoices: number; paidInvoices: number; openedTickets: number; ticketReplies: number; cancellationRequests: number }[]>([])

const chartData = ref({
  labels: [] as string[],
  datasets: [
    { label: 'Completed Orders', data: [] as number[], borderColor: '#6366f1', backgroundColor: 'rgba(99,102,241,0.1)', fill: true, tension: 0.4 },
    { label: 'New Invoices', data: [] as number[], borderColor: '#10b981', backgroundColor: 'rgba(16,185,129,0.1)', fill: true, tension: 0.4 },
    { label: 'Paid Invoices', data: [] as number[], borderColor: '#f59e0b', backgroundColor: 'rgba(245,158,11,0.1)', fill: true, tension: 0.4 },
    { label: 'Opened Tickets', data: [] as number[], borderColor: '#06b6d4', backgroundColor: 'rgba(6,182,212,0.1)', fill: true, tension: 0.4 },
    { label: 'Ticket Replies', data: [] as number[], borderColor: '#8b5cf6', backgroundColor: 'rgba(139,92,246,0.1)', fill: true, tension: 0.4 },
    { label: 'Cancellation Requests', data: [] as number[], borderColor: '#ec4899', backgroundColor: 'rgba(236,72,153,0.1)', fill: true, tension: 0.4 },
  ],
})

const chartOptions = {
  responsive: true, maintainAspectRatio: false,
  plugins: { legend: { position: 'right' as const, labels: { color: '#94a3b8', boxWidth: 12, padding: 16 } }, tooltip: { mode: 'index' as const, intersect: false } },
  scales: {
    x: { grid: { color: 'rgba(255,255,255,0.05)' }, ticks: { color: '#64748b' } },
    y: { grid: { color: 'rgba(255,255,255,0.05)' }, ticks: { color: '#64748b' } },
  },
}

async function load() {
  loading.value = true; error.value = null
  try {
    const params = new URLSearchParams()
    if (dateRange.value) { params.set('from', dateRange.value[0]); params.set('to', dateRange.value[1]) }
    const data = await request<typeof rows.value>(`/reports/daily-performance?${params}`)
    rows.value = data
    chartData.value.labels = data.map(r => r.date)
    chartData.value.datasets[0].data = data.map(r => r.completedOrders)
    chartData.value.datasets[1].data = data.map(r => r.newInvoices)
    chartData.value.datasets[2].data = data.map(r => r.paidInvoices)
    chartData.value.datasets[3].data = data.map(r => r.openedTickets)
    chartData.value.datasets[4].data = data.map(r => r.ticketReplies)
    chartData.value.datasets[5].data = data.map(r => r.cancellationRequests)
  } catch { error.value = 'Failed to load report.' } finally { loading.value = false }
}

onMounted(load)
</script>

<template>
  <ReportPage title="Daily Performance" description="Overview of daily activity metrics" :loading :error>
    <template #filters>
      <div class="bg-surface-card border border-border rounded-2xl p-4 mb-6">
        <div class="flex items-end gap-4 flex-wrap">
          <div class="flex-1 min-w-[200px]">
            <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Date Range</label>
            <DateRangePicker v-model="dateRange" placeholder="Select date range..." />
          </div>
          <div class="flex gap-2">
            <button @click="load" class="gradient-brand px-4 py-2 text-[0.82rem] font-semibold text-white rounded-[9px]">Apply</button>
            <button @click="dateRange = null; load()" class="px-3 py-2 text-[0.78rem] text-text-muted hover:text-text-primary transition-colors">Clear</button>
          </div>
        </div>
      </div>
    </template>

    <div class="bg-surface-card border border-border rounded-2xl p-6 mb-6">
      <div class="h-72"><Line :data="chartData" :options="chartOptions" /></div>
    </div>

    <div class="bg-surface-card border border-border rounded-2xl overflow-hidden">
      <div class="grid grid-cols-7 gap-4 px-5 py-3 border-b border-border bg-white/[0.02]">
        <span v-for="h in ['Date','Completed Orders','New Invoices','Paid Invoices','Opened Tickets','Ticket Replies','Cancellation Requests']" :key="h" class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">{{ h }}</span>
      </div>
      <div v-if="rows.length === 0" class="px-5 py-8 text-center text-text-secondary text-[0.82rem]">No data found.</div>
      <div v-for="row in rows" :key="row.date" class="grid grid-cols-7 gap-4 px-5 py-3 border-b border-border last:border-0 hover:bg-white/[0.02] transition-colors text-[0.82rem]">
        <span class="text-text-muted font-mono">{{ row.date }}</span>
        <span>{{ row.completedOrders }}</span>
        <span>{{ row.newInvoices }}</span>
        <span class="text-status-green">{{ row.paidInvoices }}</span>
        <span>{{ row.openedTickets }}</span>
        <span>{{ row.ticketReplies }}</span>
        <span>{{ row.cancellationRequests }}</span>
      </div>
    </div>
    <ReportTimestamp />
</ReportPage>
</template>
