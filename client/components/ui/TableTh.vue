<template>
  <th :class="['p-4 text-xs font-semibold text-gray-500 dark:text-gray-400 uppercase tracking-wider', alignClass]">
    <slot />
  </th>
</template>

<script setup lang="ts">
const props = defineProps<{ align?: 'left' | 'center' | 'right' }>()
const alignClass = computed(() => {
  if (props.align === 'center') return 'text-center'
  if (props.align === 'right') return 'text-right'
  return 'text-left'
})
</script>
