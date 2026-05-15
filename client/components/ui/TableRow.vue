<template>
  <tr
    :class="[
      'transition-colors',
      hoverable !== false && 'hover:bg-gray-50 dark:hover:bg-white/5',
      group && 'group'
    ]"
  >
    <slot />
  </tr>
</template>

<script setup lang="ts">
defineProps<{ hoverable?: boolean; group?: boolean }>()
</script>
