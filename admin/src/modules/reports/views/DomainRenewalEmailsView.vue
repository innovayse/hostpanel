<script setup lang="ts">
import { ref, onMounted } from 'vue'
import ReportPage from '../components/ReportPage.vue'
import ReportTimestamp from '../components/ReportTimestamp.vue'
import AppClientSelect from '../../../components/AppClientSelect.vue'
import AppSelect from '../../../components/AppSelect.vue'
import DateRangePicker from '../../../components/DateRangePicker.vue'
import { useApi } from '../../../composables/useApi'

const { request } = useApi()
const loading = ref(false)
const error = ref<string | null>(null)

interface Row {
  clientName: string
  domain: string
  sentAt: string
  reminder: string
  recipients: string
}
interface ReportData { rows: Row[] }

const data = ref<ReportData | null>(null)

const clientOptions = ref<{ id: number; name: string }[]>([])
const selectedClientId = ref<number | null>(null)
const registrar = ref('')
const domain = ref('')
const dateRange = ref<[string, string] | null>(null)

const registrarOptions = [
  { value: '', label: 'Any' },
  { value: 'name.am', label: 'Name.am' },
  { value: 'namecheap', label: 'Namecheap' },
]

async function loadClients() {
  try {
    clientOptions.value = await request<{ id: number; name: string }[]>('/reports/client-picker')
  } catch { /* ignore */ }
}

async function load() {
  loading.value = true; error.value = null
  try {
    const params = new URLSearchParams()
    if (selectedClientId.value) params.set('clientId', String(selectedClientId.value))
    if (registrar.value) params.set('registrar', registrar.value)
    if (domain.value.trim()) params.set('domain', domain.value.trim())
    if (dateRange.value) { params.set('from', dateRange.value[0]); params.set('to', dateRange.value[1]) }
    const qs = params.toString()
    data.value = await request<ReportData>(`/reports/domain-renewal-emails${qs ? '?' + qs : ''}`)
  } catch { error.value = 'Failed to load report.' } finally { loading.value = false }
}

function formatDate(iso: string) {
  return new Date(iso).toLocaleDateString('en-GB', { day: '2-digit', month: '2-digit', year: 'numeric' })
}

onMounted(() => { loadClients(); load() })
</script>

<template>
  <ReportPage
    title="Domain Renewal Reminder Emails"
    description="This report can be used to generate a custom export of the Domain Renewal Reminder Emails."
    :loading :error>

    <template #filters>
      <div class="bg-surface-card border border-border rounded-2xl p-4 mb-6">
        <div class="flex items-end gap-3 flex-wrap">
          <!-- Client -->
          <div class="flex-1 min-w-[200px]">
            <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1">Client</label>
            <AppClientSelect :clients="clientOptions" v-model="selectedClientId" placeholder="Start Typing to Search Clients" />
          </div>

          <!-- Registrar -->
          <div class="w-[160px]">
            <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1">Registrar</label>
            <AppSelect v-model="registrar" :options="registrarOptions" />
          </div>

          <!-- Domain -->
          <div class="w-[180px]">
            <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1">Domain</label>
            <input
              v-model="domain"
              type="text"
              placeholder="Optional"
              class="w-full px-3 py-2 text-[0.82rem] text-text-primary bg-white/[0.05] border border-border rounded-[9px] focus:outline-none focus:border-primary-500/40 transition-colors placeholder:text-text-muted" />
          </div>

          <!-- Date Range -->
          <div class="flex-1 min-w-[220px]">
            <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1">Date Range</label>
            <DateRangePicker v-model="dateRange" />
          </div>

          <!-- Apply -->
          <button
            @click="load"
            class="px-5 py-2 gradient-brand text-white text-[0.82rem] font-semibold rounded-[9px] transition-opacity hover:opacity-90">
            Apply
          </button>
        </div>
      </div>
    </template>

    <template v-if="data">
      <div class="bg-surface-card border border-border rounded-2xl overflow-hidden">
        <table class="w-full text-[0.82rem]">
          <thead>
            <tr class="border-b border-border bg-white/[0.02]">
              <th class="px-4 py-3 text-left text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Client</th>
              <th class="px-4 py-3 text-left text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Domain</th>
              <th class="px-4 py-3 text-left text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Date Sent</th>
              <th class="px-4 py-3 text-left text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Reminder</th>
              <th class="px-4 py-3 text-left text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Recipients</th>
              <th class="px-4 py-3 text-center text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Sent</th>
            </tr>
          </thead>
          <tbody>
            <tr v-if="data.rows.length === 0">
              <td colspan="6" class="px-4 py-8 text-center text-text-secondary text-[0.82rem]">
                No Data Found For This Report.
              </td>
            </tr>
            <tr
              v-for="(row, i) in data.rows" :key="i"
              class="border-b border-border last:border-0 hover:bg-white/[0.02] transition-colors">
              <td class="px-4 py-2.5 text-text-primary font-medium">{{ row.clientName }}</td>
              <td class="px-4 py-2.5 font-mono text-text-secondary">{{ row.domain }}</td>
              <td class="px-4 py-2.5 text-text-secondary">{{ formatDate(row.sentAt) }}</td>
              <td class="px-4 py-2.5 text-text-secondary">{{ row.reminder }}</td>
              <td class="px-4 py-2.5 text-text-muted text-[0.78rem]">{{ row.recipients }}</td>
              <td class="px-4 py-2.5 text-center">
                <svg class="w-4 h-4 text-status-green inline-block" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round">
                  <polyline points="20 6 9 17 4 12"/>
                </svg>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </template>

    <ReportTimestamp />
  </ReportPage>
</template>
