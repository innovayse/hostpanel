<template>
  <div class="flex flex-col items-center justify-center text-center py-12 px-4" data-testid="ui-empty-state">
    <div
      v-if="icon"
      class="w-14 h-14 rounded-full bg-gray-100 dark:bg-white/5 flex items-center justify-center mb-4"
    >
      <component :is="icon" :size="26" :stroke-width="1.75" class="text-gray-500 dark:text-gray-400" />
    </div>
    <h3 class="text-base font-semibold text-gray-900 dark:text-white">{{ title }}</h3>
    <p v-if="description" class="mt-1 max-w-sm text-sm text-gray-500 dark:text-gray-400">{{ description }}</p>
    <div v-if="$slots.default" class="mt-5">
      <slot />
    </div>
  </div>
</template>

<script setup lang="ts">
/**
 * UiEmptyState. The "nothing here yet" block of a list or page: an icon, a title, a sentence and
 * an optional action in the default slot (a button that creates the first item).
 */
import type { Component } from 'vue'

/** Props of UiEmptyState. */
interface Props {
  /** Headline. */
  title: string
  /** One supporting sentence. */
  description?: string
  /** A lucide icon component. */
  icon?: Component
}

withDefaults(defineProps<Props>(), {
  description: undefined,
  icon: undefined,
})
</script>
