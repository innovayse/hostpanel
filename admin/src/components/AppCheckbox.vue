<script setup lang="ts">
defineProps<{
  modelValue: boolean
  label?: string
  disabled?: boolean
}>()

defineEmits<{
  'update:modelValue': [value: boolean]
}>()
</script>

<template>
  <label class="flex items-center gap-2 cursor-pointer">
    <input
      type="checkbox"
      :checked="modelValue"
      :disabled="disabled"
      @change="(e) => $emit('update:modelValue', (e.target as HTMLInputElement).checked)"
      class="w-4 h-4 rounded border border-border bg-white/[0.05] text-primary-500 cursor-pointer disabled:opacity-50 disabled:cursor-not-allowed focus:ring-2 focus:ring-primary-500/30 focus:ring-offset-0 transition-colors"
    />
    <span v-if="label" class="text-[0.82rem] text-text-primary">{{ label }}</span>
  </label>
</template>
