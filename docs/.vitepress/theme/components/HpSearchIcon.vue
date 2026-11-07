<script setup lang="ts">
/**
 * A small Lucide icon for the search box, drawn from the path data in `searchIcons.ts`. It is
 * decorative (hidden from assistive technology) and takes the text colour of its parent.
 */
import { computed } from 'vue'
import type { SearchIconName } from '../recentPages'
import { SEARCH_ICONS } from '../searchIcons'

/** Props of the icon. */
const props = defineProps<{
  /** Which icon to draw. */
  name: SearchIconName
}>()

/** Path data of the chosen icon. */
const paths = computed<readonly string[]>(() => SEARCH_ICONS[props.name])
</script>

<template>
  <svg
    class="hp-search-icon"
    viewBox="0 0 24 24"
    fill="none"
    stroke="currentColor"
    stroke-width="2"
    stroke-linecap="round"
    stroke-linejoin="round"
    aria-hidden="true"
  >
    <path v-for="(d, index) in paths" :key="index" :d="d" />
  </svg>
</template>

<style scoped>
.hp-search-icon {
  width: 1em;
  height: 1em;
  flex: none;
}
</style>
