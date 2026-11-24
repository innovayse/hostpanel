<template>
  <div :class="['overflow-hidden', !flush && 'rounded-2xl border border-gray-200 dark:border-white/10 bg-ui-surface']">
    <div
      :class="scrollable ? 'overflow-x-auto focus:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-primary-500' : undefined"
      :tabindex="scrollable && label ? 0 : undefined"
      :role="scrollable && label ? 'region' : undefined"
      :aria-label="scrollable ? label : undefined"
    >
      <table class="w-full">
        <slot name="head" />

        <!-- Placeholder rows: one skeleton per column, drawn instead of the body while the rows are read. -->
        <UiTableBody v-if="loading" aria-busy="true" data-testid="ui-table-loading">
          <UiTableRow v-for="row in LOADING_ROWS" :key="row" :hoverable="false">
            <UiTableTd v-for="column in columns" :key="column">
              <UiSkeleton />
            </UiTableTd>
          </UiTableRow>
        </UiTableBody>

        <!-- Nothing to list: one full-width row carrying the kit empty state. -->
        <UiTableBody v-else-if="empty" data-testid="ui-table-empty">
          <UiTableRow :hoverable="false">
            <UiTableTd :colspan="columns">
              <UiEmptyState :title="emptyText ?? ''" :description="emptyDescription" :icon="emptyIcon">
                <template v-if="$slots.empty" #default>
                  <slot name="empty" />
                </template>
              </UiEmptyState>
            </UiTableTd>
          </UiTableRow>
        </UiTableBody>

        <slot v-else />
      </table>
    </div>
  </div>
</template>

<script setup lang="ts">
/**
 * UiTable: the framed table shell; compose it with TableHead, TableBody, TableRow, TableTh and
 * TableTd (`UiTableHead` and the rest). The frame clips the corners; `scrollable` lets wide tables scroll
 * sideways inside it; `flush` drops the frame for a table that sits inside a section or card of its own.
 *
 * Put the header in the `head` slot and the body in the default slot. While `loading` the body is
 * replaced by placeholder rows, and when `empty` by one full-width row with the empty state, so a
 * screen never draws those states by hand. A table that needs neither can keep its header and body in the default slot.
 */
import type { Component } from 'vue'

/** How many placeholder rows the loading state draws. */
const LOADING_ROWS = 4

/** Props of UiTable. */
interface Props {
  /** Allows horizontal scrolling inside the frame. */
  scrollable?: boolean
  /**
   * Accessible name of the scroll area. When set on a scrollable table the area becomes a focus
   * stop, which is what lets a keyboard reader scroll a table wider than the screen.
   */
  label?: string
  /** Drops the frame (border, radius, background) for a table embedded in a section that frames it already. */
  flush?: boolean
  /** Replaces the body with placeholder rows while the data is read. Wins over `empty`. */
  loading?: boolean
  /** Replaces the body with the empty state; the screen sets it when there is no row to list. */
  empty?: boolean
  /** Headline of the empty state. */
  emptyText?: string
  /** Supporting sentence of the empty state. */
  emptyDescription?: string
  /** Lucide icon of the empty state. */
  emptyIcon?: Component
  /** Number of header columns: how many placeholder cells a loading row has and how wide the empty row is. */
  columns?: number
}

withDefaults(defineProps<Props>(), {
  scrollable: false,
  label: undefined,
  flush: false,
  loading: false,
  empty: false,
  emptyText: undefined,
  emptyDescription: undefined,
  emptyIcon: undefined,
  columns: 1,
})
</script>
