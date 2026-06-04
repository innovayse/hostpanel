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

interface TopClient {
  clientId: number; clientName: string; totalAmountIn: number
  totalFees: number; totalAmountOut: number; balance: number
}
const rows = ref<TopClient[]>([])

const chartColors = ['#3b82f6','#ef4444','#22c55e','#f59e0b','#8b5cf6','#ec4899','#14b8a6','#f97316','#6366f1','#64748b']

const chartData = computed(() => {
  const labels = rows.value.map(r => r.clientName)
  const data = rows.value.map(r => r.totalAmountIn)
  const total = data.reduce((a, b) => a + b, 0)
  return {
    labels: [...labels, 'Other'],
    datasets: [{
      data: [...data, Math.max(0, total * 0.01)],
      backgroundColor: [...chartColors.slice(0, rows.value.length), '#334155'],
      borderWidth: 0,
    }],
  }
})

const chartOptions = {
  responsive: true,
  maintainAspectRatio: false,
  plugins: {
    legend: { position: 'right' as const, labels: { color: '#94a3b8', boxWidth: 12, padding: 12, font: { size: 12 } } },
    tooltip: { callbacks: { label: (c: any) => `${c.label}: $${c.raw.toFixed(2)}` } },
  },
}

function fmt(n: number) { return `$${n.toFixed(2)}` }

function exportCsv() {
  const header = 'Client ID,Client Name,Total Amount In,Total Fees,Total Amount Out,Balance\n'
  const body = rows.value.map(r => `${r.clientId},"${r.clientName}",${r.totalAmountIn},${r.totalFees},${r.totalAmountOut},${r.balance}`).join('\n')
  const blob = new Blob([header + body], { type: 'text/csv' })
  const a = document.createElement('a'); a.href = URL.createObjectURL(blob); a.download = 'top-clients-by-income.csv'; a.click()
}

async function load() {
  loading.value = true; error.value = null
  try { rows.value = await request<TopClient[]>('/reports/top-clients-by-income?take=10') }
  catch { error.value = 'Failed to load report.' } finally { loading.value = false }
}

onMounted(load)
</script>

<template>
  <ReportPage title="Top 10 Clients by Income" description="This report shows the 10 clients with the highest net income according to the transactions entered." :loading :error>
    <template #filters>
      <div class="flex justify-end mb-4">
        <button class="px-4 py-2 bg-white/[0.04] border border-border text-text-secondary text-[0.82rem] font-medium rounded-[10px] hover:bg-white/[0.08] transition-colors" @click="exportCsv">Export CSV</button>
      </div>
    </template>

    <!-- Pie chart -->
    <div class="bg-surface-card border border-border rounded-2xl p-6 mb-6">
      <div class="h-72 flex items-center justify-center">
        <Pie v-if="rows.length > 0" :data="chartData" :options="chartOptions" />
        <span v-else class="text-text-secondary text-[0.82rem]">No data</span>
      </div>
    </div>

    <!-- Table -->
    <div class="bg-surface-card border border-border rounded-2xl overflow-hidden">
      <table class="w-full text-[0.82rem]">
        <thead>
          <tr class="border-b border-border bg-white/[0.02]">
            <th v-for="h in ['Client ID','Client Name','Total Amount In','Total Fees','Total Amount Out','Balance']" :key="h" class="px-5 py-3 text-left text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">{{ h }}</th>
          </tr>
        </thead>
        <tbody>
          <tr v-if="rows.length === 0"><td colspan="6" class="px-5 py-8 text-center text-text-secondary">No data.</td></tr>
          <tr v-for="row in rows" :key="row.clientId" class="border-b border-border last:border-0 hover:bg-white/[0.02] transition-colors">
            <td class="px-5 py-3 text-text-muted font-mono">{{ row.clientId }}</td>
            <td class="px-5 py-3 text-text-primary">{{ row.clientName }}</td>
            <td class="px-5 py-3 text-status-green font-medium">{{ fmt(row.totalAmountIn) }}</td>
            <td class="px-5 py-3 text-text-secondary">{{ fmt(row.totalFees) }}</td>
            <td class="px-5 py-3 text-status-red font-medium">{{ fmt(row.totalAmountOut) }}</td>
            <td class="px-5 py-3 text-text-primary font-medium">{{ fmt(row.balance) }}</td>
          </tr>
        </tbody>
      </table>
    </div>

    <div class="text-[0.68rem] text-text-muted mt-4">* denotes converted to default currency</div>
    <div class="text-[0.68rem] text-text-muted mt-1 text-right">Report Generated at {{ new Date().toLocaleString() }}</div>
    <ReportTimestamp />
</ReportPage>
</template>
