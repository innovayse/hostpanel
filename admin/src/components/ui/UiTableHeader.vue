<script setup lang="ts">
/**
 * Table header cell (`<th>`).
 *
 * Typography is the kit default (12px semibold muted caption, sentence case) at zero specificity, so a screen
 * can still override a single property. `sortable` turns the caption into a button that emits
 * `sort`; the parent owns the order and passes the current `direction`, which is mirrored into
 * `aria-sort` and an arrow. `numeric` and `actions` match the cells below them (right aligned, the
 * actions column at its one fixed width); `hideBelow` hides the column under a breakpoint. The cell paints the one hairline under the header (an inset
 * shadow, because a border does not travel with a sticky header in a collapsed-border table); the fill of a
 * sticky header comes from the scrolling box (`UiTable` with `maxHeight`). `select` marks the checkbox column of a selectable table
 * (drawn by `UiTableRow`, not set by a screen); a header that holds the select-all checkbox itself (an editable table) says `select` too.
 * The caption is also the label of the column's value on a card (below `sm`, see `UiTable`), where `cardRole` sets what the column is
 * (the heading, the top right badge, a field, a control or left out; a `hideBelow` column is left out unless it says otherwise); the header
 * itself is not drawn there, except a `select` one. The caption text is read from a `display: contents` span around the slot, because a
 * slot can hold any markup, and a sortable column also hands the table its `sort` request, so the table's sort control can order it.
 *
 * The row hands each header its position (`columnIndex`), counted over the row's cells at render time, and the header registers its
 * column with the table at that position.
 */
import { computed, inject, onBeforeUnmount, onMounted, onUpdated, ref, watch } from 'vue'
import { UI_TABLE_CONTEXT, UiTableDensity } from '@/utils/ui/injection'
import type { UiTableColumn } from '@/utils/ui/injection'
import { columnClasses, headerDensityClasses, UiSortDirection } from '@/utils/ui/table'
import type { UiCardRole } from '@/utils/ui/table'
import type { UiAlign, UiHideBelow } from '@/utils/ui/table'

/** Props for UiTableHeader. */
const props = withDefaults(defineProps<{
  /** Horizontal alignment (left when omitted). */
  align?: UiAlign
  /** Makes the caption a sort button. */
  sortable?: boolean
  /** Current order of this column. */
  direction?: UiSortDirection
  /** `data-testid` of the sort button (the `<th>` keeps the inherited one). */
  sortTestid?: string
  /** Heads a number or money column: right aligned like its cells. */
  numeric?: boolean
  /** Heads the row actions column: right aligned at the shared actions column width. */
  actions?: boolean
  /** Hides the column below this breakpoint; set it on the header and on every cell of the column. */
  hideBelow?: UiHideBelow
  /** Heads the checkbox column of a selectable table: narrow, like its cells. */
  select?: boolean
  /** What the column is on a card (below `sm`): the heading, the badge, a labelled field, a control or left out. A field when omitted, left out when `hideBelow` is set. */
  cardRole?: UiCardRole
  /** Position of the header among the cells of its row; set by `UiTableRow`, never by a screen. */
  columnIndex?: number
}>(), {
  cardRole: undefined,
  columnIndex: undefined,
  hideBelow: undefined,
  select: false,
  numeric: false,
  actions: false,
  align: undefined,
  sortable: false,
  direction: UiSortDirection.None,
  sortTestid: undefined,
})

const emit = defineEmits<{
  /** Emitted when the user activates the sort button. */
  sort: []
}>()

/** Density shared by the parent table (plain when used outside one). */
const context = inject(UI_TABLE_CONTEXT, { density: UiTableDensity.Plain })

/** The element around the caption, read for the label the column's values get on a card. */
const captionElement = ref<HTMLElement | null>(null)

/** The caption as text (the sort arrow is not part of it). */
const label = ref<string>('')

/** Reads the caption from the rendered header. */
const readLabel = (): void => {
  label.value = captionElement.value?.textContent?.trim() ?? ''
}

/** Takes the caption once the header is in the page. */
onMounted(() => {
  readLabel()
})

/** Takes the caption again after a re-render, so a new language or a changed slot reaches the labels of the cards. */
onUpdated(() => {
  readLabel()
})

/** Asks the screen to sort by this column: the request of the sort button, also made by the sort control of a card layout. */
const requestSort = (): void => {
  emit('sort')
}

/** The column this header declares: its cells and the loading skeleton share its shape and visibility. */
const column = computed<UiTableColumn>(() => ({
  align: props.align,
  numeric: props.numeric,
  actions: props.actions,
  hideBelow: props.hideBelow,
  select: props.select,
  label: label.value,
  cardRole: props.cardRole,
  sortable: props.sortable,
  direction: props.direction,
  sort: requestSort,
}))

/** Removes the registration of the position this header holds now. */
let unregisterColumn: (() => void) | undefined

// Registered at the position the row gave it, again when that position moves (a header before it appears or goes).
watch(
  () => props.columnIndex,
  (index) => {
    unregisterColumn?.()
    unregisterColumn = index === undefined ? undefined : context.registerColumn?.(index, () => column.value)
  },
  { immediate: true },
)

onBeforeUnmount(() => {
  unregisterColumn?.()
})

/** Padding and alignment classes; below `sm` only the select-all cell is drawn. */
const classes = computed<string[]>(() => [
  headerDensityClasses[context.density],
  props.select ? '' : 'max-sm:hidden',
  ...columnClasses(column.value),
])

/** Arrow glyph of the current order. */
const arrow = computed<string>(() => {
  if (props.direction === UiSortDirection.Ascending) {
    return '▲'
  }
  return props.direction === UiSortDirection.Descending ? '▼' : '↕'
})
</script>

<template>
  <th
    role="columnheader"
    :aria-sort="sortable ? direction : undefined"
    class="shadow-[inset_0_-1px_0_var(--border-subtle)] [:where(&)]:text-xs [:where(&)]:font-semibold [:where(&)]:text-text-muted"
    :class="classes"
  >
    <button
      v-if="sortable"
      type="button"
      class="inline-flex items-center gap-1 hover:text-text-primary transition-colors cursor-pointer rounded-md focus-visible:outline-2 focus-visible:outline-primary-500"
      :data-testid="sortTestid"
      @click="requestSort"
    >
      <span ref="captionElement" class="contents"><slot /></span>
      <span aria-hidden="true">{{ arrow }}</span>
    </button>
    <span v-else ref="captionElement" class="contents"><slot /></span>
  </th>
</template>
