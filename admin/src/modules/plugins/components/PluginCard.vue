<script setup lang="ts">
/**
 * Displays a single installed plugin with its status and a remove button.
 */
import type { PluginListItemDto } from '../types/plugin.types'

/** Props for PluginCard. */
const props = defineProps<{
  /** The plugin to display. */
  plugin: PluginListItemDto
  /** True while a remove request is in flight for this plugin. */
  removing: boolean
}>()

/** Emits for PluginCard. */
const emit = defineEmits<{
  /** Emitted when the admin clicks Remove. */
  remove: [id: string]
}>()
</script>

<template>
  <div class="flex items-start gap-4 p-4 bg-white rounded-xl border border-gray-200">
    <!-- Color block -->
    <div
      class="w-10 h-10 rounded-lg flex-shrink-0"
      :style="{ backgroundColor: plugin.color }"
    />

    <!-- Info -->
    <div class="flex-1 min-w-0">
      <div class="flex items-center gap-2">
        <span class="font-semibold text-gray-800 text-sm">{{ plugin.name }}</span>
        <span class="text-xs text-gray-400">v{{ plugin.version }}</span>
        <span
          class="text-xs font-medium px-2 py-0.5 rounded-full"
          :class="plugin.isLoaded ? 'bg-green-100 text-green-700' : 'bg-yellow-100 text-yellow-700'"
        >
          {{ plugin.isLoaded ? 'Loaded' : 'Pending restart' }}
        </span>
      </div>
      <div class="text-xs text-gray-500 mt-0.5">{{ plugin.description }}</div>
      <div class="text-xs text-gray-400 mt-1">{{ plugin.category }} · by {{ plugin.author }}</div>
    </div>

    <!-- Remove -->
    <button
      type="button"
      class="text-xs text-red-600 hover:text-red-800 font-medium transition disabled:opacity-40"
      :disabled="removing"
      @click="emit('remove', plugin.id)"
    >
      Remove
    </button>
  </div>
</template>
