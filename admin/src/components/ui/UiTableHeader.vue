<script setup lang="ts">
/**
 * Table header cell (`<th>`).
 *
 * Typography is the kit default (small uppercase muted caption) at zero specificity, so a screen
 * can still override a single property. `sortable` turns the caption into a button that emits
 * `sort`; the parent owns the order and passes the current `direction`, which is mirrored into
 * `aria-sort` and an arrow.
 */
import { computed, inject } from 'vue'
import { UI_TABLE_CONTEXT, UiTableDensity } from '@/utils/ui/injection'
import { UiAlign, alignClasses, headerDensityClasses, UiSortDirection } from '@/utils/ui/table'

/** Props for UiTableHeader. */
const props = withDefaults(defineProps<{
  /** Horizontal alignment; omitted leaves it to the screen. */
  align?: UiAlign
  /** Makes the caption a sort button. */
  sortable?: boolean
  /** Current order of this column. */
  direction?: UiSortDirection
  /** `data-testid` of the sort button (the `<th>` keeps the inherited one). */
  sortTestid?: string
}>(), {
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

/** Padding and alignment classes. */
const classes = computed<string[]>(() => [
  headerDensityClasses[context.density],
  alignClasses[props.align ?? UiAlign.Left],
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
    class="[:where(&)]:text-[0.68rem] [:where(&)]:font-semibold [:where(&)]:uppercase [:where(&)]:tracking-[0.08em] [:where(&)]:min-w-28 [:where(&)]:text-text-secondary"
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
