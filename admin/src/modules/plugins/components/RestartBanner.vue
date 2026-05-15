<script setup lang="ts">
/**
 * Warning banner displayed after install or remove operations.
 * Prompts the admin to restart the server for changes to take effect.
 */

/** Props for RestartBanner. */
defineProps<{
  /** True while the restart request is in flight. */
  restarting: boolean
}>()

/** Emits for RestartBanner. */
const emit = defineEmits<{
  /** Emitted when the admin clicks Restart Server. */
  restart: []
}>()
</script>

<template>
  <div class="flex items-center gap-4 bg-amber-50 border border-amber-200 rounded-xl px-5 py-3">
    <span class="text-amber-600 text-sm font-medium flex-1">
      ⚠ Restart required to apply plugin changes.
    </span>
    <button
      type="button"
      class="bg-amber-500 hover:bg-amber-600 text-white text-xs font-semibold px-4 py-1.5 rounded-lg transition disabled:opacity-50"
      :disabled="restarting"
      @click="emit('restart')"
    >
      {{ restarting ? 'Restarting…' : 'Restart Server' }}
    </button>
  </div>
</template>
