<script setup lang="ts">
import { ref } from 'vue'
import { useMigrationStore } from '../stores/migrationStore'
import type { MigrationJob } from '../types/migration.types'

const props = defineProps<{ job: MigrationJob }>()
const store = useMigrationStore()

const checking = ref(false)
const starting = ref(false)

async function checkConnection() {
  checking.value = true
  try {
    await store.testConnection(props.job.id)
  } catch {
    // connection failed — job will reflect pluginConnected=false
  } finally {
    checking.value = false
  }
}

async function startImport() {
  starting.value = true
  try {
    await store.startImport(props.job.id)
    store.startPolling(props.job.id)
  } finally {
    starting.value = false
  }
}

const ENTITY_LABELS: Record<string, string> = {
  clients:  'Clients',
  invoices: 'Invoices',
  services: 'Services',
  domains:  'Domains',
  tickets:  'Tickets',
}

const ENTITY_ICONS: Record<string, string> = {
  clients:  'M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2M9 3a4 4 0 1 0 0 8 4 4 0 0 0 0-8z',
  invoices: 'M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8zM14 2v6h6M16 13H8M16 17H8M10 9H8',
  services: 'M22 12H2M5 12V7M19 12V7M8 12V5M12 12V3M16 12V5',
  domains:  'M12 2a10 10 0 1 0 0 20A10 10 0 0 0 12 2zM2 12h20M12 2a15 15 0 0 1 0 20M12 2a15 15 0 0 0 0 20',
  tickets:  'M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z',
}
</script>

