<script setup lang="ts">
/**
 * Warning banner displayed after install or remove operations.
 * Prompts the admin to restart the server for changes to take effect.
 */
defineProps<{
  restarting: boolean
}>()

const emit = defineEmits<{
  restart: []
}>()
</script>

<template>
  <div class="flex items-center gap-4 bg-amber-500/8 border border-amber-500/20 rounded-2xl px-5 py-3.5">
    <div class="w-8 h-8 rounded-xl bg-amber-500/15 flex items-center justify-center shrink-0">
      <svg class="w-4 h-4 text-amber-400" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
        <path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"/>
        <line x1="12" y1="9" x2="12" y2="13"/>
        <line x1="12" y1="17" x2="12.01" y2="17"/>
      </svg>
    </div>
    <div class="flex-1">
      <p class="text-[0.85rem] font-medium text-amber-400">Restart required</p>
      <p class="text-[0.75rem] text-text-muted mt-0.5">Plugin changes will take effect after a server restart.</p>
    </div>
    <button
      type="button"
      class="shrink-0 px-4 py-1.5 rounded-xl text-[0.82rem] font-semibold bg-amber-500/15 hover:bg-amber-500/25 text-amber-400 border border-amber-500/20 transition-all duration-150 disabled:opacity-50"
      :disabled="restarting"
      @click="emit('restart')"
    >
      {{ restarting ? 'Restarting…' : 'Restart Server' }}
    </button>
  </div>
</template>
