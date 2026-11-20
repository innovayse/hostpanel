<script setup lang="ts">
/**
 * Table head group (`<thead>`). Sticks to the top of the nearest scroll container (the table's own box under `maxHeight`, or the page itself for a
 * `scrollable="false"` table), so the column captions stay in view while a long table scrolls under them (the hairline is drawn by `UiTableHeader`,
 * because a border on a `<thead>` does not travel with a sticky header in a collapsed-border table, and
 * the frosted fill by the scrolling box). Attributes fall through to the native element. It tells the rows inside it that they are header rows, so on a selectable
 * table a row draws the select-all cell here and a row checkbox in the body. While the table shows its empty state the captions are hidden (not removed, so
 * the empty row keeps the header's width), since a header over a single message reads as a broken table. On a phone it renders only its slot, so the headers still
 * register their columns for the card list while no header row is drawn.
 */
import { inject, provide } from 'vue'
import { UI_TABLE_CONTEXT, UI_TABLE_HEAD } from '@/utils/ui/injection'

provide(UI_TABLE_HEAD, true)

/** The table around the head, absent when the head stands alone. */
const table = inject(UI_TABLE_CONTEXT, undefined)
</script>

<template>
  <slot v-if="table?.phone" />
  <thead v-else v-show="!table?.empty" role="rowgroup" class="sticky top-0 z-10"><slot /></thead>
</template>
