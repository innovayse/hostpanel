<script setup lang="ts">
/**
 * Table body group (`<tbody>`).
 *
 * `divided` draws a hairline between rows from the border token. Inside a loading `UiTable` it renders nothing,
 * because the table draws its own placeholder rows then.
 */
import { inject } from 'vue'
import { UI_TABLE_CONTEXT } from '@/utils/ui/injection'

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
  <tbody v-if="!table?.loading" :class="divided ? 'divide-y divide-border-subtle' : ''"><slot /></tbody>
</template>
