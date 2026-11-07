<script setup lang="ts">
/**
 * Data table root.
 *
 * Draws the `<table>` and shares its `density` with the header and data cells (set the padding
 * once here instead of on every cell). `loading` and `empty` append a full-width status row, so a
 * screen needs no separate "no rows" markup; the `empty` slot replaces the default message. The table sits in a
 * box that scrolls sideways when the columns are wider than the screen. That box clips anything that opens
 * below a cell, so an editable table whose cells hold a dropdown turns it off with `scrollable="false"`.
 */
import { provide } from 'vue'
import { useI18n } from 'vue-i18n'
import UiSkeleton from '@/components/ui/UiSkeleton.vue'
import { UI_TABLE_CONTEXT, UiTableDensity } from '@/utils/ui/injection'

/** The attributes a screen sets (classes, test ids) belong on the table; the box around it only scrolls. */
defineOptions({ inheritAttrs: false })

const { t } = useI18n()

/** Props for UiTable. */
const props = withDefaults(defineProps<{
  /** Padding of the cells (comfortable by default so header and cells always share one padding); `plain` leaves it to the screen's classes. */
  density?: UiTableDensity
  /** Shows skeleton lines under the rows while data loads. */
  loading?: boolean
  /** Shows the empty message under the header (set when there are no rows). */
  empty?: boolean
  /** Number of columns, for the width of the status row. */
  colspan?: number
  /** Whether the table scrolls sideways inside its own box; off for a table whose cells open dropdowns. */
  scrollable?: boolean
}>(), {
  density: UiTableDensity.Comfortable,
  loading: false,
  empty: false,
  colspan: 100,
  scrollable: true,
})

// A getter keeps the shared density reactive without handing parts a ref they must unwrap.
provide(UI_TABLE_CONTEXT, {
  get density() {
    return props.density
  },
})
</script>

<template>
  <div :class="scrollable ? 'overflow-x-auto' : ''">
    <table v-bind="$attrs" class="[:where(&)]:w-full">
      <slot />
      <tbody v-if="loading || empty">
        <tr>
          <td :colspan="colspan" class="py-8 px-4 text-center text-[0.82rem] text-text-secondary">
            <UiSkeleton v-if="loading" :lines="3" />
            <slot v-else name="empty">
              {{ t('ui.table.empty') }}
            </slot>
          </td>
        </tr>
      </tbody>
    </table>
  </div>
</template>
