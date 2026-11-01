<script setup lang="ts">
/**
 * Data table root.
 *
 * Draws the `<table>` inside a horizontal scroller, so a table wider than its container scrolls
 * on its own instead of stretching the page; every screen gets that without its own wrapper.
 * Classes and attributes (`data-testid`, ...) land on the `<table>`. It shares its `density` with the header and data cells (set the padding
 * once here instead of on every cell). `loading` and `empty` append a full-width status row, so a
 * screen needs no separate "no rows" markup; the `empty` slot replaces the default message.
 */
import { provide } from 'vue'
import { useI18n } from 'vue-i18n'
import UiSkeleton from '@/components/ui/UiSkeleton.vue'
import { UI_TABLE_CONTEXT } from '@/utils/ui/injection'
import type { UiTableDensity } from '@/utils/ui/injection'

/** The root is the scroller; a caller's classes and attributes belong on the table itself. */
defineOptions({ inheritAttrs: false })

const { t } = useI18n()

/** Props for UiTable. */
const props = withDefaults(defineProps<{
  /** Padding of the cells; `comfortable` by default so a table never renders without spacing, `plain` leaves it to the screen's classes. */
  density?: UiTableDensity
  /** Shows skeleton lines under the rows while data loads. */
  loading?: boolean
  /** Shows the empty message under the header (set when there are no rows). */
  empty?: boolean
  /** Number of columns, for the width of the status row. */
  colspan?: number
}>(), {
  density: 'comfortable',
  loading: false,
  empty: false,
  colspan: 100,
})

// A getter keeps the shared density reactive without handing parts a ref they must unwrap.
provide(UI_TABLE_CONTEXT, {
  get density() {
    return props.density
  },
})
</script>

<template>
  <div class="max-w-full overflow-x-auto" data-testid="ui-table-scroller">
    <table class="[:where(&)]:w-full" v-bind="$attrs">
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
