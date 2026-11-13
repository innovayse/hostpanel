<template>
  <div class="rounded-xl border border-gray-200 dark:border-white/10 bg-white dark:bg-white/5 overflow-hidden" data-testid="ui-nav-group">
    <div class="px-4 py-2.5 border-b border-gray-100 dark:border-white/10 flex items-center gap-2">
      <component :is="icon" v-if="icon" :size="13" :stroke-width="2" class="text-gray-500 dark:text-gray-400" />
      <span class="text-xs font-semibold text-gray-500 dark:text-gray-400 uppercase tracking-wide">{{ title }}</span>
    </div>
    <nav class="p-1" :aria-label="title">
      <slot />
    </nav>
  </div>
</template>

<script setup lang="ts">
/**
 * UiNavGroup: a titled panel of UiNavItem entries (the side menu of a detail page).
 */
import type { Component } from 'vue'

/** Props of UiNavGroup. */
interface Props {
  /** Heading of the group; also the accessible name of its menu. */
  title: string
  /** A lucide icon component shown before the heading. */
  icon?: Component
}

withDefaults(defineProps<Props>(), {
  icon: undefined,
})
</script>
