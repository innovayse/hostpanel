<template>
  <li :class="classes">
    <component :is="icon" v-if="icon" :size="18" :stroke-width="2" class="flex-shrink-0 mt-0.5 text-primary-600 dark:text-primary-400" />
    <span v-if="icon" class="min-w-0"><slot /></span>
    <slot v-else />
  </li>
</template>

<script setup lang="ts">
/**
 * UiListItem: one entry of a UiList, optionally led by an icon (a check mark in a feature list).
 */
import { computed } from 'vue'
import type { Component } from 'vue'

/** Props of UiListItem. */
interface Props {
  /** A lucide icon component shown before the text. */
  icon?: Component
}

/** Props of the component with their defaults applied. */
const props = withDefaults(defineProps<Props>(), {
  icon: undefined,
})

/** Class list of the item. */
const classes = computed(() => [
  'leading-relaxed text-gray-700 dark:text-gray-300',
  props.icon ? 'flex items-start gap-2' : '',
].filter(Boolean).join(' '))
</script>