<template>
  <div class="bg-surface-card border border-border rounded-2xl p-6">

    <!-- Header -->
    <div class="flex items-center justify-between mb-5">
      <div>
        <div class="flex items-center gap-2">
          <h3 class="text-[0.95rem] font-semibold text-text-primary">
            {{ job.label ?? 'Migration' }}
          </h3>
          <!-- Status badge -->
          <span
            class="text-[0.65rem] font-semibold rounded-full px-2.5 py-1 border"
            :class="{
              'text-amber-400 bg-amber-400/10 border-amber-400/20': job.status === 'Pending',
              'text-primary-400 bg-primary-500/10 border-primary-500/20': job.status === 'InProgress',
              'text-status-green bg-status-green/10 border-status-green/20': job.status === 'Completed',
              'text-status-red bg-status-red/8 border-status-red/20': job.status === 'Failed',
            }"
          >
            {{ job.status === 'InProgress' ? 'In Progress' : job.status }}
          </span>
        </div>
        <p class="text-[0.72rem] text-text-muted mt-0.5">Job #{{ job.id }}</p>
      </div>

      <!-- Overall percent -->
      <div class="text-right">
        <span class="text-[1.5rem] font-bold text-text-primary leading-none">{{ job.overallPercent }}%</span>
        <p class="text-[0.72rem] text-text-muted mt-0.5">overall</p>
      </div>
    </div>

    <!-- Overall progress bar -->
    <div class="w-full h-2 bg-white/[0.06] rounded-full mb-6 overflow-hidden">
      <div
        class="h-full rounded-full transition-all duration-500"
        :class="job.status === 'Completed' ? 'bg-status-green' : job.status === 'Failed' ? 'bg-status-red' : 'gradient-brand'"
        :style="{ width: `${job.overallPercent}%` }"
      />
    </div>

    <!-- Per-entity progress -->
    <div class="flex flex-col gap-3">
      <div
        v-for="(key) in ['clients', 'invoices', 'services', 'domains', 'tickets']"
        v-show="(job.entitySelection as any)[key]"
        :key="key"
        class="flex items-center gap-3"
      >
        <!-- Icon -->
        <div
          class="w-7 h-7 rounded-lg flex items-center justify-center shrink-0"
          :class="(job.progress as any)[key].done
            ? 'bg-status-green/10'
            : job.status === 'InProgress' && (job.progress as any)[key].imported > 0
              ? 'bg-primary-500/10'
              : 'bg-white/[0.04]'"
        >
          <svg
            class="w-3.5 h-3.5"
            :class="(job.progress as any)[key].done ? 'text-status-green' : 'text-text-muted'"
            viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"
            stroke-linecap="round" stroke-linejoin="round"
          >
            <path v-if="(job.progress as any)[key].done" d="M20 6L9 17l-5-5" />
            <path v-else :d="ENTITY_ICONS[key]" />
          </svg>
        </div>

        <!-- Label + bar -->
        <div class="flex-1 min-w-0">
          <div class="flex items-center justify-between mb-1">
            <span class="text-[0.78rem] font-medium text-text-secondary">{{ ENTITY_LABELS[key] }}</span>
            <span class="text-[0.72rem] text-text-muted font-mono">
              <template v-if="(job.progress as any)[key].total > 0">
                {{ (job.progress as any)[key].imported }} / {{ (job.progress as any)[key].total }}
              </template>
              <template v-else>—</template>
            </span>
          </div>
          <div class="w-full h-1.5 bg-white/[0.06] rounded-full overflow-hidden">
            <div
              class="h-full rounded-full transition-all duration-500"
              :class="(job.progress as any)[key].done ? 'bg-status-green' : 'gradient-brand'"
              :style="{
                width: (job.progress as any)[key].total > 0
                  ? `${Math.round(((job.progress as any)[key].imported / (job.progress as any)[key].total) * 100)}%`
                  : '0%'
              }"
            />
          </div>
        </div>
      </div>
    </div>

    <!-- Error message -->
    <div
      v-if="job.status === 'Failed' && job.errorMessage"
      class="mt-4 text-[0.78rem] text-status-red bg-status-red/8 border border-status-red/20 rounded-xl p-3"
    >
      {{ job.errorMessage }}
    </div>

    <!-- Plugin connection status -->
    <div
      v-if="job.status === 'Pending' || job.status === 'InProgress'"
      class="mt-4 flex items-center gap-2.5 rounded-xl p-3 border text-[0.78rem]"
      :class="job.pluginConnected
        ? 'bg-status-green/8 border-status-green/20 text-status-green'
        : 'bg-white/[0.03] border-border text-text-muted'"
    >
      <span
        class="w-2 h-2 rounded-full shrink-0"
        :class="job.pluginConnected ? 'bg-status-green' : 'bg-text-muted animate-pulse'"
      />
      <span class="flex-1">
        <span v-if="job.pluginConnected">Plugin connected</span>
        <span v-else>Waiting for the migration plugin to connect…</span>
      </span>
      <button
        class="shrink-0 flex items-center gap-1.5 px-3 py-1 rounded-lg text-[0.75rem] font-medium transition-all disabled:opacity-50"
        :class="job.pluginConnected
          ? 'text-status-green hover:bg-status-green/10'
          : 'text-text-muted hover:bg-white/[0.06]'"
        :disabled="checking"
        @click="checkConnection"
      >
        <svg
          class="w-3 h-3"
          :class="checking ? 'animate-spin' : ''"
          viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"
          stroke-linecap="round" stroke-linejoin="round"
        >
          <path d="M21.5 2v6h-6M2.5 22v-6h6M2 11.5a10 10 0 0 1 18.8-4.3M22 12.5a10 10 0 0 1-18.8 4.3"/>
        </svg>
        {{ checking ? 'Checking…' : 'Check' }}
      </button>
    </div>

    <!-- Start Import button (show only when pending + plugin connected) -->
    <div v-if="job.status === 'Pending' && job.pluginConnected" class="mt-3">
      <button
        class="w-full flex items-center justify-center gap-2 py-2.5 rounded-xl text-[0.82rem] font-semibold gradient-brand text-white hover:opacity-90 transition-all disabled:opacity-50"
        :disabled="starting"
        @click="startImport"
      >
        <svg class="w-4 h-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <polygon points="5 3 19 12 5 21 5 3"/>
        </svg>
        {{ starting ? 'Starting…' : 'Start Import' }}
      </button>
    </div>

  </div>
</template>
