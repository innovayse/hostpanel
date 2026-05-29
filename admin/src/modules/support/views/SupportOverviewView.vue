<script setup lang="ts">
/**
 * Support overview dashboard -- displays period-filtered statistics,
 * average first reply time chart, and tickets-by-hour distribution chart.
 */
import { ref, computed, onMounted, watch } from 'vue'
import { useSupportStore } from '../stores/supportStore'
import { Bar } from 'vue-chartjs'
import {
  Chart as ChartJS,
  CategoryScale,
  LinearScale,
  BarElement,
  Title,
  Tooltip,
} from 'chart.js'

ChartJS.register(CategoryScale, LinearScale, BarElement, Title, Tooltip)

const store = useSupportStore()

/** Currently selected overview period. */
const selectedPeriod = ref('today')

/** Period dropdown options. */
const periodOptions = [
  { value: 'today', label: 'Today' },
  { value: 'yesterday', label: 'Yesterday' },
  { value: 'last7days', label: 'Last 7 Days' },
  { value: 'last30days', label: 'Last 30 Days' },
]

/** Stat cards configuration for rendering. */
const statCards = computed(() => [
  {
    label: 'New Tickets',
    value: store.overviewStats?.newTickets ?? 0,
    borderColor: 'border-l-primary-500',
  },
  {
    label: 'Client Replies',
    value: store.overviewStats?.clientReplies ?? 0,
    borderColor: 'border-l-primary-400',
  },
  {
    label: 'Staff Replies',
    value: store.overviewStats?.staffReplies ?? 0,
    borderColor: 'border-l-status-green',
  },
  {
    label: 'Tickets Without Reply',
    value: store.overviewStats?.ticketsWithoutReply ?? 0,
    borderColor: 'border-l-status-yellow',
  },
  {
    label: 'Average First Response',
    value: store.overviewStats?.averageFirstResponse ?? 'N/A',
    borderColor: 'border-l-text-muted',
  },
])

/** Whether the tickets-by-hour chart has data. */
const hasHourData = computed(() => {
  const hours = store.overviewStats?.ticketsByHour
  if (!hours || hours.length === 0) return false
  return hours.some(v => v > 0)
})

/** Chart data for tickets submitted by hour. */
const hourChartData = computed(() => ({
  labels: Array.from({ length: 24 }, (_, i) => String(i).padStart(2, '0')),
  datasets: [
    {
      label: 'Tickets',
      data: store.overviewStats?.ticketsByHour ?? Array.from({ length: 24 }, () => 0),
      backgroundColor: '#0ea5e9',
      borderRadius: 3,
    },
  ],
}))

/** Chart options for the horizontal bar chart. */
const hourChartOptions = computed(() => ({
  indexAxis: 'y' as const,
  responsive: true,
  maintainAspectRatio: false,
  plugins: {
    title: { display: false },
    tooltip: { enabled: true },
  },
  scales: {
    x: {
      beginAtZero: true,
      ticks: { color: 'rgba(255,255,255,0.5)', precision: 0 },
      grid: { color: 'rgba(255,255,255,0.06)' },
    },
    y: {
      ticks: { color: 'rgba(255,255,255,0.5)' },
      grid: { display: false },
    },
  },
}))

watch(selectedPeriod, () => {
  store.fetchOverview(selectedPeriod.value)
})

onMounted(() => {
  store.fetchOverview(selectedPeriod.value)
})
</script>

<template>
  <div class="p-4 sm:p-6 lg:p-8 w-full">

    <!-- Header -->
    <div class="flex items-center justify-between gap-4 mb-5 flex-wrap">
      <div>
        <h1 class="font-display font-bold text-[1.25rem] text-text-primary leading-none">Support Overview</h1>
        <p class="text-[0.78rem] text-text-secondary mt-1">Key metrics for the selected period</p>
      </div>
      <select
        v-model="selectedPeriod"
        class="bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.82rem] text-text-primary focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
      >
        <option
          v-for="opt in periodOptions"
          :key="opt.value"
          :value="opt.value"
          class="bg-surface-card text-text-primary"
        >
          {{ opt.label }}
        </option>
      </select>
    </div>

    <!-- Loading -->
    <div v-if="store.loading && !store.overviewStats" class="flex items-center gap-3 text-text-secondary text-sm mt-4">
      <span class="w-4 h-4 rounded-full border-2 border-primary-500/20 border-t-primary-500 animate-spin" />
      Loading overview...
    </div>

    <!-- Error -->
    <div v-else-if="store.error && !store.overviewStats" class="text-sm text-status-red bg-status-red/8 border border-status-red/20 rounded-xl p-4 mt-4">
      {{ store.error }}
    </div>

    <template v-else>

      <!-- Stat Cards -->
      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-5 gap-4 mb-6">
        <div
          v-for="card in statCards"
          :key="card.label"
          class="bg-surface-card border border-border rounded-2xl p-5 border-l-4"
          :class="card.borderColor"
        >
          <p class="text-[0.72rem] uppercase tracking-[0.08em] text-text-muted font-medium mb-2">{{ card.label }}</p>
          <p class="text-[1.5rem] font-bold text-text-primary leading-none">{{ card.value }}</p>
        </div>
      </div>

      <!-- Charts -->
      <div class="grid grid-cols-1 lg:grid-cols-2 gap-4">

        <!-- Average First Reply Time -->
        <div class="bg-surface-card border border-border rounded-2xl p-5">
          <h2 class="text-[0.82rem] font-semibold text-text-primary mb-4">Average First Reply Time</h2>
          <div class="flex items-center justify-center h-48">
            <p class="text-[0.82rem] text-text-muted">
              {{ store.overviewStats?.averageFirstResponse ?? 'No data' }}
            </p>
          </div>
        </div>

        <!-- Tickets by Hour -->
        <div class="bg-surface-card border border-border rounded-2xl p-5">
          <h2 class="text-[0.82rem] font-semibold text-text-primary mb-4">Tickets Submitted by Hour</h2>
          <div v-if="hasHourData" class="h-[400px]">
            <Bar :data="hourChartData" :options="hourChartOptions" />
          </div>
          <div v-else class="flex items-center justify-center h-48">
            <p class="text-[0.82rem] text-text-muted">No data</p>
          </div>
        </div>

      </div>

    </template>
  </div>
</template>
