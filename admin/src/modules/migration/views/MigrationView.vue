<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue'
import { useMigrationStore } from '../stores/migrationStore'
import MigrationProgressCard from '../components/MigrationProgressCard.vue'
import AppCheckbox from '../../../components/AppCheckbox.vue'
import type { MigrationJob } from '../types/migration.types'

const store = useMigrationStore()

const showNewJobModal = ref(false)
const newJobLabel = ref('')
const newJobSourceUrl = ref('')
const creatingJob = ref(false)
const copiedKey = ref(false)

const exportClients  = ref(true)
const exportInvoices = ref(true)
const exportServices = ref(true)
const exportDomains  = ref(true)
const exportTickets  = ref(true)

onMounted(store.fetchAll)
onUnmounted(store.stopPolling)

async function handleCreateJob() {
  creatingJob.value = true
  try {
    const job = await store.createJob({
      label:          newJobLabel.value || undefined,
      sourceUrl:      newJobSourceUrl.value,
      exportClients:  exportClients.value,
      exportInvoices: exportInvoices.value,
      exportServices: exportServices.value,
      exportDomains:  exportDomains.value,
      exportTickets:  exportTickets.value,
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
</script>

<template>
  <div class="p-4 sm:p-6 lg:p-8 w-full">

    <!-- Header -->
    <div class="flex items-center justify-between mb-6">
      <div>
        <h1 class="font-display font-bold text-[1.25rem] text-text-primary leading-none">Migration Tools</h1>
        <p class="text-[0.78rem] text-text-secondary mt-1">Import your existing client data into this panel</p>
      </div>
      <button
        class="flex items-center gap-2 px-4 py-2 rounded-xl text-[0.82rem] font-semibold gradient-brand text-white hover:opacity-90 transition-all"
        @click="showNewJobModal = true"
      >
        <svg class="w-4 h-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/>
        </svg>
        New Migration
      </button>
    </div>

    <!-- Loading -->
    <div v-if="store.loading" class="flex items-center gap-3 text-text-secondary text-sm">
      <span class="w-4 h-4 rounded-full border-2 border-primary-500/20 border-t-primary-500 animate-spin" />
      Loading…
    </div>

    <!-- Main content -->
    <div v-else class="grid grid-cols-1 lg:grid-cols-3 gap-6">

      <!-- Left: Active job progress -->
      <div class="lg:col-span-2">
        <MigrationProgressCard
          v-if="store.activeJob"
          :job="store.activeJob"
        />

        <!-- Key + instructions (show only for Pending jobs) -->
        <div
          v-if="store.activeJob?.status === 'Pending'"
          class="mt-4 bg-surface-card border border-border rounded-2xl p-6"
        >
          <h3 class="text-[0.9rem] font-semibold text-text-primary mb-4">Setup Instructions</h3>

          <ol class="flex flex-col gap-4">
            <!-- Step 1 -->
            <li class="flex gap-3">
              <span class="w-6 h-6 rounded-full gradient-brand text-white text-[0.7rem] font-bold flex items-center justify-center shrink-0 mt-0.5">1</span>
              <div>
                <p class="text-[0.82rem] font-medium text-text-primary">Download the migration plugin</p>
                <a
                  :href="PLUGIN_DOWNLOAD_URL"
                  class="text-[0.78rem] text-primary-400 hover:text-primary-300 transition-colors"
                >
                  Download innovayse-migration.zip →
                </a>
              </div>
            </li>

            <!-- Step 2 -->
            <li class="flex gap-3">
              <span class="w-6 h-6 rounded-full gradient-brand text-white text-[0.7rem] font-bold flex items-center justify-center shrink-0 mt-0.5">2</span>
              <div>
                <p class="text-[0.82rem] font-medium text-text-primary">Install the plugin on your source server</p>
                <p class="text-[0.78rem] text-text-muted">Extract the ZIP to your source system's root — files go into <span class="font-mono text-text-secondary">/modules/addons/innovayse_migration/</span>. Then activate it in Admin → Addons.</p>
              </div>
            </li>

            <!-- Step 3 -->
            <li class="flex gap-3">
              <span class="w-6 h-6 rounded-full gradient-brand text-white text-[0.7rem] font-bold flex items-center justify-center shrink-0 mt-0.5">3</span>
              <div class="flex-1">
                <p class="text-[0.82rem] font-medium text-text-primary mb-2">Copy the secret key into the plugin settings</p>
                <div class="flex items-center gap-2 bg-white/[0.04] border border-border rounded-xl px-3 py-2">
                  <span class="flex-1 font-mono text-[0.78rem] text-text-secondary truncate">{{ store.activeJob.key }}</span>
                  <button
                    class="shrink-0 text-[0.75rem] font-medium px-2.5 py-1 rounded-lg transition-all"
                    :class="copiedKey ? 'text-status-green bg-status-green/10' : 'text-primary-400 hover:bg-primary-500/10'"
                    @click="copyKey(store.activeJob!.key)"
                  >
                    {{ copiedKey ? 'Copied!' : 'Copy' }}
                  </button>
                </div>
                <p class="text-[0.72rem] text-text-muted mt-1.5">Paste this key in the plugin's "Migration Key" field. The plugin acts as an API — this panel will pull data from it.</p>
              </div>
            </li>

            <!-- Step 4 -->
            <li class="flex gap-3">
              <span class="w-6 h-6 rounded-full gradient-brand text-white text-[0.7rem] font-bold flex items-center justify-center shrink-0 mt-0.5">4</span>
              <div>
                <p class="text-[0.82rem] font-medium text-text-primary">Test the connection, then start the import</p>
                <p class="text-[0.78rem] text-text-muted">Click <strong class="text-text-secondary">Check</strong> to verify this panel can reach the plugin. Once connected, click <strong class="text-text-secondary">Start Import</strong>.</p>
              </div>
            </li>
          </ol>
        </div>

        <!-- Empty state -->
        <div
          v-if="!store.activeJob"
          class="bg-surface-card border border-border rounded-2xl flex flex-col items-center justify-center py-20 gap-3"
        >
          <div class="w-14 h-14 rounded-2xl bg-white/[0.04] border border-border flex items-center justify-center">
            <svg class="w-7 h-7 text-text-muted" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
              <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="17 8 12 3 7 8"/><line x1="12" y1="3" x2="12" y2="15"/>
            </svg>
          </div>
          <p class="text-[0.875rem] font-medium text-text-secondary">No migration selected</p>
          <p class="text-[0.78rem] text-text-muted">Create a new migration or select one from the list</p>
          <button
            class="mt-2 px-4 py-2 rounded-xl text-[0.82rem] font-semibold gradient-brand text-white hover:opacity-90 transition-all"
            @click="showNewJobModal = true"
          >
            Start Migration
          </button>
        </div>
      </div>

      <!-- Right: Jobs list -->
      <div class="flex flex-col gap-2">
        <p class="text-[0.72rem] font-semibold text-text-muted uppercase tracking-widest mb-1">Migration History</p>

        <div
          v-if="store.jobs.length === 0"
          class="bg-surface-card border border-border rounded-2xl p-6 text-center"
        >
          <p class="text-[0.82rem] text-text-muted">No migrations yet</p>
        </div>

        <button
          v-for="job in store.jobs"
          :key="job.id"
          class="w-full text-left p-4 bg-surface-card border rounded-2xl transition-all duration-150 hover:border-primary-500/40"
          :class="store.activeJob?.id === job.id ? 'border-primary-500/40 bg-primary-500/5' : 'border-border'"
          @click="selectJob(job)"
        >
          <div class="flex items-center justify-between mb-1">
            <span class="text-[0.82rem] font-medium text-text-primary truncate">
              {{ job.label ?? 'Migration' }}
            </span>
            <div class="flex items-center gap-1.5 ml-2 shrink-0">
              <span
                class="text-[0.6rem] font-semibold rounded-full px-2 py-0.5 border"
                :class="{
                  'text-amber-400 bg-amber-400/10 border-amber-400/20': job.status === 'Pending',
                  'text-primary-400 bg-primary-500/10 border-primary-500/20': job.status === 'InProgress',
                  'text-status-green bg-status-green/10 border-status-green/20': job.status === 'Completed',
                  'text-status-red bg-status-red/8 border-status-red/20': job.status === 'Failed',
                }"
              >
                {{ job.status === 'InProgress' ? 'Running' : job.status }}
              </span>
              <button
                class="w-5 h-5 flex items-center justify-center rounded-lg hover:bg-status-red/10 text-text-muted hover:text-status-red transition-all disabled:opacity-40"
                :disabled="deletingId === job.id"
                @click="handleDelete(job.id, $event)"
              >
                <svg class="w-3 h-3" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                  <polyline points="3 6 5 6 21 6"/><path d="M19 6l-1 14a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2L5 6"/><path d="M10 11v6M14 11v6"/><path d="M9 6V4h6v2"/>
                </svg>
              </button>
            </div>
          </div>

          <!-- Mini progress bar -->
          <div class="w-full h-1 bg-white/[0.06] rounded-full overflow-hidden mt-2">
            <div
              class="h-full rounded-full transition-all duration-500"
              :class="job.status === 'Completed' ? 'bg-status-green' : job.status === 'Failed' ? 'bg-status-red' : 'gradient-brand'"
              :style="{ width: `${job.overallPercent}%` }"
            />
          </div>
          <p class="text-[0.7rem] text-text-muted mt-1">{{ job.overallPercent }}% complete</p>
        </button>
      </div>
    </div>

    <!-- New Job Modal -->
    <Teleport to="body">
      <div
        v-if="showNewJobModal"
        class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm"
        @click.self="showNewJobModal = false"
      >
        <div class="bg-surface-card border border-border rounded-2xl p-6 w-full max-w-md shadow-2xl">
          <h2 class="text-[1rem] font-semibold text-text-primary mb-1">New Migration</h2>
          <p class="text-[0.78rem] text-text-muted mb-5">A secret key will be generated for the migration plugin.</p>

          <label class="block text-[0.78rem] font-medium text-text-secondary mb-1.5">Source URL <span class="text-status-red">*</span></label>
          <input
            v-model="newJobSourceUrl"
            type="url"
            placeholder="https://your-whmcs.com/modules/addons/innovayse_migration/api.php"
            class="w-full bg-white/[0.04] border border-border rounded-xl px-3 py-2.5 text-[0.88rem] text-text-primary placeholder-text-muted outline-none focus:border-primary-500/60 transition mb-4"
          />

          <label class="block text-[0.78rem] font-medium text-text-secondary mb-1.5">Label (optional)</label>
          <input
            v-model="newJobLabel"
            type="text"
            placeholder="e.g. Production Server"
            class="w-full bg-white/[0.04] border border-border rounded-xl px-3 py-2.5 text-[0.88rem] text-text-primary placeholder-text-muted outline-none focus:border-primary-500/60 transition mb-5"
            @keydown.enter="handleCreateJob"
          />

          <p class="text-[0.78rem] font-medium text-text-secondary mb-2">Data to export</p>
          <div class="flex flex-col gap-2 mb-5">
            <label class="flex items-center gap-3 cursor-pointer select-none">
              <AppCheckbox v-model="exportClients" />
              <span class="text-[0.82rem] text-text-secondary">Clients</span>
            </label>
            <label class="flex items-center gap-3 cursor-pointer select-none">
              <AppCheckbox v-model="exportInvoices" />
              <span class="text-[0.82rem] text-text-secondary">Invoices & line items</span>
            </label>
            <label class="flex items-center gap-3 cursor-pointer select-none">
              <AppCheckbox v-model="exportServices" />
              <span class="text-[0.82rem] text-text-secondary">Hosting services</span>
            </label>
            <label class="flex items-center gap-3 cursor-pointer select-none">
              <AppCheckbox v-model="exportDomains" />
              <span class="text-[0.82rem] text-text-secondary">Domain registrations</span>
            </label>
            <label class="flex items-center gap-3 cursor-pointer select-none">
              <AppCheckbox v-model="exportTickets" />
              <span class="text-[0.82rem] text-text-secondary">Support tickets</span>
            </label>
          </div>

          <div class="flex gap-3 justify-end">
            <button
              class="px-4 py-2 rounded-xl text-[0.82rem] font-medium text-text-muted hover:text-text-secondary hover:bg-white/[0.04] transition-all"
              @click="showNewJobModal = false"
            >
              Cancel
            </button>
            <button
              class="px-5 py-2 rounded-xl text-[0.82rem] font-semibold gradient-brand text-white hover:opacity-90 transition-all disabled:opacity-50"
              :disabled="creatingJob || !newJobSourceUrl.trim()"
              @click="handleCreateJob"
            >
              {{ creatingJob ? 'Creating…' : 'Create Migration' }}
            </button>
          </div>
        </div>
      </div>
    </Teleport>

  </div>
</template>
