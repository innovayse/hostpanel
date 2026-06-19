<script setup lang="ts">
/**
 * Displays a single installed plugin with its status and a remove button.
 */
import type { PluginListItemDto } from '../types/plugin.types'

const props = defineProps<{
  plugin: PluginListItemDto
  removing: boolean
}>()

const emit = defineEmits<{
  remove: [id: string]
}>()
</script>

<template>
  <div class="flex items-center gap-4 p-4 bg-surface-card rounded-2xl border border-border hover:border-border/80 transition-all duration-150">
    <!-- Color block / logo -->
    <div
      class="w-11 h-11 rounded-xl flex-shrink-0 flex items-center justify-center"
      :style="{ backgroundColor: plugin.color + '22' }"
    >
      <div class="w-5 h-5 rounded-md" :style="{ backgroundColor: plugin.color }" />
    </div>

    <!-- Info -->
    <div class="flex-1 min-w-0">
      <div class="flex items-center gap-2 flex-wrap">
        <span class="font-semibold text-text-primary text-[0.9rem]">{{ plugin.name }}</span>
        <span class="text-[0.72rem] text-text-muted font-mono">v{{ plugin.version }}</span>
        <span
          class="text-[0.65rem] font-semibold px-2 py-0.5 rounded-full border"
          :class="plugin.isLoaded
            ? 'text-status-green bg-status-green/10 border-status-green/20'
            : 'text-amber-400 bg-amber-400/10 border-amber-400/20'"
        >
          {{ plugin.isLoaded ? 'Active' : 'Pending restart' }}
        </span>
      </div>
      <p class="text-[0.78rem] text-text-muted mt-0.5 truncate">{{ plugin.description }}</p>
      <p class="text-[0.72rem] text-text-muted/60 mt-0.5">{{ plugin.category }} · by {{ plugin.author }}</p>
    </div>

    <!-- Remove -->
    <button
      type="button"
      class="shrink-0 text-[0.78rem] font-medium text-text-muted hover:text-status-red transition-colors disabled:opacity-40 px-3 py-1.5 rounded-xl hover:bg-status-red/8"
      :disabled="removing"
      @click="emit('remove', plugin.id)"
    >
      {{ removing ? 'Removing…' : 'Remove' }}
    </button>
  </div>
</template>
