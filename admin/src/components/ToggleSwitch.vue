<script setup lang="ts">
/**
 * Reusable toggle switch component matching the admin panel theme.
 * Replaces the repeated toggle button + inner div pattern used across forms.
 */

/** Props for ToggleSwitch. */
defineProps<{
  /** Current toggle state. */
  modelValue: boolean
}>()

const emit = defineEmits<{
  /** Emitted when the toggle is clicked. */
  'update:modelValue': [value: boolean]
}>()
</script>

<template>
  <button
    type="button"
    class="w-9 h-5 rounded-full transition-colors duration-200 flex items-center px-0.5 shrink-0"
    :class="modelValue ? 'bg-primary-500' : 'bg-border'"
    @click="emit('update:modelValue', !modelValue)"
  >
    <div
      class="w-4 h-4 rounded-full bg-white shadow transition-transform duration-200"
      :class="modelValue ? 'translate-x-4' : 'translate-x-0'"
    />
  </button>
</template>
