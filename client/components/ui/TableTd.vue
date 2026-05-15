<template>
  <td :class="['p-4 text-sm', alignClass]">
    <slot />
  </td>
</template>

<script setup lang="ts">
/** Data cell with standard padding. Use `class` attribute for color/weight overrides. */
const props = defineProps<{
  /** Text alignment (default: none — inherits or set via class) */
  align?: 'left' | 'center' | 'right'
}>()

const alignClass = computed(() => {
  if (props.align === 'center') return 'text-center'
  if (props.align === 'right') return 'text-right'
  return ''
})
</script>
