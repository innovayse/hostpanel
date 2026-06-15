<script setup lang="ts">
import { ref, computed } from 'vue'
import { useMigrationStore } from '../stores/migrationStore'
import type { MigrationJob } from '../types/migration.types'

const props = defineProps<{ job: MigrationJob }>()
const store = useMigrationStore()

const checking = ref(false)
const starting = ref(false)
const checkResult = ref<'success' | 'error' | null>(null)
let checkResultTimer: ReturnType<typeof setTimeout> | null = null

async function checkConnection() {
  checking.value = true
  checkResult.value = null
  if (checkResultTimer) clearTimeout(checkResultTimer)
  try {
    const job = await store.testConnection(props.job.id)
    checkResult.value = job.pluginConnected ? 'success' : 'error'
  } catch {
    checkResult.value = 'error'
  } finally {
    checking.value = false
    checkResultTimer = setTimeout(() => { checkResult.value = null }, 3000)
  }
}

async function startImport() {
  starting.value = true
  try {
    await store.startImport(props.job.id)
    store.startPolling(props.job.id)
  } finally { starting.value = false }
}

const ENTITY_KEYS = ['clients', 'invoices', 'services', 'domains', 'tickets', 'products', 'orders', 'transactions', 'quotes', 'knowledgebase', 'contacts', 'ticketReplies', 'announcements', 'downloads', 'networkIssues'] as const

const ENTITY_LABELS: Record<string, string> = {
  clients: 'Clients', invoices: 'Invoices', services: 'Services', domains: 'Domains',
  tickets: 'Tickets', products: 'Products', orders: 'Orders', transactions: 'Transactions',
  quotes: 'Quotes', knowledgebase: 'Knowledgebase', contacts: 'Contacts', ticketReplies: 'Ticket Replies',
  announcements: 'Announcements', downloads: 'Downloads', networkIssues: 'Network Issues',
}


const activeKeys = computed(() =>
  ENTITY_KEYS.filter(k => (props.job.entitySelection as any)[k])
)

const totalMigrated = computed(() =>
  activeKeys.value.reduce((s, k) => s + ((props.job.progress as any)[k]?.imported ?? 0), 0)
)
const totalSkipped = computed(() =>
  activeKeys.value.reduce((s, k) => s + ((props.job.progress as any)[k]?.skipped ?? 0), 0)
)
const totalRecords = computed(() =>
  activeKeys.value.reduce((s, k) => s + ((props.job.progress as any)[k]?.total ?? 0), 0)
)
const totalRemaining = computed(() =>
  activeKeys.value.reduce((s, k) => {
    const p = (props.job.progress as any)[k]
    return s + (!p?.done ? Math.max(0, (p?.total ?? 0) - (p?.imported ?? 0) - (p?.skipped ?? 0)) : 0)
  }, 0)
)
const totalFailed = computed(() =>
  activeKeys.value.reduce((s, k) => {
    const p = (props.job.progress as any)[k]
    return s + (p?.total > 0 && p?.done ? Math.max(0, p.total - p.imported - p.skipped) : 0)
  }, 0)
)

function rowPercent(key: string): number {
  const p = (props.job.progress as any)[key]
  if (!p || p.total === 0) return 0
  return Math.round(((p.imported + (p.skipped ?? 0)) / p.total) * 100)
}
</script>

