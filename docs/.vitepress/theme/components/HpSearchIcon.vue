<script setup lang="ts">
/**
 * A small icon for the search box, a component of the icon library chosen by name in `searchIcons.ts`. It is
 * decorative (hidden from assistive technology) and takes the text colour of its parent.
 */
import type { SearchIconName } from '../recentPages'
import { SEARCH_ICONS } from '../searchIcons'

/** Props of the icon. */
defineProps<{
  /** Which icon to draw. */
  name: SearchIconName
}>()
</script>

<template>
  <component :is="SEARCH_ICONS[name]" class="hp-search-icon" size="1em" :stroke-width="2" aria-hidden="true" />
</template>

<style scoped>
.hp-search-icon {
  flex: none;
}
</style>
