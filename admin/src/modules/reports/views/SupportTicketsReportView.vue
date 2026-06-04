<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import ReportPage from '../components/ReportPage.vue'
import AppSelect from '../../../components/AppSelect.vue'
import { useApi } from '../../../composables/useApi'

const { request } = useApi()
const loading = ref(false)
const error = ref<string | null>(null)

const now = new Date()
const selectedYear = ref(now.getFullYear())
const selectedMonth = ref(now.getMonth() + 1)

const monthNames = ['January','February','March','April','May','June','July','August','September','October','November','December']
const monthOptions = monthNames.map((name, i) => ({ value: i + 1, label: name }))
const yearOptions = Array.from({ length: 5 }, (_, i) => now.getFullYear() - 2 + i).map(y => ({ value: y, label: String(y) }))

interface Row { adminName: string; dailyCounts: number[]; total: number }
interface ReportData { month: number; year: number; daysInMonth: number; rows: Row[] }

const data = ref<ReportData | null>(null)

const reportTitle = computed(() => data.value
  ? `Support Ticket Replies for ${monthNames[data.value.month - 1]} ${data.value.year}`
  : 'Support Ticket Replies')

const dayHeaders = computed(() =>
  data.value ? Array.from({ length: data.value.daysInMonth }, (_, i) => i + 1) : []
)

async function load() {
  loading.value = true; error.value = null
  try {
    data.value = await request<ReportData>(`/reports/support-ticket-replies?year=${selectedYear.value}&month=${selectedMonth.value}`)
  } catch { error.value = 'Failed to load report.' } finally { loading.value = false }
}

onMounted(load)
</script>

<template>
  <ReportPage :title="reportTitle" description="This report shows a breakdown of support tickets dealt with per admin for a given month." :loading :error>
    <template #filters>
      <div class="bg-surface-card border border-border rounded-2xl p-4 mb-6">
        <div class="flex items-end gap-3 flex-wrap">
          <div class="w-[160px]">
            <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1">Month</label>
            <AppSelect v-model="selectedMonth" :options="monthOptions" />
          </div>
          <div class="w-[110px]">
            <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1">Year</label>
            <AppSelect v-model="selectedYear" :options="yearOptions" />
          </div>
          <button class="px-5 py-2 gradient-brand text-white text-[0.82rem] font-semibold rounded-[9px] transition-opacity hover:opacity-90" @click="load">Generate</button>
        </div>
      </div>
    </template>

    <template v-if="data">
      <div class="bg-surface-card border border-border rounded-2xl overflow-x-auto">
        <table class="text-[0.78rem] w-full">
          <thead>
            <tr class="border-b border-border bg-white/[0.02]">
              <th class="px-4 py-3 text-left text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted sticky left-0 bg-surface-card z-10 min-w-[140px]">Admin</th>
              <th v-for="day in dayHeaders" :key="day"
                class="px-2 py-3 text-center text-[0.68rem] font-semibold text-text-muted w-8">{{ day }}</th>
              <th class="px-4 py-3 text-right text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Total</th>
            </tr>
          </thead>
          <tbody>
            <tr v-if="data.rows.length === 0">
              <td :colspan="data.daysInMonth + 2" class="px-4 py-8 text-center text-text-secondary">
                No Data Found For This Report
              </td>
            </tr>
            <tr v-for="row in data.rows" :key="row.adminName"
              class="border-b border-border last:border-0 hover:bg-white/[0.02] transition-colors">
              <td class="px-4 py-2.5 text-text-primary font-medium sticky left-0 bg-surface-card z-10">{{ row.adminName }}</td>
              <td v-for="(count, i) in row.dailyCounts" :key="i"
                class="px-2 py-2.5 text-center"
                :class="count > 0 ? 'text-text-primary font-semibold' : 'text-text-muted'">
                {{ count > 0 ? count : '' }}
              </td>
              <td class="px-4 py-2.5 text-right font-bold text-text-primary">{{ row.total }}</td>
            </tr>
          </tbody>
          <tfoot v-if="data.rows.length > 0">
            <tr class="border-t-2 border-border bg-white/[0.03]">
              <td class="px-4 py-2.5 text-[0.75rem] font-bold text-text-primary sticky left-0 bg-surface-card z-10">Total</td>
              <td v-for="(day, i) in dayHeaders" :key="day"
                class="px-2 py-2.5 text-center text-[0.78rem] font-semibold text-text-primary">
                {{ data.rows.reduce((s, r) => s + r.dailyCounts[i], 0) || '' }}
              </td>
              <td class="px-4 py-2.5 text-right text-[0.88rem] font-bold text-text-primary">
                {{ data.rows.reduce((s, r) => s + r.total, 0) }}
              </td>
            </tr>
          </tfoot>
        </table>
      </div>
    </template>
  </ReportPage>
</template>
