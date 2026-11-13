<template>
  <div class="overflow-hidden rounded-2xl border border-gray-200 dark:border-white/10 bg-ui-surface">
    <div
      :class="scrollable ? 'overflow-x-auto focus:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-primary-500' : undefined"
      :tabindex="scrollable && label ? 0 : undefined"
      :role="scrollable && label ? 'region' : undefined"
      :aria-label="scrollable ? label : undefined"
    >
      <table class="w-full">
        <slot />
      </table>
    </div>
  </div>
</template>

<script setup lang="ts">
/**
 * UiTable: the framed table shell; compose it with TableHead, TableBody, TableRow, TableTh and
 * TableTd (`UiTableHead` and the rest). The frame clips the corners; `scrollable` lets wide tables scroll
 * sideways inside it.
 */

/** Props of UiTable. */
interface Props {
  /** Allows horizontal scrolling inside the frame. */
  scrollable?: boolean
  /**
   * Accessible name of the scroll area. When set on a scrollable table the area becomes a focus
   * stop, which is what lets a keyboard reader scroll a table wider than the screen.
   */
  label?: string
}

withDefaults(defineProps<Props>(), {
  scrollable: false,
  label: undefined,
})
</script>
