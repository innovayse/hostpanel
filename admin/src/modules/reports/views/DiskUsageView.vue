<script setup lang="ts">
import { ref, onMounted } from 'vue'
import ReportPage from '../components/ReportPage.vue'
import ReportTimestamp from '../components/ReportTimestamp.vue'
import { useApi } from '../../../composables/useApi'

const { request } = useApi()
const loading = ref(false)
const updating = ref(false)
const error = ref<string | null>(null)

interface Row { clientName: string; domain: string; diskUsage: string; diskLimit: string; diskPercent: number; bwUsage: string; bwLimit: string; bwPercent: number }
interface Server { serverName: string; rows: Row[] }
interface ReportData { servers: Server[]; lastUpdated: string | null }

const data = ref<ReportData | null>(null)

function formatLastUpdated(iso: string | null) {
  if (!iso) return null
  return new Date(iso).toLocaleDateString('en-GB', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' })
}

async function load() {
  loading.value = true; error.value = null
  try { data.value = await request<ReportData>('/reports/disk-usage') }
  catch { error.value = 'Failed to load report.' } finally { loading.value = false }
}

async function updateNow() {
  updating.value = true; error.value = null
  try { data.value = await request<ReportData>('/reports/disk-usage/update', { method: 'POST' }) }
  catch { error.value = 'Update failed.' } finally { updating.value = false }
}

onMounted(load)
</script>

<template>
  <ReportPage
    title="Disk Space & Bandwidth Usage Summary"
    description="This report shows the Disk Space & Bandwidth Usage Statistics for hosting accounts."
    :loading :error>

    <template #filters>
      <div class="flex items-center justify-between mb-6">
        <div v-if="data?.lastUpdated" class="text-[0.78rem] text-text-muted">
          Disk Space Usage Stats Last Updated at {{ formatLastUpdated(data.lastUpdated) }}
        </div>
        <div v-else class="text-[0.78rem] text-text-muted italic">No data yet — click Update Now to fetch from servers.</div>
        <button
          @click="updateNow"
          :disabled="updating"
          class="flex items-center gap-1.5 px-4 py-2 text-[0.82rem] font-medium rounded-[9px] border border-border bg-white/[0.04] text-text-secondary hover:bg-white/[0.08] transition-colors disabled:opacity-50 disabled:cursor-not-allowed">
          <svg class="w-3.5 h-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
            <polyline points="23 4 23 10 17 10"/><polyline points="1 20 1 14 7 14"/>
            <path d="M3.51 9a9 9 0 0 1 14.85-3.36L23 10M1 14l4.64 4.36A9 9 0 0 0 20.49 15"/>
          </svg>
          {{ updating ? 'Updating…' : 'Update Now' }}
        </button>
      </div>
    </template>

    <template v-if="data">
      <template v-if="data.servers.length === 0">
        <div class="bg-surface-card border border-border rounded-2xl px-4 py-8 text-center text-text-secondary text-[0.82rem]">
          No data found. Click <strong>Update Now</strong> to fetch stats from your hosting servers.
        </div>
      </template>

      <template v-for="server in data.servers" :key="server.serverName">
        <div class="mb-2 text-[0.78rem] font-semibold text-text-muted uppercase tracking-[0.08em]">
          {{ server.serverName }}
        </div>
        <div class="bg-surface-card border border-border rounded-2xl overflow-hidden mb-6">
          <table class="w-full text-[0.82rem]">
            <thead>
              <tr class="border-b border-border bg-white/[0.02]">
                <th class="px-4 py-3 text-left   text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Client Name / Domain</th>
                <th class="px-4 py-3 text-right  text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Disk Usage</th>
                <th class="px-4 py-3 text-right  text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Disk Limit</th>
                <th class="px-4 py-3 text-right  text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">% Used</th>
                <th class="px-4 py-3 text-right  text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">BW Usage</th>
                <th class="px-4 py-3 text-right  text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">BW Limit</th>
                <th class="px-4 py-3 text-right  text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">% Used</th>
              </tr>
            </thead>
            <tbody>
              <tr v-if="server.rows.length === 0">
                <td colspan="7" class="px-4 py-6 text-center text-text-secondary">No accounts on this server.</td>
              </tr>
              <tr v-for="row in server.rows" :key="row.domain"
                class="border-b border-border last:border-0 hover:bg-white/[0.02] transition-colors">
                <td class="px-4 py-2.5">
                  <div class="text-text-primary font-medium">{{ row.clientName }}</div>
                  <div class="text-[0.75rem] text-text-muted font-mono">{{ row.domain }}</div>
                </td>
                <td class="px-4 py-2.5 text-right font-mono text-text-secondary">{{ row.diskUsage }}</td>
                <td class="px-4 py-2.5 text-right font-mono text-text-muted">{{ row.diskLimit }}</td>
                <td class="px-4 py-2.5 text-right">
                  <span :class="row.diskPercent >= 90 ? 'text-status-red font-semibold' : row.diskPercent >= 70 ? 'text-status-yellow' : 'text-text-secondary'">
                    {{ row.diskPercent }}%
                  </span>
                </td>
                <td class="px-4 py-2.5 text-right font-mono text-text-secondary">{{ row.bwUsage }}</td>
                <td class="px-4 py-2.5 text-right font-mono text-text-muted">{{ row.bwLimit }}</td>
                <td class="px-4 py-2.5 text-right">
                  <span :class="row.bwPercent >= 90 ? 'text-status-red font-semibold' : row.bwPercent >= 70 ? 'text-status-yellow' : 'text-text-secondary'">
                    {{ row.bwPercent }}%
                  </span>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </template>
    </template>
    <ReportTimestamp />
  </ReportPage>
</template>
