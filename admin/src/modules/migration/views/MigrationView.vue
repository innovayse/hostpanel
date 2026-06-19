<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { useMigrationStore } from '../stores/migrationStore'
import MigrationProgressCard from '../components/MigrationProgressCard.vue'
import MigrationReportTable from '../components/MigrationReportTable.vue'
import AppCheckbox from '../../../components/AppCheckbox.vue'
import type { MigrationJob } from '../types/migration.types'

const store = useMigrationStore()

const showNewJobModal = ref(false)
const newJobLabel = ref('')
const newJobSourceUrl = ref('')
const creatingJob = ref(false)
const copiedKey = ref(false)
const historyExpanded = ref(false)

const exportClients       = ref(true)
const exportInvoices      = ref(true)
const exportServices      = ref(true)
const exportDomains       = ref(true)
const exportTickets       = ref(true)
const exportProducts      = ref(true)
const exportOrders        = ref(true)
const exportTransactions  = ref(true)
const exportQuotes        = ref(true)
const exportKnowledgebase = ref(true)
const exportContacts       = ref(true)
const exportTicketReplies  = ref(true)
const exportAnnouncements  = ref(true)
const exportDownloads      = ref(true)
const exportNetworkIssues  = ref(true)

const allExportRefs = [
  exportClients, exportInvoices, exportServices, exportDomains, exportTickets,
  exportProducts, exportOrders, exportTransactions, exportQuotes, exportKnowledgebase,
  exportContacts, exportTicketReplies, exportAnnouncements, exportDownloads, exportNetworkIssues,
]
const allSelected = computed(() => allExportRefs.every(r => r.value))
function toggleAll() {
  const val = !allSelected.value
  allExportRefs.forEach(r => { r.value = val })
}

onMounted(store.fetchAll)
onUnmounted(store.stopPolling)

async function handleCreateJob() {
  creatingJob.value = true
  try {
    const job = await store.createJob({
      label:               newJobLabel.value || undefined,
      sourceUrl:           newJobSourceUrl.value,
      exportClients:       exportClients.value,
      exportInvoices:      exportInvoices.value,
      exportServices:      exportServices.value,
      exportDomains:       exportDomains.value,
      exportTickets:       exportTickets.value,
      exportProducts:      exportProducts.value,
      exportOrders:        exportOrders.value,
      exportTransactions:  exportTransactions.value,
      exportQuotes:        exportQuotes.value,
      exportKnowledgebase: exportKnowledgebase.value,
      exportContacts:       exportContacts.value,
      exportTicketReplies:  exportTicketReplies.value,
      exportAnnouncements:  exportAnnouncements.value,
      exportDownloads:      exportDownloads.value,
      exportNetworkIssues:  exportNetworkIssues.value,
    })
    store.setActiveJob(job)
    store.startPolling(job.id)
    newJobLabel.value = ''
    newJobSourceUrl.value = ''
    showNewJobModal.value = false
  } finally {
    creatingJob.value = false
  }
}

function selectJob(job: MigrationJob) {
  store.setActiveJob(job)
  if (job.status === 'Pending' || job.status === 'InProgress') {
    store.startPolling(job.id)
  }
}

async function copyKey(key: string) {
  await navigator.clipboard.writeText(key)
  copiedKey.value = true
  setTimeout(() => { copiedKey.value = false }, 2000)
}

const deletingId = ref<number | null>(null)

async function handleDelete(id: number, event: Event) {
  event.stopPropagation()
  deletingId.value = id
  try {
    await store.deleteJob(id)
  } finally {
    deletingId.value = null
  }
}

const PLUGIN_DOWNLOAD_URL = '/api/migrations/plugin/download'

function statusColor(status: string) {
  return {
    Pending:    'text-amber-400 bg-amber-400/10 border-amber-400/20',
    InProgress: 'text-primary-400 bg-primary-500/10 border-primary-500/20',
    Completed:  'text-status-green bg-status-green/10 border-status-green/20',
    Failed:     'text-status-red bg-status-red/10 border-status-red/20',
  }[status] ?? 'text-text-muted bg-white/[0.04] border-border'
}

function statusLabel(status: string) {
  return status === 'InProgress' ? 'Running' : status
}
</script>

