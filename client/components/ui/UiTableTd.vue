<template>
  <td :class="['p-4 text-sm', alignClass]">
    <slot />
  </td>
</template>

<script setup lang="ts">
/**
 * TableTd (`UiTableTd`): a data cell with the standard padding. Use the `class` attribute for
 * colour or weight overrides.
 */
import { computed } from 'vue'
import { UiAlign } from '~/utils/ui/enums'

/** Props of TableTd. */
interface Props {
  /** Text alignment; left when omitted, like the header. */
  align?: `${UiAlign}`
}

/** Props of the component with their defaults applied. */
const props = withDefaults(defineProps<Props>(), {
  align: undefined,
})

/** Alignment utility for the chosen `align`. */
const alignClass = computed(() => {
  if (props.align === UiAlign.Center) {
    return 'text-center'
  }
  if (props.align === UiAlign.Right) {
    return 'text-right'
  }
  return 'text-left'
})
</script>
