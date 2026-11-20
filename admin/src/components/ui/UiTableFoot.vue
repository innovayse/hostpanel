<script setup lang="ts">
/**
 * Table foot group (`<tfoot>`, totals row): set apart from the rows by a hairline above and a semibold weight.
 * Attributes fall through to the native element. Inside a loading `UiTable` it renders nothing, and on a phone only its slot (its rows are cards).
 */
import { inject } from 'vue'
import { UI_TABLE_CONTEXT } from '@/utils/ui/injection'

/** The table around this foot, absent when the foot is mounted alone. */
const table = inject(UI_TABLE_CONTEXT, null)
</script>

<template>
  <slot v-if="table?.phone && !table.loading" />
  <tfoot v-else-if="!table?.loading" role="rowgroup" class="border-t border-border-subtle text-sm font-semibold"><slot /></tfoot>
</template>
