<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import ReportPage from '../components/ReportPage.vue'
import ReportTimestamp from '../components/ReportTimestamp.vue'
import DateRangePicker from '../../../components/DateRangePicker.vue'
import { useApi } from '../../../composables/useApi'

const { request } = useApi()
const loading = ref(false)
const error = ref<string | null>(null)

interface Row {
  staffName: string
  r1: number; r2: number; r3: number; r4: number; r5: number
  r6: number; r7: number; r8: number; r9: number; r10: number
  totalRatings: number
  averageRating: number
}
interface ReportData { rows: Row[] }

const data = ref<ReportData | null>(null)
const now = new Date()
const dateRange = ref<[string, string] | null>([
  `${now.getFullYear()}-01-01`,
  `${now.getFullYear()}-12-31`,
])

async function load() {
  loading.value = true; error.value = null
  try {
    const params = new URLSearchParams()
    if (dateRange.value) { params.set('from', dateRange.value[0]); params.set('to', dateRange.value[1]) }
    data.value = await request<ReportData>(`/reports/ticket-feedback-scores?${params}`)
  } catch { error.value = 'Failed to load report.' } finally { loading.value = false }
}

function ratingColor(r: number) {
  if (r >= 8) return 'text-status-green font-semibold'
  if (r >= 5) return 'text-status-yellow font-semibold'
  return 'text-status-red font-semibold'
}

// Chart helpers — vertical bars, Y axis 0-10, X axis = staff names
const CHART_H = 320
const BAR_W = 56
const Y_TICKS = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10]

function barHeight(avg: number) {
  return (avg / 10) * CHART_H
}

const chartWidth = computed(() => {
  const rows = data.value?.rows ?? []
  return Math.max(600, rows.length * (BAR_W + 36) + 80)
})

onMounted(load)
</script>

<template>
  <ReportPage
    title="Ticket Feedback Scores"
    description="This report provides a summary of scores received on a per staff member basis for a given date range."
    :loading :error>

    <template #filters>
      <div class="bg-surface-card border border-border rounded-2xl p-4 mb-6">
        <div class="flex items-end gap-3 flex-wrap">
          <div class="flex-1 min-w-[220px]">
            <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1">Date Range</label>
            <DateRangePicker v-model="dateRange" />
          </div>
          <button @click="load" class="px-5 py-2 gradient-brand text-white text-[0.82rem] font-semibold rounded-[9px] transition-opacity hover:opacity-90">
            Generate Report
          </button>
        </div>
      </div>
    </template>

    <template v-if="data">
      <!-- Vertical bar chart -->
      <div class="bg-surface-card border border-border rounded-2xl p-6 mb-6 overflow-x-auto">
        <svg :width="chartWidth" :height="CHART_H + 70" class="overflow-visible" style="min-width: 100%">
          <!-- Y axis label -->
          <text
            :x="-CHART_H / 2"
            y="14"
            transform="rotate(-90)"
            text-anchor="middle"
            class="fill-text-muted"
            font-size="11">Average Rating</text>

          <!-- Y grid lines + labels -->
          <g transform="translate(40, 0)">
            <line
              v-for="tick in Y_TICKS" :key="tick"
              x1="0" :y1="CHART_H - (tick / 10) * CHART_H"
              :x2="chartWidth - 40" :y2="CHART_H - (tick / 10) * CHART_H"
              stroke="currentColor" stroke-opacity="0.08" />
            <text
              v-for="tick in Y_TICKS" :key="`l${tick}`"
              x="-6" :y="CHART_H - (tick / 10) * CHART_H + 4"
              text-anchor="end" font-size="10"
              class="fill-text-muted">{{ tick }}</text>

            <!-- Bars -->
            <g v-for="(row, i) in data.rows" :key="row.staffName"
              :transform="`translate(${i * (BAR_W + 36) + 12}, 0)`">
              <rect
                :x="0"
                :y="CHART_H - barHeight(row.averageRating)"
                :width="BAR_W"
                :height="barHeight(row.averageRating)"
                rx="3"
                class="fill-accent opacity-70" />
              <!-- Staff name label -->
              <text
                :x="BAR_W / 2"
                :y="CHART_H + 16"
                text-anchor="middle"
                font-size="10"
                class="fill-text-muted">{{ row.staffName }}</text>
            </g>

            <!-- X axis label -->
            <text
              :x="(data.rows.length * (BAR_W + 36)) / 2"
              :y="CHART_H + 56"
              text-anchor="middle"
              font-size="11"
              font-style="italic"
              class="fill-text-muted">Staff Name</text>
          </g>
        </svg>
      </div>

      <!-- Table -->
      <div class="bg-surface-card border border-border rounded-2xl overflow-hidden overflow-x-auto">
        <table class="w-full text-[0.82rem]">
          <thead>
            <tr class="border-b border-border bg-white/[0.02]">
              <th class="px-4 py-3 text-left text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Staff Name</th>
              <th v-for="n in 10" :key="n" class="px-3 py-3 text-right text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">{{ n }}</th>
              <th class="px-4 py-3 text-right text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Total Ratings</th>
              <th class="px-4 py-3 text-right text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Average Rating</th>
            </tr>
          </thead>
          <tbody>
            <tr v-if="data.rows.length === 0">
              <td colspan="13" class="px-4 py-8 text-center text-text-secondary">No Data Found For This Report.</td>
            </tr>
            <tr v-for="row in data.rows" :key="row.staffName"
              class="border-b border-border last:border-0 hover:bg-white/[0.02] transition-colors">
              <td class="px-4 py-2.5 text-text-primary font-medium">{{ row.staffName }}</td>
              <td class="px-3 py-2.5 text-right text-text-secondary">{{ row.r1 || '—' }}</td>
              <td class="px-3 py-2.5 text-right text-text-secondary">{{ row.r2 || '—' }}</td>
              <td class="px-3 py-2.5 text-right text-text-secondary">{{ row.r3 || '—' }}</td>
              <td class="px-3 py-2.5 text-right text-text-secondary">{{ row.r4 || '—' }}</td>
              <td class="px-3 py-2.5 text-right text-text-secondary">{{ row.r5 || '—' }}</td>
              <td class="px-3 py-2.5 text-right text-text-secondary">{{ row.r6 || '—' }}</td>
              <td class="px-3 py-2.5 text-right text-text-secondary">{{ row.r7 || '—' }}</td>
              <td class="px-3 py-2.5 text-right text-text-secondary">{{ row.r8 || '—' }}</td>
              <td class="px-3 py-2.5 text-right text-text-secondary">{{ row.r9 || '—' }}</td>
              <td class="px-3 py-2.5 text-right text-text-secondary">{{ row.r10 || '—' }}</td>
              <td class="px-4 py-2.5 text-right text-text-secondary font-medium">{{ row.totalRatings }}</td>
              <td class="px-4 py-2.5 text-right" :class="ratingColor(row.averageRating)">{{ row.averageRating.toFixed(2) }}</td>
            </tr>
          </tbody>
        </table>
      </div>
    </template>

    <ReportTimestamp />
  </ReportPage>
</template>
