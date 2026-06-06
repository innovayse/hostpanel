<script setup lang="ts">
import { ref, onMounted } from 'vue'
import ReportPage from '../components/ReportPage.vue'
import ReportTimestamp from '../components/ReportTimestamp.vue'
import { useApi } from '../../../composables/useApi'

const { request } = useApi()
const loading = ref(false)
const revalidating = ref(false)
const error = ref<string | null>(null)
const includeInactive = ref(false)

interface SslRow { domainName: string; hasSsl: boolean; issuer: string | null; expiresAt: string | null; lastUpdate: string; isActive: boolean }
interface SslGroup { groupName: string; rows: SslRow[] }
interface ReportData { groups: SslGroup[] }

const data = ref<ReportData | null>(null)

async function load() {
  loading.value = true; error.value = null
  try {
    data.value = await request<ReportData>(`/reports/ssl-monitoring?includeInactive=${includeInactive.value}`)
  } catch { error.value = 'Failed to load report.' } finally { loading.value = false }
}

async function revalidate() {
  revalidating.value = true; error.value = null
  try {
    data.value = await request<ReportData>(`/reports/ssl-monitoring/revalidate?includeInactive=${includeInactive.value}`, { method: 'POST' })
  } catch { error.value = 'Re-validation failed.' } finally { revalidating.value = false }
}

function toggleInactive() {
  includeInactive.value = !includeInactive.value
  load()
}

onMounted(load)
</script>

<template>
  <ReportPage
    title="SSL Certificate Monitoring"
    description="Displays a list of domains with their SSL status, if available."
    :loading :error>

    <template #filters>
      <div class="flex items-center gap-3 mb-6">
        <button
          @click="toggleInactive"
          class="px-4 py-2 text-[0.82rem] font-medium rounded-[9px] border transition-colors"
          :class="includeInactive
            ? 'bg-brand-500/10 border-brand-500/40 text-brand-400'
            : 'bg-white/[0.04] border-border text-text-secondary hover:bg-white/[0.08]'">
          {{ includeInactive ? 'Hide Inactive Domains' : 'Show Inactive Domains' }}
        </button>
        <button
          @click="revalidate"
          :disabled="revalidating"
          class="px-4 py-2 text-[0.82rem] font-medium rounded-[9px] border border-border bg-white/[0.04] text-text-secondary hover:bg-white/[0.08] transition-colors disabled:opacity-50 disabled:cursor-not-allowed">
          {{ revalidating ? 'Re-validating...' : 'Re-validate SSL Status' }}
        </button>
      </div>
    </template>

    <template v-if="data">
      <div class="bg-surface-card border border-border rounded-2xl overflow-hidden">
        <table class="w-full text-[0.82rem]">
          <thead>
            <tr class="border-b border-border bg-white/[0.02]">
              <th class="px-4 py-3 text-left   text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Domain Name</th>
              <th class="px-4 py-3 text-center text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Status</th>
              <th class="px-4 py-3 text-left   text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Issuer</th>
              <th class="px-4 py-3 text-center text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Expiry</th>
              <th class="px-4 py-3 text-right  text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Last Update</th>
            </tr>
          </thead>
          <tbody>
            <template v-for="group in data.groups" :key="group.groupName">
              <!-- Group header -->
              <tr class="border-b border-border bg-white/[0.04]">
                <td colspan="5" class="px-4 py-2 text-[0.78rem] font-bold text-text-primary">{{ group.groupName }}</td>
              </tr>
              <!-- No records -->
              <tr v-if="group.rows.length === 0" class="border-b border-border">
                <td colspan="5" class="px-4 py-3 text-center text-[0.82rem] text-text-muted italic">No Records Found</td>
              </tr>
              <!-- Data rows -->
              <tr v-for="row in group.rows" :key="row.domainName"
                class="border-b border-border last:border-0 hover:bg-white/[0.02] transition-colors"
                :class="!row.isActive ? 'opacity-50' : ''">
                <td class="px-4 py-2.5 text-text-primary font-medium">{{ row.domainName }}</td>
                <td class="px-4 py-2.5 text-center">
                  <!-- SSL valid -->
                  <span v-if="row.hasSsl" class="inline-flex items-center justify-center w-6 h-6">
                    <svg viewBox="0 0 24 24" class="w-5 h-5 text-status-green" fill="none" stroke="currentColor" stroke-width="2">
                      <rect x="5" y="11" width="14" height="10" rx="2" fill="currentColor" stroke="none" opacity="0.15"/>
                      <rect x="5" y="11" width="14" height="10" rx="2"/>
                      <path d="M8 11V7a4 4 0 0 1 8 0v4"/>
                      <path d="M12 15v2" stroke-linecap="round"/>
                      <circle cx="12" cy="15" r="1" fill="currentColor"/>
                    </svg>
                  </span>
                  <!-- No SSL -->
                  <span v-else class="inline-flex items-center justify-center w-6 h-6">
                    <svg viewBox="0 0 24 24" class="w-5 h-5 text-status-red" fill="none" stroke="currentColor" stroke-width="2">
                      <rect x="5" y="11" width="14" height="10" rx="2" fill="currentColor" stroke="none" opacity="0.15"/>
                      <rect x="5" y="11" width="14" height="10" rx="2"/>
                      <path d="M8 11V7a4 4 0 0 1 8 0v4"/>
                      <line x1="9" y1="15" x2="15" y2="19" stroke-linecap="round"/>
                      <line x1="15" y1="15" x2="9" y2="19" stroke-linecap="round"/>
                    </svg>
                  </span>
                </td>
                <td class="px-4 py-2.5 text-text-secondary">{{ row.issuer ?? '—' }}</td>
                <td class="px-4 py-2.5 text-center font-mono text-text-secondary">{{ row.expiresAt ?? '—' }}</td>
                <td class="px-4 py-2.5 text-right text-text-muted text-[0.78rem]">{{ row.lastUpdate }}</td>
              </tr>
            </template>
          </tbody>
        </table>
      </div>
    </template>
    <ReportTimestamp />
  </ReportPage>
</template>
