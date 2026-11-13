<template>
  <div class="flex items-center justify-between mb-5">
    <h2 class="text-base font-bold text-gray-900 dark:text-white flex items-center gap-2">
      <component :is="icon" v-if="icon" :size="18" :stroke-width="2" class="text-gray-500 dark:text-gray-400 flex-shrink-0" />
      {{ title }}
    </h2>
    <slot />
  </div>
</template>

<script setup lang="ts">
/**
 * CardHeader (`UiCardHeader`). The title row of a card: an optional icon and title on the
 * left, the default slot (actions, badges) on the right.
 */
import type { Component } from 'vue'

/** Props of CardHeader. */
interface Props {
  /** The heading text. */
  title: string
  /** A lucide icon component shown before the title. */
  icon?: Component
}

withDefaults(defineProps<Props>(), {
  icon: undefined,
})
</script>
