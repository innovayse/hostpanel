<script setup lang="ts">
import { ref, onMounted } from 'vue'
import ReportPage from '../components/ReportPage.vue'
import ReportTimestamp from '../components/ReportTimestamp.vue'
import DateRangePicker from '../../../components/DateRangePicker.vue'
import { useApi } from '../../../composables/useApi'

const { request } = useApi()
const loading = ref(false)
const error = ref<string | null>(null)

interface Row { tag: string; count: number }
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
    data.value = await request<ReportData>(`/reports/ticket-tags?${params}`)
  } catch { error.value = 'Failed to load report.' } finally { loading.value = false }
}

const maxCount = () => data.value ? Math.max(...data.value.rows.map(r => r.count), 1) : 1

onMounted(load)
</script>

<template>
  <ReportPage
    title="Ticket Tags Overview"
    description="This report provides an overview of ticket tags assigned to tickets for a given date range."
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
      <!-- Bar chart -->
      <div v-if="data.rows.length > 0" class="bg-surface-card border border-border rounded-2xl p-6 mb-6">
        <div class="space-y-3">
          <div v-for="row in data.rows" :key="row.tag" class="flex items-center gap-3">
            <div class="w-28 text-[0.78rem] text-text-secondary text-right truncate shrink-0">{{ row.tag }}</div>
            <div class="flex-1 h-5 bg-white/[0.05] rounded-full overflow-hidden">
              <div class="h-full rounded-full bg-accent/70 transition-all"
                :style="{ width: `${(row.count / maxCount()) * 100}%` }" />
            </div>
            <div class="w-8 text-[0.78rem] text-text-secondary text-right shrink-0">{{ row.count }}</div>
          </div>
        </div>
      </div>

      <!-- Table -->
      <div class="bg-surface-card border border-border rounded-2xl overflow-hidden">
        <table class="w-full text-[0.82rem]">
          <thead>
            <tr class="border-b border-border bg-white/[0.02]">
              <th class="px-4 py-3 text-left text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Tag</th>
              <th class="px-4 py-3 text-right text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Count</th>
            </tr>
          </thead>
          <tbody>
            <tr v-if="data.rows.length === 0">
              <td colspan="2" class="px-4 py-8 text-center text-text-secondary">No Data Found For This Report.</td>
            </tr>
            <tr v-for="row in data.rows" :key="row.tag"
              class="border-b border-border last:border-0 hover:bg-white/[0.02] transition-colors">
              <td class="px-4 py-2.5">
                <span class="inline-flex items-center px-2 py-0.5 rounded-full text-[0.72rem] font-medium bg-accent/10 text-accent border border-accent/20">
                  {{ row.tag }}
                </span>
              </td>
              <td class="px-4 py-2.5 text-right text-text-secondary font-medium">{{ row.count }}</td>
            </tr>
          </tbody>
        </table>
      </div>
    </template>

    <ReportTimestamp />
  </ReportPage>
</template>
