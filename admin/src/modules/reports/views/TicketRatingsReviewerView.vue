<script setup lang="ts">
import { ref, onMounted } from 'vue'
import ReportPage from '../components/ReportPage.vue'
import ReportTimestamp from '../components/ReportTimestamp.vue'
import DateRangePicker from '../../../components/DateRangePicker.vue'
import AppSelect from '../../../components/AppSelect.vue'
import { useApi } from '../../../composables/useApi'

const { request } = useApi()
const loading = ref(false)
const error = ref<string | null>(null)

interface Row { ticketId: number; date: string; message: string; adminName: string; rating: number }
interface ReportData { rows: Row[] }

const data = ref<ReportData | null>(null)
const now = new Date()
const dateRange = ref<[string, string] | null>([
  `${now.getFullYear()}-01-01`,
  `${now.getFullYear()}-12-31`,
])
const minRating = ref('')

const ratingOptions = [
  { value: '', label: 'Any' },
  { value: '1', label: '1+' },
  { value: '3', label: '3+' },
  { value: '5', label: '5+' },
  { value: '7', label: '7+' },
  { value: '9', label: '9+' },
]

async function load() {
  loading.value = true; error.value = null
  try {
    const params = new URLSearchParams()
    if (minRating.value) params.set('minRating', minRating.value)
    if (dateRange.value) { params.set('from', dateRange.value[0]); params.set('to', dateRange.value[1]) }
    data.value = await request<ReportData>(`/reports/ticket-ratings-reviewer?${params}`)
  } catch { error.value = 'Failed to load report.' } finally { loading.value = false }
}

function formatDate(iso: string) {
  return new Date(iso).toLocaleDateString('en-GB', { day: '2-digit', month: '2-digit', year: 'numeric' })
}

function ratingColor(r: number) {
  if (r >= 8) return 'text-status-green font-semibold'
  if (r >= 5) return 'text-status-yellow font-semibold'
  return 'text-status-red font-semibold'
}

onMounted(load)
</script>

<template>
  <ReportPage
    title="Support Ticket Ratings Reviewer"
    description="This report is showing all 0-ticket replies rated 1 between the selected date range for review."
    :loading :error>

    <template #filters>
      <div class="bg-surface-card border border-border rounded-2xl p-4 mb-6">
        <div class="flex items-end gap-3 flex-wrap">
          <div class="w-[140px]">
            <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1">Rating</label>
            <AppSelect v-model="minRating" :options="ratingOptions" />
          </div>
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
      <div class="bg-surface-card border border-border rounded-2xl overflow-hidden">
        <table class="w-full text-[0.82rem]">
          <thead>
            <tr class="border-b border-border bg-white/[0.02]">
              <th class="px-4 py-3 text-left text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Ticket #</th>
              <th class="px-4 py-3 text-left text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Date</th>
              <th class="px-4 py-3 text-left text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Message</th>
              <th class="px-4 py-3 text-left text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Admin</th>
              <th class="px-4 py-3 text-center text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Rating</th>
            </tr>
          </thead>
          <tbody>
            <tr v-if="data.rows.length === 0">
              <td colspan="5" class="px-4 py-8 text-center text-text-secondary">No Data Found For This Report.</td>
            </tr>
            <tr v-for="row in data.rows" :key="row.ticketId"
              class="border-b border-border last:border-0 hover:bg-white/[0.02] transition-colors">
              <td class="px-4 py-2.5 font-mono text-text-muted">#{{ row.ticketId }}</td>
              <td class="px-4 py-2.5 text-text-secondary">{{ formatDate(row.date) }}</td>
              <td class="px-4 py-2.5 text-text-secondary text-[0.78rem] max-w-xs truncate">{{ row.message }}</td>
              <td class="px-4 py-2.5 text-text-primary">{{ row.adminName }}</td>
              <td class="px-4 py-2.5 text-center" :class="ratingColor(row.rating)">{{ row.rating }}</td>
            </tr>
          </tbody>
        </table>
      </div>
    </template>

    <ReportTimestamp />
  </ReportPage>
</template>
