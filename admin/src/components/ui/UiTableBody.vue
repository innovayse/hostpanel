<script setup lang="ts">
/**
 * Table body group (`<tbody>`).
 *
 * `divided` draws a hairline between rows from the border token. Inside a loading `UiTable` it renders nothing,
 * because the table draws its own placeholder rows then. Below `sm` the rows are cards, so the dividers go.
 */
import { inject } from 'vue'
import { UI_TABLE_CONTEXT } from '@/utils/ui/injection'
import { cardTableClasses } from '@/utils/ui/table'

/** Props for UiTableBody. */
withDefaults(defineProps<{
  /** Draws a divider between rows. */
  divided?: boolean
}>(), {
  divided: false,
})

/** The table around this body, absent when the body is mounted alone. */
const table = inject(UI_TABLE_CONTEXT, null)
</script>

<template>
  <tbody v-if="!table?.loading" role="rowgroup" :class="[cardTableClasses, divided ? 'divide-y divide-border-subtle max-sm:divide-y-0' : '']"><slot /></tbody>
</template>
