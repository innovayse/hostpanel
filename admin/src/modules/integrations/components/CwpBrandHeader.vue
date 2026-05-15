<script setup lang="ts">
/**
 * Dark branded header for the CWP integration config page.
 *
 * Shows CWP logo, title, version badge, and an animated connection status dot.
 */

/** Props for CwpBrandHeader. */
const props = defineProps<{
  /** CWP version string to display in the badge, or null while loading. */
  version: string | null
  /** Connection status — controls dot color and animation. */
  status: 'connected' | 'error' | 'unknown'
}>()
</script>

<template>
  <div class="flex flex-wrap items-center gap-3 bg-surface-card border border-border rounded-2xl px-6 py-5"
    style="border-left: 3px solid #0ea5e9;">

    <!-- Left: logo + title + version -->
    <div class="flex items-center gap-4 flex-1 min-w-0">
      <div class="flex items-center justify-center w-11 h-11 rounded-xl bg-[#1a73e8] shrink-0">
        <span class="font-display font-bold text-lg text-white leading-none">C</span>
      </div>
      <div class="min-w-0">
        <h1 class="font-display font-bold text-[1.05rem] text-text-primary leading-none mb-1 truncate">CentOS Web Panel</h1>
        <p class="text-[0.75rem] text-text-secondary">Provisioning Provider</p>
      </div>
      <span
        v-if="props.version"
        class="text-[0.7rem] font-mono font-medium text-text-secondary bg-white/[0.05] border border-border rounded-full px-2.5 py-1"
      >
        v{{ props.version }}
      </span>
    </div>

    <!-- Right: connection status -->
    <div class="flex items-center gap-2 ml-auto shrink-0">
      <span
        class="w-2.5 h-2.5 rounded-full shrink-0"
        :class="{
          'bg-status-green animate-pulse': props.status === 'connected',
          'bg-status-red': props.status === 'error',
          'bg-text-muted': props.status === 'unknown',
        }"
      />
      <span
        class="text-[0.82rem] font-medium"
        :class="{
          'text-status-green': props.status === 'connected',
          'text-status-red':   props.status === 'error',
          'text-text-muted':   props.status === 'unknown',
        }"
      >
        {{ props.status === 'connected' ? 'Connected' : props.status === 'error' ? 'Connection Error' : 'Unknown' }}
      </span>
    </div>

  </div>
</template>