<template>
  <div class="flex flex-col h-full w-full">

    <!-- Page Header -->
    <div class="flex items-center justify-between px-4 sm:px-6 py-3 border-b border-border shrink-0">
      <div class="flex items-center gap-3">
        <div class="w-8 h-8 rounded-[9px] bg-primary-500/10 border border-primary-500/20 flex items-center justify-center">
          <svg class="w-4 h-4 text-primary-400" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
            <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="17 8 12 3 7 8"/><line x1="12" y1="3" x2="12" y2="15"/>
          </svg>
        </div>
        <div>
          <h1 class="font-display font-bold text-[1.25rem] text-text-primary tracking-tight leading-none">Migration Tools</h1>
          <p class="text-[0.72rem] text-text-muted mt-0.5">Import existing client data into this panel</p>
        </div>
      </div>
      <button
        class="flex items-center gap-2 px-4 py-2 rounded-xl text-[0.82rem] font-semibold gradient-brand text-white hover:opacity-90 transition-all shadow-lg shadow-primary-500/20"
        @click="showNewJobModal = true"
      >
        <svg class="w-4 h-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round">
          <line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/>
        </svg>
        New Migration
      </button>
    </div>

    <!-- Loading -->
    <div v-if="store.loading" class="flex items-center gap-3 text-text-secondary text-sm py-20 justify-center">
      <span class="w-5 h-5 rounded-full border-2 border-primary-500/20 border-t-primary-500 animate-spin" />
      <span class="text-[0.82rem]">Loading migrations…</span>
    </div>

    <!-- Body: sidebar + content (same pattern as ClientLayout) -->
    <div v-else class="flex flex-1 min-h-0">

      <!-- LEFT: History Sidebar (hover to expand) -->
      <div
        class="flex flex-col bg-surface-card border-r border-border transition-all duration-200 ease-in-out overflow-hidden shrink-0"
        :class="historyExpanded ? 'w-[260px]' : 'w-[48px]'"
        @mouseenter="historyExpanded = true"
        @mouseleave="historyExpanded = false"
      >
        <!-- Header -->
        <div class="flex items-center gap-3 px-3 py-[13px] border-b border-border shrink-0">
          <svg class="w-5 h-5 shrink-0 text-text-muted" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
            <circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/>
          </svg>
          <div
            class="flex items-center justify-between flex-1 transition-opacity duration-200 whitespace-nowrap overflow-hidden"
            :class="historyExpanded ? 'opacity-100' : 'opacity-0'"
          >
            <span class="text-[0.68rem] font-semibold uppercase tracking-widest text-text-muted">History</span>
            <span v-if="store.jobs.length > 0" class="text-[0.68rem] font-semibold text-text-muted">{{ store.jobs.length }}</span>
          </div>
        </div>

        <!-- Empty -->
        <div v-if="store.jobs.length === 0" class="flex flex-col items-center py-10 px-3 gap-2">
          <svg class="w-5 h-5 text-text-muted shrink-0" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
            <circle cx="12" cy="12" r="10"/><line x1="12" y1="8" x2="12" y2="12"/><line x1="12" y1="16" x2="12.01" y2="16"/>
          </svg>
          <p
            class="text-[0.72rem] text-text-muted text-center whitespace-nowrap transition-opacity duration-200"
            :class="historyExpanded ? 'opacity-100' : 'opacity-0'"
          >No migrations yet</p>
        </div>

        <!-- Job rows -->
        <div class="flex flex-col overflow-y-auto flex-1">
          <button
            v-for="job in store.jobs"
            :key="job.id"
            class="group relative flex items-center gap-3 px-3 py-3 transition-all duration-150 text-left border-b border-border/40 last:border-0"
            :class="store.activeJob?.id === job.id
              ? 'bg-primary-500/8 text-primary-400'
              : 'text-text-muted hover:text-text-secondary hover:bg-white/[0.04]'"
            @click="selectJob(job)"
          >
            <!-- Active bar -->
            <span
              v-if="store.activeJob?.id === job.id"
              class="absolute left-0 top-1/2 -translate-y-1/2 w-[3px] h-[18px] rounded-r-full gradient-brand"
            />

            <!-- Status icon -->
            <div
              class="w-6 h-6 rounded-lg flex items-center justify-center shrink-0 transition-all duration-150"
              :class="{
                'bg-status-green/15 text-status-green':   job.status === 'Completed',
                'bg-status-red/15 text-status-red':       job.status === 'Failed',
                'bg-primary-500/15 text-primary-400':     job.status === 'InProgress',
                'bg-amber-400/15 text-amber-400':         job.status === 'Pending',
              }"
            >
              <!-- Completed -->
              <svg v-if="job.status === 'Completed'" class="w-3.5 h-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round">
                <path d="M20 6L9 17l-5-5"/>
              </svg>
              <!-- Failed -->
              <svg v-else-if="job.status === 'Failed'" class="w-3.5 h-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round">
                <path d="M18 6L6 18M6 6l12 12"/>
              </svg>
              <!-- InProgress -->
              <svg v-else-if="job.status === 'InProgress'" class="w-3.5 h-3.5 animate-spin" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <path d="M21 12a9 9 0 1 1-6.219-8.56"/>
              </svg>
              <!-- Pending -->
              <svg v-else class="w-3.5 h-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/>
              </svg>
            </div>

            <!-- Expanded content -->
            <div
              class="flex-1 min-w-0 transition-opacity duration-200"
              :class="historyExpanded ? 'opacity-100' : 'opacity-0 pointer-events-none'"
            >
              <p class="text-[0.78rem] font-medium truncate whitespace-nowrap leading-none mb-1">{{ job.label ?? 'Migration' }}</p>
              <div class="flex items-center justify-between gap-2">
                <p class="text-[0.65rem] text-text-muted">#{{ job.id }} · {{ job.overallPercent }}%</p>
                <span
                  class="text-[0.6rem] font-semibold rounded-full px-1.5 py-px border whitespace-nowrap"
                  :class="statusColor(job.status)"
                >{{ statusLabel(job.status) }}</span>
              </div>
              <div class="w-full h-1 bg-white/[0.06] rounded-full overflow-hidden mt-1.5">
                <div
                  class="h-full rounded-full transition-all duration-700"
                  :class="{
                    'bg-status-green': job.status === 'Completed',
                    'bg-status-red':   job.status === 'Failed',
                    'gradient-brand':  job.status === 'InProgress' || job.status === 'Pending',
                  }"
                  :style="{ width: `${job.overallPercent}%` }"
                />
              </div>
            </div>

            <!-- Delete -->
            <button
              v-if="historyExpanded"
              class="shrink-0 w-6 h-6 flex items-center justify-center rounded-lg opacity-0 group-hover:opacity-100 hover:bg-status-red/10 text-text-muted hover:text-status-red transition-all disabled:opacity-40"
              :disabled="deletingId === job.id"
              @click="handleDelete(job.id, $event)"
            >
              <svg class="w-3 h-3" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <polyline points="3 6 5 6 21 6"/><path d="M19 6l-1 14a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2L5 6"/><path d="M10 11v6M14 11v6"/>
              </svg>
            </button>
          </button>
        </div>
      </div>

      <!-- MAIN -->
      <div class="flex-1 min-w-0 overflow-y-auto">
      <div class="flex flex-col gap-4 p-4 sm:p-6">

        <!-- Empty state -->
        <div
          v-if="!store.activeJob"
          class="bg-surface-card border border-border rounded-2xl flex flex-col items-center justify-center py-24 gap-4 relative overflow-hidden"
        >
          <div class="absolute inset-0 pointer-events-none" style="background: radial-gradient(circle at 50% 60%, rgba(14,165,233,0.04), transparent 70%)" />
          <div class="w-16 h-16 rounded-2xl bg-white/[0.04] border border-border flex items-center justify-center">
            <svg class="w-8 h-8 text-text-muted" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.2" stroke-linecap="round" stroke-linejoin="round">
              <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="17 8 12 3 7 8"/><line x1="12" y1="3" x2="12" y2="15"/>
            </svg>
          </div>
          <div class="text-center">
            <p class="text-[0.95rem] font-semibold text-text-secondary mb-1">No migration selected</p>
            <p class="text-[0.78rem] text-text-muted">Create a new migration or select one from the history</p>
          </div>
          <button
            class="mt-1 px-5 py-2.5 rounded-xl text-[0.82rem] font-semibold gradient-brand text-white hover:opacity-90 transition-all shadow-lg shadow-primary-500/20"
            @click="showNewJobModal = true"
          >
            Start your first migration
          </button>
        </div>

        <!-- Progress card -->
        <MigrationProgressCard v-if="store.activeJob" :job="store.activeJob" />

        <!-- Setup Instructions (only for Pending) -->
        <div
          v-if="store.activeJob?.status === 'Pending'"
          class="bg-surface-card border border-border rounded-2xl p-6"
        >
          <div class="flex items-center gap-2.5 mb-5">
            <div class="w-7 h-7 rounded-lg bg-amber-400/10 border border-amber-400/20 flex items-center justify-center">
              <svg class="w-3.5 h-3.5 text-amber-400" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <circle cx="12" cy="12" r="10"/><line x1="12" y1="8" x2="12" y2="12"/><line x1="12" y1="16" x2="12.01" y2="16"/>
              </svg>
            </div>
            <h3 class="text-[0.88rem] font-semibold text-text-primary">Setup Required</h3>
          </div>

          <div class="flex flex-col gap-5">
            <!-- Step 1 -->
            <div class="flex gap-4">
              <div class="flex flex-col items-center gap-1 shrink-0">
                <span class="w-7 h-7 rounded-full gradient-brand text-white text-[0.7rem] font-bold flex items-center justify-center">1</span>
                <div class="w-px flex-1 bg-border min-h-[20px]" />
              </div>
              <div class="pb-2">
                <p class="text-[0.82rem] font-semibold text-text-primary mb-0.5">Download the migration plugin</p>
                <a :href="PLUGIN_DOWNLOAD_URL" class="inline-flex items-center gap-1.5 text-[0.78rem] text-primary-400 hover:text-primary-300 transition-colors">
                  <svg class="w-3 h-3" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                    <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="7 10 12 15 17 10"/><line x1="12" y1="15" x2="12" y2="3"/>
                  </svg>
                  innovayse-migration.zip
                </a>
              </div>
            </div>

            <!-- Step 2 -->
            <div class="flex gap-4">
              <div class="flex flex-col items-center gap-1 shrink-0">
                <span class="w-7 h-7 rounded-full gradient-brand text-white text-[0.7rem] font-bold flex items-center justify-center">2</span>
                <div class="w-px flex-1 bg-border min-h-[20px]" />
              </div>
              <div class="pb-2">
                <p class="text-[0.82rem] font-semibold text-text-primary mb-0.5">Install on your source server</p>
                <p class="text-[0.78rem] text-text-muted">Extract to your source system's root. Files go into <code class="font-mono text-[0.75rem] text-text-secondary bg-white/[0.06] px-1.5 py-0.5 rounded-md">/modules/addons/innovayse_migration/</code> then activate in Admin → Addons.</p>
              </div>
            </div>

            <!-- Step 3 -->
            <div class="flex gap-4">
              <div class="flex flex-col items-center gap-1 shrink-0">
                <span class="w-7 h-7 rounded-full gradient-brand text-white text-[0.7rem] font-bold flex items-center justify-center">3</span>
                <div class="w-px flex-1 bg-border min-h-[20px]" />
              </div>
              <div class="flex-1 pb-2">
                <p class="text-[0.82rem] font-semibold text-text-primary mb-2">Copy the secret key into plugin settings</p>
                <div class="flex items-center gap-2 bg-white/[0.04] border border-border rounded-xl px-3 py-2.5">
                  <code class="flex-1 font-mono text-[0.75rem] text-text-secondary truncate">{{ store.activeJob.key }}</code>
                  <button
                    class="shrink-0 flex items-center gap-1.5 text-[0.72rem] font-semibold px-2.5 py-1 rounded-lg transition-all"
                    :class="copiedKey ? 'text-status-green bg-status-green/10' : 'text-primary-400 hover:bg-primary-500/10'"
                    @click="copyKey(store.activeJob!.key)"
                  >
                    <svg v-if="copiedKey" class="w-3 h-3" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><path d="M20 6L9 17l-5-5"/></svg>
                    <svg v-else class="w-3 h-3" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="9" y="9" width="13" height="13" rx="2"/><path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"/></svg>
                    {{ copiedKey ? 'Copied!' : 'Copy' }}
                  </button>
                </div>
              </div>
            </div>

            <!-- Step 4 -->
            <div class="flex gap-4">
              <div class="flex flex-col items-center gap-1 shrink-0">
                <span class="w-7 h-7 rounded-full gradient-brand text-white text-[0.7rem] font-bold flex items-center justify-center">4</span>
              </div>
              <div>
                <p class="text-[0.82rem] font-semibold text-text-primary mb-0.5">Test connection and start import</p>
                <p class="text-[0.78rem] text-text-muted">Click <strong class="text-text-secondary font-semibold">Check</strong> to verify connectivity, then <strong class="text-text-secondary font-semibold">Start Import</strong> to begin.</p>
              </div>
            </div>
          </div>
        </div>

        <!-- Import Report -->
        <div v-if="store.activeJob && (store.activeJob.status === 'Completed' || store.activeJob.status === 'Failed')">
          <MigrationReportTable :job-id="store.activeJob.id" />
        </div>
      </div>
      </div>
    </div>

    <!-- New Job Modal -->
    <Teleport to="body">
      <Transition
        enter-active-class="transition duration-200 ease-out"
        enter-from-class="opacity-0"
        enter-to-class="opacity-100"
        leave-active-class="transition duration-150 ease-in"
        leave-from-class="opacity-100"
        leave-to-class="opacity-0"
      >
        <div
          v-if="showNewJobModal"
          class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/70 backdrop-blur-sm"
          @click.self="showNewJobModal = false"
        >
          <Transition
            enter-active-class="transition duration-200 ease-out"
            enter-from-class="opacity-0 scale-95 translate-y-2"
            enter-to-class="opacity-100 scale-100 translate-y-0"
          >
            <div v-if="showNewJobModal" class="bg-surface-card border border-border rounded-2xl p-6 w-full max-w-lg shadow-2xl">

              <!-- Modal header -->
              <div class="flex items-center gap-3 mb-5">
                <div class="w-9 h-9 rounded-xl gradient-brand flex items-center justify-center shrink-0">
                  <svg class="w-4.5 h-4.5 text-white" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                    <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="17 8 12 3 7 8"/><line x1="12" y1="3" x2="12" y2="15"/>
                  </svg>
                </div>
                <div>
                  <h2 class="text-[1rem] font-semibold text-text-primary leading-none">New Migration</h2>
                  <p class="text-[0.72rem] text-text-muted mt-0.5">A secret key will be generated automatically</p>
                </div>
              </div>

              <!-- Source URL -->
              <div class="mb-4">
                <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">
                  Source URL <span class="text-status-red">*</span>
                </label>
                <input
                  v-model="newJobSourceUrl"
                  type="url"
                  placeholder="https://your-source.com"
                  class="w-full bg-white/[0.04] border border-border rounded-xl px-3 py-2.5 text-[0.82rem] text-text-primary placeholder-text-muted outline-none focus:border-primary-500/60 transition"
                />
                <p class="mt-1 text-[0.65rem] text-text-muted">/modules/addons/innovayse_migration/api.php will be appended automatically</p>
              </div>

              <!-- Label -->
              <div class="mb-5">
                <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Label <span class="normal-case tracking-normal font-normal">(optional)</span></label>
                <input
                  v-model="newJobLabel"
                  type="text"
                  placeholder="e.g. Production Server"
                  class="w-full bg-white/[0.04] border border-border rounded-xl px-3 py-2.5 text-[0.82rem] text-text-primary placeholder-text-muted outline-none focus:border-primary-500/60 transition"
                  @keydown.enter="handleCreateJob"
                />
              </div>

              <!-- Data to import -->
              <div class="mb-5">
                <div class="flex items-center justify-between mb-3">
                  <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Data to import</label>
                  <button type="button" @click="toggleAll" class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-primary hover:text-primary/80 transition-colors">
                    {{ allSelected ? 'Clear All' : 'Select All' }}
                  </button>
                </div>
                <div class="grid grid-cols-2 gap-x-6 gap-y-2.5">
                  <label class="flex items-center gap-2.5 cursor-pointer select-none group"><AppCheckbox v-model="exportClients" /><span class="text-[0.78rem] text-text-secondary group-hover:text-text-primary transition-colors">Clients</span></label>
                  <label class="flex items-center gap-2.5 cursor-pointer select-none group"><AppCheckbox v-model="exportInvoices" /><span class="text-[0.78rem] text-text-secondary group-hover:text-text-primary transition-colors">Invoices</span></label>
                  <label class="flex items-center gap-2.5 cursor-pointer select-none group"><AppCheckbox v-model="exportServices" /><span class="text-[0.78rem] text-text-secondary group-hover:text-text-primary transition-colors">Hosting services</span></label>
                  <label class="flex items-center gap-2.5 cursor-pointer select-none group"><AppCheckbox v-model="exportDomains" /><span class="text-[0.78rem] text-text-secondary group-hover:text-text-primary transition-colors">Domains</span></label>
                  <label class="flex items-center gap-2.5 cursor-pointer select-none group"><AppCheckbox v-model="exportTickets" /><span class="text-[0.78rem] text-text-secondary group-hover:text-text-primary transition-colors">Support tickets</span></label>
                  <label class="flex items-center gap-2.5 cursor-pointer select-none group"><AppCheckbox v-model="exportProducts" /><span class="text-[0.78rem] text-text-secondary group-hover:text-text-primary transition-colors">Products</span></label>
                  <label class="flex items-center gap-2.5 cursor-pointer select-none group"><AppCheckbox v-model="exportOrders" /><span class="text-[0.78rem] text-text-secondary group-hover:text-text-primary transition-colors">Orders</span></label>
                  <label class="flex items-center gap-2.5 cursor-pointer select-none group"><AppCheckbox v-model="exportTransactions" /><span class="text-[0.78rem] text-text-secondary group-hover:text-text-primary transition-colors">Transactions</span></label>
                  <label class="flex items-center gap-2.5 cursor-pointer select-none group"><AppCheckbox v-model="exportQuotes" /><span class="text-[0.78rem] text-text-secondary group-hover:text-text-primary transition-colors">Quotes</span></label>
                  <label class="flex items-center gap-2.5 cursor-pointer select-none group"><AppCheckbox v-model="exportKnowledgebase" /><span class="text-[0.78rem] text-text-secondary group-hover:text-text-primary transition-colors">Knowledgebase</span></label>
                  <label class="flex items-center gap-2.5 cursor-pointer select-none group"><AppCheckbox v-model="exportContacts" /><span class="text-[0.78rem] text-text-secondary group-hover:text-text-primary transition-colors">Contacts</span></label>
                  <label class="flex items-center gap-2.5 cursor-pointer select-none group"><AppCheckbox v-model="exportTicketReplies" /><span class="text-[0.78rem] text-text-secondary group-hover:text-text-primary transition-colors">Ticket replies</span></label>
                  <label class="flex items-center gap-2.5 cursor-pointer select-none group"><AppCheckbox v-model="exportAnnouncements" /><span class="text-[0.78rem] text-text-secondary group-hover:text-text-primary transition-colors">Announcements</span></label>
                  <label class="flex items-center gap-2.5 cursor-pointer select-none group"><AppCheckbox v-model="exportDownloads" /><span class="text-[0.78rem] text-text-secondary group-hover:text-text-primary transition-colors">Downloads</span></label>
                  <label class="flex items-center gap-2.5 cursor-pointer select-none group"><AppCheckbox v-model="exportNetworkIssues" /><span class="text-[0.78rem] text-text-secondary group-hover:text-text-primary transition-colors">Network Issues</span></label>
                </div>
              </div>

              <!-- Actions -->
              <div class="flex gap-3 justify-end pt-2 border-t border-border">
                <button
                  class="px-4 py-2 rounded-xl text-[0.82rem] font-medium text-text-muted hover:text-text-secondary hover:bg-white/[0.04] transition-all"
                  @click="showNewJobModal = false"
                >
                  Cancel
                </button>
                <button
                  class="px-5 py-2 rounded-xl text-[0.82rem] font-semibold gradient-brand text-white hover:opacity-90 transition-all disabled:opacity-40 shadow-lg shadow-primary-500/20"
                  :disabled="creatingJob || !newJobSourceUrl.trim()"
                  @click="handleCreateJob"
                >
                  <span v-if="creatingJob" class="flex items-center gap-2">
                    <span class="w-3.5 h-3.5 rounded-full border-2 border-white/30 border-t-white animate-spin" />
                    Creating…
                  </span>
                  <span v-else>Create Migration</span>
                </button>
              </div>
            </div>
          </Transition>
        </div>
      </Transition>
    </Teleport>

  </div>
</template>
