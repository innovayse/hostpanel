<template>
  <tr
    :class="[
      'transition-colors',
      hoverable && 'hover:bg-gray-50 dark:hover:bg-white/5',
      group && 'group'
    ]"
  >
    <slot />
  </tr>
</template>

<script setup lang="ts">
/**
 * TableRow (`UiTableRow`): a row of a UiTable with an optional hover highlight and a `group`
 * marker so cells can react to row hover (`group-hover:`).
 */

/** Props of TableRow. */
interface Props {
  /** Highlights the row on hover. */
  hoverable?: boolean
  /** Adds the `group` class so children can use `group-hover:`. */
  group?: boolean
}

withDefaults(defineProps<Props>(), {
  hoverable: true,
  group: false,
})
</script>
