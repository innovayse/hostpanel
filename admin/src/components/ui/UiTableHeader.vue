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
 * (drawn by `UiTableRow`, not set by a screen).
 */
import { computed, inject, onBeforeUnmount } from 'vue'
import { UI_TABLE_CONTEXT, UiTableDensity } from '@/utils/ui/injection'
import type { UiTableColumn } from '@/utils/ui/injection'
import { columnClasses, headerDensityClasses, UiSortDirection } from '@/utils/ui/table'
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
}>(), {
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

/** The column this header declares: its cells and the loading skeleton share its shape and visibility. */
const column = computed<UiTableColumn>(() => ({
  align: props.align,
  numeric: props.numeric,
  actions: props.actions,
  hideBelow: props.hideBelow,
  select: props.select,
}))

// Registered in render order so the skeleton rows get one cell per header, with the same classes.
const unregisterColumn = context.registerColumn?.(() => column.value)

onBeforeUnmount(() => {
  unregisterColumn?.()
})

/** Padding and alignment classes. */
const classes = computed<string[]>(() => [
  headerDensityClasses[context.density],
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
    :aria-sort="sortable ? direction : undefined"
    class="shadow-[inset_0_-1px_0_var(--border-subtle)] [:where(&)]:text-xs [:where(&)]:font-semibold [:where(&)]:text-text-muted"
    :class="classes"
  >
    <button
      v-if="sortable"
      type="button"
      class="inline-flex items-center gap-1 hover:text-text-primary transition-colors cursor-pointer rounded-md focus-visible:outline-2 focus-visible:outline-primary-500"
      :data-testid="sortTestid"
      @click="emit('sort')"
    >
      <slot />
      <span aria-hidden="true">{{ arrow }}</span>
    </button>
    <slot v-else />
  </th>
</template>
