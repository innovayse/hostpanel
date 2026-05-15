<template>
  <div>
    <dt class="text-xs font-semibold text-gray-400 dark:text-gray-500 uppercase tracking-wide mb-1">
      {{ label }}
    </dt>
    <dd class="text-gray-900 dark:text-white text-sm" :class="valueClass">
      <slot>{{ value ?? '—' }}</slot>
    </dd>
  </div>
</template>

<script setup lang="ts">
interface Props {
  label: string
  value?: string | number | null
  valueClass?: string
}

withDefaults(defineProps<Props>(), { valueClass: '' })
</script>
