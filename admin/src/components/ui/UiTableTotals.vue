<script lang="ts">
/** One line of a totals block: its caption, the amount already formatted and how it is stressed. */
export interface UiTotalRow {
  /** Stable key of the line. */
  key: string
  /** Caption of the line (already translated). */
  label: string
  /** The amount, already formatted for display. */
  value: string
  /** True for the closing line (total due): primary, semibold text instead of the muted one. */
  strong?: boolean
  /** `data-testid` of the amount cell. */
  testid?: string
}
</script>

<script setup lang="ts">
/**
 * The totals of a table of line items (sub-total, tax, credit, total due), drawn as the rows of the table's own foot.
 *
 * Every line is a row of `UiTableFoot`: its caption spans the columns before the amount column (the table says which
 * one that is, so the span follows the real columns, a select column included) and its figure sits in the amount column
 * itself, so the figures line up with the amounts above them by construction, in every table that draws its lines with
 * the kit (an invoice, a quote, a credit note). Columns after the amount column (the row actions) are left without a
 * cell. On a phone, where the table is a list of cards, a line is the caption at the start and the figure at the end, with
 * no column caption. Place it as the last child of a `UiTable`.
 */
import { computed, inject } from 'vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableFoot from '@/components/ui/UiTableFoot.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import { UI_TABLE_CONTEXT } from '@/utils/ui/injection'

/** Props for UiTableTotals. */
defineProps<{
  /** The lines of the block, top to bottom. */
  rows: UiTotalRow[]
}>()

/** The table around the block. */
const table = inject(UI_TABLE_CONTEXT, null)

/** Number of columns before the amount column, which the caption spans. */
const span = computed<number>(() => Math.max(table?.amountColumn ?? 1, 1))
</script>

<template>
  <UiTableFoot>
    <template v-if="table?.phone">
      <div
        v-for="row in rows"
        :key="row.key"
        class="flex items-center justify-between gap-3 px-1 text-sm tabular-nums"
        :class="row.strong ? 'font-semibold text-text-primary' : 'text-text-muted'"
      >
        <span>{{ row.label }}</span>
        <span :data-testid="row.testid">{{ row.value }}</span>
      </div>
    </template>
    <template v-else>
      <UiTableRow v-for="row in rows" :key="row.key">
        <UiTableCell numeric :colspan="span" :muted="!row.strong" :strong="row.strong">{{ row.label }}</UiTableCell>
        <UiTableCell numeric :muted="!row.strong" :strong="row.strong" :data-testid="row.testid">{{ row.value }}</UiTableCell>
      </UiTableRow>
    </template>
  </UiTableFoot>
</template>
