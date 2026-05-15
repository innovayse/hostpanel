<script setup lang="ts">
/**
 * Right sidebar for the integration detail page.
 *
 * Displays last connection test status and an optional contextual hint.
 */
import type { IntegrationTestResult } from '../types/integration.types'

/** Props for IntegrationStatusSidebar. */
const props = defineProps<{
  /** ISO 8601 timestamp of last successful test, or null if never tested. */
  lastTestedAt: string | null
  /** Result of the most recent in-session test, or null. */
  testResult: IntegrationTestResult | null
  /** Optional hint text shown in a callout box. */
  hint?: string
}>()

/**
 * Formats lastTestedAt into a human-readable relative string.
 *
 * @returns Formatted string like "2 hours ago" or "Never".
 */
function formatLastTested(): string {
  if (!props.lastTestedAt) return 'Never'
  const diff = Date.now() - new Date(props.lastTestedAt).getTime()
  const minutes = Math.floor(diff / 60000)
  if (minutes < 60) return `${minutes} minute${minutes !== 1 ? 's' : ''} ago`
  const hours = Math.floor(minutes / 60)
  if (hours < 24) return `${hours} hour${hours !== 1 ? 's' : ''} ago`
  return `${Math.floor(hours / 24)} day(s) ago`
}
</script>

<template>
  <div class="flex flex-col gap-3">

    <!-- Status card -->
    <div class="bg-surface-card border border-border rounded-2xl p-4">
      <p class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-3">Connection Status</p>

      <!-- In-session test result -->
      <template v-if="testResult">
        <div class="flex items-center gap-2 mb-1">
          <span
            class="w-2 h-2 rounded-full shrink-0"
            :class="testResult.success ? 'bg-status-green' : 'bg-status-red'"
          />
          <span class="text-[0.82rem] font-medium" :class="testResult.success ? 'text-status-green' : 'text-status-red'">
            {{ testResult.success ? 'Connection OK' : 'Connection Failed' }}
          </span>
        </div>
        <p class="text-[0.76rem] text-text-muted pl-4">{{ testResult.message }}</p>
      </template>

      <!-- Persisted last-tested -->
      <template v-else>
        <div class="flex items-center gap-2 mb-1.5">
          <span
            class="w-2 h-2 rounded-full shrink-0"
            :class="lastTestedAt ? 'bg-status-green animate-pulse' : 'bg-border'"
          />
          <span class="text-[0.82rem] font-medium text-text-primary">
            {{ lastTestedAt ? 'Previously tested OK' : 'Not tested yet' }}
          </span>
        </div>
        <p class="text-[0.75rem] text-text-muted pl-4">Last tested: {{ formatLastTested() }}</p>
      </template>
    </div>

    <!-- Hint callout -->
    <div
      v-if="hint"
      class="bg-status-yellow/[0.07] border border-status-yellow/20 rounded-2xl p-4"
    >
      <p class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-status-yellow mb-2">Setup Note</p>
      <p class="text-[0.78rem] text-text-secondary leading-relaxed">{{ hint }}</p>
    </div>

  </div>
</template>
