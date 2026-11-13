<script setup lang="ts">
/**
 * Table head group (`<thead>`). Sticks to the top of the nearest scroll container (the table's own box under `maxHeight`, or the page itself for a
 * `scrollable="false"` table), so the column captions stay in view while a long table scrolls under them (the hairline is drawn by `UiTableHeader`,
 * because a border on a `<thead>` does not travel with a sticky header in a collapsed-border table, and
 * the frosted fill by the scrolling box). Attributes fall through to the native element. It tells the rows inside it that they are header rows, so on a selectable
 * table a row draws the select-all cell here and a row checkbox in the body.
 */
import { provide } from 'vue'
import { UI_TABLE_HEAD } from '@/utils/ui/injection'

provide(UI_TABLE_HEAD, true)
</script>

<template>
  <thead class="sticky top-0 z-10"><slot /></thead>
</template>