<template>
  <div class="bg-surface-card border border-border rounded-2xl overflow-hidden">

    <!-- Header -->
    <div class="flex items-center justify-between px-5 pt-4 pb-3">
      <div>
        <div class="flex items-center gap-2 mb-0.5">
          <h3 class="text-[0.9rem] font-bold text-text-primary leading-none">{{ job.label ?? 'Migration' }}</h3>
          <span
            class="text-[0.6rem] font-semibold rounded-full px-2 py-0.5 border"
            :class="{
              'text-amber-400 bg-amber-400/10 border-amber-400/20':          job.status === 'Pending',
              'text-primary-400 bg-primary-500/10 border-primary-500/20':    job.status === 'InProgress',
              'text-status-green bg-status-green/10 border-status-green/20': job.status === 'Completed',
              'text-status-red bg-status-red/10 border-status-red/20':       job.status === 'Failed',
            }"
          >{{ job.status === 'InProgress' ? 'In Progress' : job.status }}</span>
          <span v-if="job.status === 'InProgress'" class="w-3.5 h-3.5 rounded-full border-2 border-primary-400/30 border-t-primary-400 animate-spin" />
        </div>
        <p class="text-[0.68rem] text-text-muted">Job #{{ job.id }}</p>
      </div>

      <!-- Big percent -->
      <div class="text-right">
        <span class="text-[2rem] font-black text-text-primary leading-none tabular-nums">{{ job.overallPercent }}</span>
        <span class="text-[0.8rem] text-text-muted font-medium">%</span>
      </div>
    </div>

    <!-- Overall progress bar -->
    <div class="px-5 pb-4">
      <div class="w-full h-1.5 bg-white/[0.06] rounded-full overflow-hidden relative">
        <div
          class="h-full rounded-full transition-all duration-700 relative overflow-hidden"
          :class="job.status === 'Completed' ? 'bg-status-green' : job.status === 'Failed' ? 'bg-status-red' : 'gradient-brand'"
          :style="{ width: `${job.overallPercent}%` }"
        >
          <span
            v-if="job.status === 'InProgress'"
            class="absolute inset-0 -translate-x-full animate-[shimmer_1.4s_infinite] bg-gradient-to-r from-transparent via-white/25 to-transparent"
          />
        </div>
      </div>
    </div>

    <!-- Entity checklist -->
    <div class="border-t border-border/60 divide-y divide-border/30">
      <div
        v-for="key in activeKeys"
        :key="key"
        class="group flex items-center gap-3 px-5 py-2 transition-colors duration-100 hover:bg-white/[0.02]"
      >
        <!-- Status icon -->
        <div class="shrink-0 w-5 h-5 flex items-center justify-center">
          <!-- Done -->
          <svg v-if="(job.progress as any)[key].done" class="w-4 h-4 text-status-green" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round">
            <path d="M20 6L9 17l-5-5"/>
          </svg>
          <!-- In progress -->
          <svg v-else-if="job.status === 'InProgress' && (job.progress as any)[key].imported > 0" class="w-4 h-4 text-primary-400" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
            <path d="M12 2v4M12 18v4M4.93 4.93l2.83 2.83M16.24 16.24l2.83 2.83M2 12h4M18 12h4M4.93 19.07l2.83-2.83M16.24 7.76l2.83-2.83"/>
          </svg>
          <!-- Pending -->
          <svg v-else class="w-3.5 h-3.5 text-text-muted/40" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
            <circle cx="12" cy="12" r="9"/>
          </svg>
        </div>

        <!-- Label -->
        <span
          class="text-[0.78rem] flex-1 transition-colors duration-100"
          :class="(job.progress as any)[key].done
            ? 'text-text-primary font-medium'
            : job.status === 'InProgress' && (job.progress as any)[key].imported > 0
              ? 'text-text-secondary'
              : 'text-text-secondary'"
        >{{ ENTITY_LABELS[key] }}</span>

        <!-- Mini progress bar -->
        <div class="w-16 h-1 bg-white/[0.06] rounded-full overflow-hidden shrink-0">
          <div
            class="h-full rounded-full transition-all duration-500"
            :class="(job.progress as any)[key].done ? 'bg-status-green' : 'gradient-brand'"
            :style="{ width: `${rowPercent(key)}%` }"
          />
        </div>

        <!-- Count -->
        <span
          class="text-[0.72rem] font-semibold tabular-nums shrink-0 min-w-[48px] text-right"
          :class="(job.progress as any)[key].done ? 'text-status-green' : 'text-text-muted'"
        >
          <template v-if="(job.progress as any)[key].total > 0">
            {{ (job.progress as any)[key].imported + ((job.progress as any)[key].skipped ?? 0) }}<span class="opacity-40 font-normal">/{{ (job.progress as any)[key].total }}</span>
          </template>
          <template v-else>—</template>
        </span>
      </div>
    </div>

    <!-- KPI Stats -->
    <div class="border-t border-border/60 px-5 py-4 grid grid-cols-4 gap-3">
      <!-- Migrated -->
      <div class="bg-white/[0.03] border border-border/50 rounded-xl p-3 flex flex-col gap-1">
        <div class="flex items-center gap-1.5 mb-0.5">
          <svg class="w-3 h-3 text-status-green" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round">
            <path d="M20 6L9 17l-5-5"/>
          </svg>
          <span class="text-[0.6rem] font-semibold uppercase tracking-widest text-text-muted">Migrated</span>
        </div>
        <span class="text-[1.25rem] font-black text-text-primary leading-none tabular-nums">{{ totalMigrated }}</span>
      </div>

      <!-- Skipped -->
      <div class="bg-white/[0.03] border border-border/50 rounded-xl p-3 flex flex-col gap-1">
        <div class="flex items-center gap-1.5 mb-0.5">
          <svg class="w-3 h-3 text-amber-400" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
            <path d="M5 12h14M12 5l7 7-7 7"/>
          </svg>
          <span class="text-[0.6rem] font-semibold uppercase tracking-widest text-text-muted">Skipped</span>
        </div>
        <span class="text-[1.25rem] font-black text-amber-400 leading-none tabular-nums">{{ totalSkipped }}</span>
      </div>

      <!-- Failed -->
      <div class="bg-white/[0.03] border border-border/50 rounded-xl p-3 flex flex-col gap-1">
        <div class="flex items-center gap-1.5 mb-0.5">
          <svg class="w-3 h-3 text-status-red" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round">
            <path d="M18 6L6 18M6 6l12 12"/>
          </svg>
          <span class="text-[0.6rem] font-semibold uppercase tracking-widest text-text-muted">Failed</span>
        </div>
        <span
          class="text-[1.25rem] font-black leading-none tabular-nums"
          :class="totalFailed > 0 ? 'text-status-red' : 'text-text-primary'"
        >{{ totalFailed }}</span>
      </div>

      <!-- ETA / Status -->
      <div class="bg-white/[0.03] border border-border/50 rounded-xl p-3 flex flex-col gap-1">
        <div class="flex items-center gap-1.5 mb-0.5">
          <svg class="w-3 h-3 text-primary-400" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
            <rect x="3" y="3" width="18" height="18" rx="2"/><path d="M3 9h18"/>
          </svg>
          <span class="text-[0.6rem] font-semibold uppercase tracking-widest text-text-muted">Total</span>
        </div>
        <span class="text-[1.25rem] font-black text-text-primary leading-none tabular-nums">{{ totalRecords }}</span>
      </div>
    </div>

    <!-- Error -->
    <div
      v-if="job.status === 'Failed' && job.errorMessage"
      class="mx-5 mb-4 text-[0.75rem] text-status-red bg-status-red/8 border border-status-red/20 rounded-xl px-3 py-2.5"
    >{{ job.errorMessage }}</div>

    <!-- Plugin connection + Start -->
    <div
      v-if="job.status === 'Pending' || job.status === 'InProgress'"
      class="px-5 py-3 border-t border-border/60 flex items-center gap-2"
    >
      <div
        class="flex-1 flex items-center gap-2 rounded-xl px-3 py-2 border text-[0.75rem]"
        :class="job.pluginConnected
          ? 'bg-status-green/8 border-status-green/20 text-status-green'
          : 'bg-white/[0.03] border-border text-text-muted'"
      >
        <span class="w-1.5 h-1.5 rounded-full shrink-0" :class="job.pluginConnected ? 'bg-status-green' : 'bg-text-muted animate-pulse'" />
        <span class="flex-1">{{ job.pluginConnected ? 'Plugin connected' : 'Waiting for plugin…' }}</span>
        <button
          class="flex items-center gap-1.5 text-[0.78rem] font-semibold px-3 py-1.5 rounded-xl gradient-brand text-white hover:opacity-90 transition-all disabled:opacity-50 shadow-lg shadow-primary-500/20 shrink-0"
          :disabled="checking"
          @click="checkConnection"
        >
          <svg class="w-3.5 h-3.5" :class="checking ? 'animate-spin' : ''" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
            <path d="M21.5 2v6h-6M2.5 22v-6h6M2 11.5a10 10 0 0 1 18.8-4.3M22 12.5a10 10 0 0 1-18.8 4.3"/>
          </svg>
          {{ checking ? 'Checking…' : 'Check' }}
        </button>

        <!-- Check result toast -->
        <Transition
          enter-active-class="transition duration-200 ease-out"
          enter-from-class="opacity-0 translate-x-2"
          enter-to-class="opacity-100 translate-x-0"
          leave-active-class="transition duration-150 ease-in"
          leave-from-class="opacity-100"
          leave-to-class="opacity-0"
        >
          <div
            v-if="checkResult"
            class="flex items-center gap-1.5 text-[0.75rem] font-medium px-2.5 py-1.5 rounded-xl border shrink-0"
            :class="checkResult === 'success'
              ? 'text-status-green bg-status-green/10 border-status-green/20'
              : 'text-status-red bg-status-red/10 border-status-red/20'"
          >
            <svg v-if="checkResult === 'success'" class="w-3.5 h-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round">
              <path d="M20 6L9 17l-5-5"/>
            </svg>
            <svg v-else class="w-3.5 h-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round">
              <path d="M18 6L6 18M6 6l12 12"/>
            </svg>
            {{ checkResult === 'success' ? 'Connected' : 'Not reachable' }}
          </div>
        </Transition>
      </div>

      <button
        v-if="job.status === 'Pending' && job.pluginConnected"
        class="flex items-center gap-2 px-4 py-2 rounded-xl text-[0.78rem] font-semibold gradient-brand text-white hover:opacity-90 transition-all disabled:opacity-50 shadow-lg shadow-primary-500/20 shrink-0"
        :disabled="starting"
        @click="startImport"
      >
        <span v-if="starting" class="w-3.5 h-3.5 rounded-full border-2 border-white/30 border-t-white animate-spin" />
        <svg v-else class="w-3.5 h-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <polygon points="5 3 19 12 5 21 5 3"/>
        </svg>
        {{ starting ? 'Starting…' : 'Start Import' }}
      </button>
    </div>

  </div>
</template>
