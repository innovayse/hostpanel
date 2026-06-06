<script setup lang="ts">
import { ref, onMounted } from 'vue'
import ReportPage from '../components/ReportPage.vue'
import ReportTimestamp from '../components/ReportTimestamp.vue'
import AppClientSelect from '../../../components/AppClientSelect.vue'
import AppSpinner from '../../../components/AppSpinner.vue'
import DateRangePicker from '../../../components/DateRangePicker.vue'
import { useApi } from '../../../composables/useApi'

const { request } = useApi()
const loading = ref(false)
const error = ref<string | null>(null)
const clientsLoading = ref(false)

interface ClientOption { id: number; name: string; email: string }
const clientOptions = ref<ClientOption[]>([])
const selectedClient = ref<string | number>('')

const now = new Date()
const dateRange = ref<[string, string]>([
  `${now.getFullYear()}-01-01`,
  `${now.getFullYear()}-12-31`,
])
const minAmount = ref(0)
const maxAmount = ref(0)

interface Row { id: number; clientId: number; clientName: string; date: string; description: string; amount: number; adminUser: string | null }
interface ReportData { totalCount: number; totalAmount: number; rows: Row[] }

const data = ref<ReportData | null>(null)

async function loadClients() {
  clientsLoading.value = true
  try {
    const res = await request<{ id: number; name: string }[]>('/reports/client-picker')
    clientOptions.value = res.map(c => ({ id: c.id, name: c.name, email: '' }))
  } catch { /* ignore */ } finally { clientsLoading.value = false }
}

async function load() {
  loading.value = true; error.value = null
  try {
    const params = new URLSearchParams()
    if (selectedClient.value) params.set('clientId', String(selectedClient.value))
    if (dateRange.value) { params.set('from', dateRange.value[0]); params.set('to', dateRange.value[1]) }
    if (minAmount.value > 0) params.set('minAmount', String(minAmount.value))
    if (maxAmount.value > 0) params.set('maxAmount', String(maxAmount.value))
    data.value = await request<ReportData>(`/reports/credits-reviewer?${params}`)
  } catch { error.value = 'Failed to load report.' } finally { loading.value = false }
}

onMounted(() => { loadClients(); load() })
</script>

<template>
  <ReportPage title="Credits Reviewer" description="This report allows you to review all the credits issued to clients between 2 dates you specify." :loading :error>
    <template #filters>
      <div class="bg-surface-card border border-border rounded-2xl p-4 mb-6">
        <div class="grid grid-cols-2 gap-3 mb-3">
          <!-- Client -->
          <div>
            <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1">Client</label>
            <AppClientSelect v-model="selectedClient" :clients="clientOptions" placeholder="All clients" />
          </div>

          <!-- Date Range -->
          <div>
            <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1">Date Range</label>
            <DateRangePicker v-model="dateRange" />
          </div>

          <!-- Min Amount -->
          <div>
            <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1">Min. Amount</label>
            <AppSpinner v-model="minAmount" :step="1" :min="0" placeholder="Any" />
          </div>

          <!-- Max Amount -->
          <div>
            <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1">Max. Amount</label>
            <AppSpinner v-model="maxAmount" :step="1" :min="0" placeholder="Any" />
          </div>
        </div>
        <div class="flex justify-center">
          <button class="px-5 py-2 gradient-brand text-white text-[0.82rem] font-semibold rounded-[9px] transition-opacity hover:opacity-90" @click="load">
            Generate
          </button>
        </div>
      </div>
    </template>

    <template v-if="data">
      <!-- Summary -->
      <div v-if="data.totalCount > 0" class="mb-4 text-[0.82rem] text-text-secondary">
        <span class="text-text-primary font-semibold">{{ data.totalCount }}</span> credits found —
        Total: <span class="text-status-green font-semibold">${{ data.totalAmount.toFixed(2) }} USD</span>
      </div>

      <!-- Table -->
      <div class="bg-surface-card border border-border rounded-2xl overflow-hidden">
        <table class="w-full text-[0.82rem]">
          <thead>
            <tr class="border-b border-border bg-white/[0.02]">
              <th class="px-4 py-3 text-left   text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Credit ID</th>
              <th class="px-4 py-3 text-left   text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Client ID</th>
              <th class="px-4 py-3 text-left   text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Client Name</th>
              <th class="px-4 py-3 text-center text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Date</th>
              <th class="px-4 py-3 text-left   text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Description</th>
              <th class="px-4 py-3 text-right  text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Amount</th>
              <th class="px-4 py-3 text-left   text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Admin User</th>
            </tr>
          </thead>
          <tbody>
            <tr v-if="data.rows.length === 0">
              <td colspan="7" class="px-4 py-8 text-center text-text-secondary">No credits found for this period.</td>
            </tr>
            <tr v-for="row in data.rows" :key="row.id"
              class="border-b border-border last:border-0 hover:bg-white/[0.02] transition-colors">
              <td class="px-4 py-2.5 font-mono text-text-muted">{{ row.id }}</td>
              <td class="px-4 py-2.5 font-mono text-text-muted">{{ row.clientId }}</td>
              <td class="px-4 py-2.5 text-text-primary">{{ row.clientName }}</td>
              <td class="px-4 py-2.5 text-center font-mono text-text-secondary">{{ row.date }}</td>
              <td class="px-4 py-2.5 text-text-secondary">{{ row.description }}</td>
              <td class="px-4 py-2.5 text-right font-semibold text-status-green">${{ row.amount.toFixed(2) }} USD</td>
              <td class="px-4 py-2.5 text-text-muted">{{ row.adminUser ?? '—' }}</td>
            </tr>
          </tbody>
          <tfoot v-if="data.rows.length > 0">
            <tr class="border-t-2 border-border bg-white/[0.03]">
              <td colspan="5" class="px-4 py-3 text-[0.78rem] font-bold text-text-primary">Total</td>
              <td class="px-4 py-3 text-right text-[0.88rem] font-bold text-status-green">${{ data.totalAmount.toFixed(2) }} USD</td>
              <td></td>
            </tr>
          </tfoot>
        </table>
      </div>
    </template>
    <ReportTimestamp />
  </ReportPage>
</template>
