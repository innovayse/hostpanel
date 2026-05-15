<template>
  <div class="flex items-center justify-between mb-5">
    <h2 class="text-base font-bold text-gray-900 dark:text-white flex items-center gap-2">
      <component v-if="icon" :is="icon" :size="18" :stroke-width="2" class="text-gray-400 dark:text-gray-500 flex-shrink-0" />
      {{ title }}
    </h2>
    <slot />
  </div>
</template>

<script setup lang="ts">
import type { Component } from 'vue'
defineProps<{ title: string; icon?: Component }>()
</script>
