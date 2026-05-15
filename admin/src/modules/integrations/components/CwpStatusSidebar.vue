<script setup lang="ts">
/**
 * Live status sidebar for the CWP integration config page.
 *
 * Displays connection badge, accounts count, CWP version, and last tested time.
 * Emits `test` when the user clicks Test Connection.
 */
import { computed } from 'vue'
import type { CwpServerInfoDto } from '../types/cwp.types'

/** Props for CwpStatusSidebar. */
const props = defineProps<{
  /** CWP server info data, null while loading or on error. */
  info: CwpServerInfoDto | null
  /** True while a test connection request is in progress. */
  testing: boolean
}>()

const emit = defineEmits<{
  /** Emitted when the user clicks the Test Connection button. */
  test: []
}>()

/** Human-readable relative time since last test. */
const lastTestedLabel = computed(() => {
  if (!props.info?.lastTestedAt) return 'Never'
  const diff = Math.floor((Date.now() - new Date(props.info.lastTestedAt).getTime()) / 1000)
  if (diff < 60) return 'Just now'
  if (diff < 3600) return `${Math.floor(diff / 60)} minutes ago`
  return `${Math.floor(diff / 3600)} hours ago`
})
</script>

<template>
  <div class="bg-surface-card border border-border rounded-2xl p-5 flex flex-col gap-5">

    <!-- Connection badge -->
    <div class="flex items-center gap-2">
      <span
        class="w-2.5 h-2.5 rounded-full shrink-0"
        :class="props.info?.connected ? 'bg-status-green animate-pulse' : 'bg-text-muted'"
      />
      <span
        class="text-[0.85rem] font-semibold"
        :class="props.info?.connected ? 'text-status-green' : 'text-text-muted'"
      >
        {{ props.info?.connected ? 'Connected' : props.info ? 'Not Connected' : 'Status Unknown' }}
      </span>
    </div>

    <!-- Accounts count -->
    <div v-if="props.info?.connected">
      <p class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1">Accounts</p>
      <p class="font-display text-3xl font-bold text-text-primary">{{ props.info.accountsCount ?? '—' }}</p>
    </div>

    <!-- CWP Version -->
    <div v-if="props.info?.cwpVersion">
      <p class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1">CWP Version</p>
      <p class="text-[0.85rem] font-mono font-medium text-text-primary">{{ props.info.cwpVersion }}</p>
    </div>

    <!-- Last tested -->
    <div>
      <p class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1">Last Tested</p>
      <p class="text-[0.82rem] text-text-secondary">{{ lastTestedLabel }}</p>
    </div>

    <!-- Error message -->
    <div
      v-if="props.info && !props.info.connected && props.info.errorMessage"
      class="text-[0.76rem] text-status-red bg-status-red/8 border border-status-red/20 rounded-lg px-3 py-2"
    >
      {{ props.info.errorMessage }}
    </div>

    <!-- Test button -->
    <button
      class="w-full bg-white/[0.05] border border-border text-text-secondary rounded-[10px] px-4 py-2.5 text-[0.84rem] font-semibold transition-all duration-150 hover:text-text-primary hover:border-primary-500/30 hover:bg-primary-500/[0.05] disabled:opacity-40 disabled:cursor-not-allowed"
      :disabled="props.testing"
      @click="emit('test')"
    >
      {{ props.testing ? 'Testing…' : 'Test Connection' }}
    </button>

  </div>
</template>
