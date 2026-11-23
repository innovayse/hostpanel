<script setup lang="ts">
/**
 * The table of a list screen: the glass card, the table and its pager in one component.
 *
 * Every list draws its table the same way, so no screen frames a `UiTable` itself: it passes the head and body rows in the
 * default slot, the paging state through `page`, `total` and `pageSize`, and the loading and empty states through
 * `loading`, `empty` and `emptyText` (or the `empty` slot for an empty state with a description or an action). The card
 * clips the table at its rounded corners and the pager sits in the card's footer, so a list never has a separate count
 * line or pager under it. The pager (`UiPagination`) draws the "x to y of n" range itself and renders nothing for a
 * single page. A list that is not paged leaves `page` out and gets no footer.
 *
 * The attributes a screen sets (the test id) go to the table; the card around it only frames it. `header` is the card's
 * title row, for a table that is one block of a larger screen; the table pads it and rules it off like the footer, so a
 * screen passes only the heading.
 */
import UiCard from '@/components/ui/UiCard.vue'
import UiPagination from '@/components/ui/UiPagination.vue'
import UiTable from '@/components/ui/UiTable.vue'
import type { RowSelection } from '@/composables/useRowSelection'
import { UiTableDensity } from '@/utils/ui/injection'
import type { UiTableMaxHeight } from '@/utils/ui/table'

/** The attributes a screen sets belong on the table, not on the card around it. */
defineOptions({ inheritAttrs: false })

/** Props for UiDataTable. */
withDefaults(defineProps<{
  /** Padding of the cells; comfortable for a list, compact for a table that is a block of a larger screen. */
  density?: UiTableDensity
  /** Draws placeholder rows under the real header while the data loads. */
  loading?: boolean
  /** Shows the empty state under the header (set when there are no rows). */
  empty?: boolean
  /** Message of the empty state; the translated "No data" when omitted. */
  emptyText?: string
  /** Whether the table scrolls inside its own box; off for a table whose cells open dropdowns. */
  scrollable?: boolean
  /** Height past which the box scrolls vertically and the header sticks to it. */
  maxHeight?: UiTableMaxHeight
  /** Makes the table selectable: the selection its checkbox column reads and changes. */
  selection?: RowSelection
  /** `data-testid` of the select-all checkbox of a selectable table. */
  selectAllTestid?: string
  /** Current page, starting at 1; the card gets its pager only when this is set. */
  page?: number
  /** Total number of rows over all pages. */
  total?: number
  /** Rows per page. */
  pageSize?: number
  /** Page sizes the reader can pick from; the rows-per-page select is shown only when this is set. */
  pageSizeOptions?: number[]
  /** `data-testid` prefix of the pager. */
  paginationTestid?: string
}>(), {
  density: UiTableDensity.Comfortable,
  loading: false,
  empty: false,
  emptyText: undefined,
  scrollable: true,
  maxHeight: undefined,
  selection: undefined,
  selectAllTestid: undefined,
  page: undefined,
  total: 0,
  pageSize: 0,
  pageSizeOptions: undefined,
  paginationTestid: undefined,
})

defineEmits<{
  /** Emitted with the page the reader chose. */
  'update:page': [page: number]
  /** Emitted with the page size the reader chose; the screen returns to the first page. */
  'update:pageSize': [pageSize: number]
}>()
</script>

<template>
  <UiCard clip>
    <template v-if="$slots.header" #header>
      <div class="border-b border-border px-4 py-3" data-testid="ui-data-table-header">
        <slot name="header" />
      </div>
    </template>
    <UiTable
      v-bind="$attrs"
      :density="density"
      :loading="loading"
      :empty="empty"
      :empty-text="emptyText"
      :scrollable="scrollable"
      :max-height="maxHeight"
      :selection="selection"
      :select-all-testid="selectAllTestid"
    >
      <slot />
      <template v-if="$slots.empty" #empty>
        <slot name="empty" />
      </template>
    </UiTable>
    <template v-if="page !== undefined" #footer>
      <UiPagination
        :page="page"
        :total="total"
        :page-size="pageSize"
        :page-size-options="pageSizeOptions"
        :loading="loading"
        :testid="paginationTestid"
        @update:page="$emit('update:page', $event)"
        @update:page-size="$emit('update:pageSize', $event)"
      />
    </template>
  </UiCard>
</template>
