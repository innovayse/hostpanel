<template>
  <div class="overflow-hidden rounded-2xl border border-gray-200 dark:border-white/10 bg-white dark:bg-[#0d0d12]">
    <div :class="scrollable ? 'overflow-x-auto' : undefined">
      <table class="w-full">
        <slot />
      </table>
    </div>
  </div>
</template>

<script setup lang="ts">
defineProps<{ scrollable?: boolean }>()
</script>
