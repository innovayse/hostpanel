<script setup lang="ts">
/**
 * The loading skeleton of a table described by a schema: the real header captions over placeholder rows, and the
 * pager under them when the schema has one. Each column takes the width the schema gives it, and a grouped table opens with
 * the placeholder of a group heading, as the real table does.
 *
 * It is `UiTable` in its `loading` state, so the rows take the column widths, visibility and card shape the real table
 * has; the captions are the schema's column labels, which are page text and stay while the rows load. The pager sits
 * under the table with the strip a card's `footer` slot gives it, so a screen puts both in a `UiCard clip`.
 */
import { useI18n } from 'vue-i18n'
import UiPagination from '@/components/ui/UiPagination.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import type { SkeletonTable } from '@/utils/ui/skeletonSchema'

const { t } = useI18n()

/** Props for UiSkeletonTable. */
defineProps<{
  /** The table of the screen's schema. */
  table: SkeletonTable
}>()
</script>

<template>
  <UiTable loading :skeleton-rows="table.rows" :skeleton-group="table.grouped" :scrollable="false">
    <UiTableHead>
      <UiTableRow>
        <UiTableHeader v-for="column in table.columns" :key="column.key" :align="column.align" :size="column.size">
          {{ column.label ? t(column.label) : '' }}
        </UiTableHeader>
      </UiTableRow>
    </UiTableHead>
  </UiTable>
  <UiPagination v-if="table.pagination" loading :page="1" :total="1" :page-size="1" class="border-t border-border px-4 py-3" />
</template>
