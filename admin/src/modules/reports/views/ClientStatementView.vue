<script setup lang="ts">
import { ref, onMounted } from 'vue'
import ReportPage from '../components/ReportPage.vue'
import AppClientSelect from '../../../components/AppClientSelect.vue'
import DateRangePicker from '../../../components/DateRangePicker.vue'
import { useApi } from '../../../composables/useApi'

const { request } = useApi()
const loading = ref(false)
const error = ref<string | null>(null)
const clientsLoading = ref(false)

interface ClientOption { id: number; name: string; email: string }
const clientOptions = ref<ClientOption[]>([])
const selectedClient = ref<string | number>('')
const dateRange = ref<[string, string] | null>(null)

interface StatementLine { type: string; date: string; description: string; debit: number; credit: number; balance: number }
interface StatementData { clientId: number; clientName: string; previousBalance: number; lines: StatementLine[]; endingBalance: number }
const data = ref<StatementData | null>(null)

function fmt(n: number) { return `$${n.toFixed(2)}` }

async function loadClients() {
  clientsLoading.value = true
  try {
    const res = await request<{ id: number; name: string }[]>('/reports/client-picker')
    clientOptions.value = res.map(c => ({ id: c.id, name: c.name, email: '' }))
  } catch { /* ignore */ } finally { clientsLoading.value = false }
}

async function load() {
  if (!selectedClient.value) return
  loading.value = true; error.value = null
  try {
    const p = new URLSearchParams({ clientId: String(selectedClient.value) })
    if (dateRange.value) { p.set('from', dateRange.value[0]); p.set('to', dateRange.value[1]) }
    data.value = await request<StatementData>(`/reports/client-statement?${p.toString()}`)
  } catch { error.value = 'Failed to load statement.' } finally { loading.value = false }
}

function printReport() { window.print() }

onMounted(loadClients)
</script>

<template>
  <ReportPage title="Client Account Register Balance" description="This report provides a statement of account for individual client accounts." :loading :error>
    <template #filters>
      <div class="bg-surface-card border border-border rounded-2xl p-4 mb-6">
        <div class="flex items-end gap-3">
          <div class="flex-1">
            <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Select Client</label>
            <AppClientSelect v-model="selectedClient" :clients="clientOptions" placeholder="Select a client..." />
          </div>
          <div class="flex-1">
            <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Date Range</label>
            <DateRangePicker v-model="dateRange" />
          </div>
          <button class="px-5 py-2 gradient-brand text-white text-[0.82rem] font-semibold rounded-[9px] transition-opacity hover:opacity-90 shrink-0" @click="load">Generate</button>
          <button class="px-4 py-2 bg-white/[0.04] border border-border text-text-secondary text-[0.78rem] font-medium rounded-[9px] hover:bg-white/[0.08] transition-colors shrink-0" @click="printReport">Print</button>
        </div>
      </div>
    </template>

    <template v-if="data">
      <!-- Previous Balance -->
      <div class="flex justify-end mb-3">
        <span class="text-[0.82rem] text-text-secondary">Previous Balance: <span class="font-medium text-text-primary">{{ fmt(data.previousBalance) }}</span></span>
      </div>

      <!-- Statement table -->
      <div class="bg-surface-card border border-border rounded-2xl overflow-hidden">
        <table class="w-full text-[0.82rem]">
          <thead>
            <tr class="border-b border-border bg-white/[0.02]">
              <th v-for="h in ['Type','Date','Description','Debit','Credit','Balance']" :key="h" class="px-5 py-3 text-left text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">{{ h }}</th>
            </tr>
          </thead>
          <tbody>
            <tr v-if="data.lines.length === 0"><td colspan="6" class="px-5 py-8 text-center text-text-secondary">No entries for this period.</td></tr>
            <tr v-for="(line, i) in data.lines" :key="i" class="border-b border-border last:border-0 hover:bg-white/[0.02] transition-colors">
              <td class="px-5 py-3">
                <span :class="line.type === 'Invoice' ? 'text-status-yellow' : 'text-status-green'" class="text-[0.72rem] font-medium">{{ line.type }}</span>
              </td>
              <td class="px-5 py-3 text-text-secondary">{{ line.date }}</td>
              <td class="px-5 py-3 text-text-primary">{{ line.description }}</td>
              <td class="px-5 py-3 font-medium" :class="line.debit > 0 ? 'text-status-red' : 'text-text-muted'">{{ fmt(line.debit) }}</td>
              <td class="px-5 py-3 font-medium" :class="line.credit > 0 ? 'text-status-green' : 'text-text-muted'">{{ fmt(line.credit) }}</td>
              <td class="px-5 py-3 text-text-primary font-medium">{{ fmt(line.balance) }}</td>
            </tr>
          </tbody>
          <tfoot>
            <tr class="border-t-2 border-border bg-white/[0.02]">
              <td colspan="5" class="px-5 py-3 text-right text-[0.78rem] font-semibold text-text-muted">Ending Balance:</td>
              <td class="px-5 py-3 text-text-primary font-bold text-[0.88rem]">{{ fmt(data.endingBalance) }}</td>
            </tr>
          </tfoot>
        </table>
      </div>

      <div class="text-[0.68rem] text-text-muted mt-4 text-right">Report Generated at {{ new Date().toLocaleString() }}</div>
    </template>
  </ReportPage>
</template>
