<template>
  <th :class="['p-4 text-xs font-semibold text-gray-600 dark:text-gray-400 uppercase tracking-wider', alignClass]">
    <slot>{{ actions ? $t('common.actions') : '' }}</slot>
  </th>
</template>

<script setup lang="ts">
/**
 * TableTh (`UiTableTh`). A header cell: small uppercase muted text, left-aligned by default. `actions` heads the row
 * actions column: right aligned and captioned "Actions" by default, so no table has a nameless actions column.
 */
import { computed } from 'vue'
import { UiAlign } from '~/utils/ui/enums'

/** Props of TableTh. */
interface Props {
  /** Text alignment; defaults to left. */
  align?: `${UiAlign}`
  /** Heads the row actions column: right aligned, with the shared "Actions" caption when the slot is empty. */
  actions?: boolean
}

/** Props of the component with their defaults applied. */
const props = withDefaults(defineProps<Props>(), {
  align: undefined,
  actions: false,
})

/** Alignment utility for the chosen `align`. */
const alignClass = computed(() => {
  if (props.actions) {
    return 'text-right'
  }
  if (props.align === UiAlign.Center) {
    return 'text-center'
  }
  if (props.align === UiAlign.Right) {
    return 'text-right'
  }
  return 'text-left'
})
</script>
