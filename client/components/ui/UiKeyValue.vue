<template>
  <div class="flex items-start justify-between gap-4 text-sm" data-testid="ui-key-value">
    <span class="text-gray-600 dark:text-gray-400">{{ label }}</span>
    <span :class="strong ? 'text-gray-900 dark:text-white font-semibold text-right' : 'text-gray-900 dark:text-white font-medium text-right'">
      <slot>{{ value }}</slot>
    </span>
  </div>
</template>

<script setup lang="ts">
/**
 * UiKeyValue: a label on the left and its value on the right, one line of a summary
 * (an order recap, a totals block).
 */

/** Props of UiKeyValue. */
interface Props {
  /** What the value is. */
  label: string
  /** The value; a `default` slot replaces it with richer content. */
  value?: string | number
  /** Emphasises the value (a total). */
  strong?: boolean
}

withDefaults(defineProps<Props>(), {
  value: undefined,
  strong: false,
})
</script>
