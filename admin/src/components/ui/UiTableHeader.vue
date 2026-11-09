<script setup lang="ts">
/**
 * Table header cell (`<th>`).
 *
 * Typography is the kit default (small uppercase muted caption) at zero specificity, so a screen
 * can still override a single property. `sortable` turns the caption into a button that emits
 * `sort`; the parent owns the order and passes the current `direction`, which is mirrored into
 * `aria-sort` and an arrow. `numeric` and `actions` match the cells below them (right aligned, the
 * actions column at its one fixed width).
 */
import { computed, inject } from 'vue'
import { UI_TABLE_CONTEXT, UiTableDensity } from '@/utils/ui/injection'
import { UiAlign, actionsColumnClasses, alignClasses, headerDensityClasses, numericCellClasses, UiSortDirection } from '@/utils/ui/table'

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
}>(), {
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

/** Alignment classes: the actions and numeric shapes win over `align`. */
const shapeClasses = computed<string>(() => {
  if (props.actions) {
    return actionsColumnClasses
  }
  if (props.numeric) {
    return numericCellClasses
  }
  return alignClasses[props.align ?? UiAlign.Left]
})

/** Padding and alignment classes. */
const classes = computed<string[]>(() => [
  headerDensityClasses[context.density],
  shapeClasses.value,
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
    class="[:where(&)]:text-[0.68rem] [:where(&)]:font-semibold [:where(&)]:uppercase [:where(&)]:tracking-[0.08em] [:where(&)]:text-text-secondary"
    :class="classes"
  >
    <button
      v-if="sortable"
      type="button"
      class="inline-flex items-center gap-1 uppercase hover:text-text-primary transition-colors cursor-pointer rounded-md focus-visible:outline-2 focus-visible:outline-primary-500"
      :data-testid="sortTestid"
      @click="emit('sort')"
    >
      <slot />
      <span aria-hidden="true">{{ arrow }}</span>
    </button>
    <slot v-else />
  </th>
</template>
