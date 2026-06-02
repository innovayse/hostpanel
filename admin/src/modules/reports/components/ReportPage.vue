<script setup lang="ts">
defineProps<{
  title: string
  description?: string
  loading?: boolean
  error?: string | null
}>()
</script>

<template>
  <div class="p-6 w-full">
    <div class="mb-6">
      <h1 class="font-display font-bold text-[1.25rem] text-text-primary">{{ title }}</h1>
      <p v-if="description" class="text-[0.78rem] text-text-secondary mt-1">{{ description }}</p>
    </div>

    <slot name="filters" />

    <div v-if="loading" class="flex items-center gap-3 text-text-secondary text-sm py-8">
      <span class="w-4 h-4 rounded-full border-2 border-primary-500/20 border-t-primary-500 animate-spin" />
      Loading...
    </div>
    <div v-else-if="error" class="text-sm text-status-red bg-status-red/8 border border-status-red/20 rounded-xl p-4">
      {{ error }}
    </div>
    <slot v-else />
  </div>
</template>
