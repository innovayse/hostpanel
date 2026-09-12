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
  /** True while a connection test is in flight. */
  testing?: boolean
  /** Host the integration talks to (e.g. "pg.inecoecom.am"), when it has one. */
  endpointHost?: string
  /** Optional hint text shown in a callout box. */
  hint?: string
}>()

/**
 * Formats lastTestedAt into a human-readable relative string.
 *
 * @returns Formatted string like "2 hours ago" or "Never".
 */
/**
 * Formats a test timestamp as local wall-clock time, e.g. "14:02:37".
 *
 * @param iso - ISO 8601 timestamp.
 * @returns Local time, or an empty string when the input cannot be parsed.
 */
function formatClock(iso: string): string {
  const d = new Date(iso)
  return Number.isNaN(d.getTime()) ? '' : d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' })
}

/**
 * Formats a round-trip duration for the detail row.
 *
 * @param ms - Duration in milliseconds.
 * @returns "812 ms" below a second, "2.4 s" above it.
 */
function formatDuration(ms: number): string {
  return ms < 1000 ? `${ms} ms` : `${(ms / 1000).toFixed(1)} s`
}

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

      <!-- The three states swap through one transition so a result never just snaps into
           place: the probe's skeleton fades out and the verdict slides up in its place. -->
      <Transition name="status-swap" mode="out-in">

        <!-- Test in flight. Checked first: the store clears the previous result when a test
             starts, so without this branch the card fell straight through to "Not tested yet"
             for the duration of the probe and gave no sign anything was happening. The two
             skeleton bars sit exactly where the verdict and its message will land. -->
        <div v-if="testing" key="testing">
          <div class="flex items-center gap-2 mb-2">
            <span class="w-3.5 h-3.5 shrink-0 rounded-full border-2 border-primary-500/20 border-t-primary-500 animate-spin" />
            <span class="text-[0.82rem] font-medium text-text-primary">Testing connection…</span>
          </div>
          <div class="pl-[1.375rem] flex flex-col gap-1.5" aria-hidden="true">
            <span class="skeleton-bar h-2.5 w-3/4" />
            <span class="skeleton-bar h-2.5 w-1/2" />
          </div>
        </div>

        <!-- In-session test result -->
        <div v-else-if="testResult" :key="testResult.success ? 'ok' : 'failed'">
          <div class="flex items-center gap-2 mb-1">
            <span class="relative flex w-2 h-2 shrink-0">
              <!-- One-shot ping on success: the only moment the card has news worth a glance. -->
              <span
                v-if="testResult.success"
                class="absolute inline-flex h-full w-full rounded-full bg-status-green opacity-75 animate-ping-once"
              />
              <span
                class="relative inline-flex w-2 h-2 rounded-full"
                :class="testResult.success ? 'bg-status-green' : 'bg-status-red'"
              />
            </span>
            <span class="text-[0.82rem] font-medium" :class="testResult.success ? 'text-status-green' : 'text-status-red'">
              {{ testResult.success ? 'Connection OK' : 'Connection Failed' }}
            </span>
          </div>
          <p class="text-[0.76rem] text-text-muted pl-4">{{ testResult.message }}</p>

          <!-- What the probe actually did. Each row is shown only when its value exists, so a
               built-in integration without a URL still gets a tidy card. -->
          <dl class="mt-3 pl-4 grid grid-cols-[auto_1fr] gap-x-3 gap-y-1 text-[0.72rem]">
            <template v-if="endpointHost">
              <dt class="text-text-muted">Endpoint</dt>
              <dd class="text-text-secondary font-mono truncate">{{ endpointHost }}</dd>
            </template>
            <template v-if="testResult.testedAt">
              <dt class="text-text-muted">Tested</dt>
              <dd class="text-text-secondary">{{ formatClock(testResult.testedAt) }}</dd>
            </template>
            <template v-if="testResult.durationMs !== undefined">
              <dt class="text-text-muted">Round trip</dt>
              <dd class="text-text-secondary">{{ formatDuration(testResult.durationMs) }}</dd>
            </template>
          </dl>
        </div>

        <!-- Persisted last-tested -->
        <div v-else key="idle">
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
        </div>

      </Transition>
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

<style scoped>
/* Skeleton placeholder with the shared shimmer sweep (keyframe lives in assets/main.css). */
.skeleton-bar {
  position: relative;
  overflow: hidden;
  border-radius: 9999px;
  background: rgb(255 255 255 / 0.06);
}
.skeleton-bar::after {
  content: '';
  position: absolute;
  inset: 0;
  transform: translateX(-100%);
  background: linear-gradient(90deg, transparent, rgb(255 255 255 / 0.12), transparent);
  animation: shimmer 1.4s infinite;
}

/* Verdict swap: outgoing state fades, incoming one rises into place. */
.status-swap-enter-active { transition: opacity 180ms ease-out, transform 180ms ease-out; }
.status-swap-leave-active { transition: opacity 120ms ease-in; }
.status-swap-enter-from { opacity: 0; transform: translateY(4px); }
.status-swap-leave-to { opacity: 0; }

/* Tailwind's animate-ping loops forever; a verdict deserves one ring, not a beacon. */
@keyframes ping-once {
  0%   { transform: scale(1);   opacity: 0.75; }
  100% { transform: scale(2.6); opacity: 0; }
}
.animate-ping-once { animation: ping-once 700ms cubic-bezier(0, 0, 0.2, 1) 1 forwards; }

@media (prefers-reduced-motion: reduce) {
  .skeleton-bar::after, .animate-ping-once { animation: none; }
  .status-swap-enter-active, .status-swap-leave-active { transition: none; }
}
</style>
