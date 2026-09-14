<script setup lang="ts">
/**
 * Reusable toggle switch component matching the admin panel theme.
 * Replaces the repeated toggle button + inner div pattern used across forms.
 */

/** Props for UiToggleSwitch. */
withDefaults(defineProps<{
  /** Current toggle state. */
  modelValue: boolean
  /**
   * When true, the switch is shown dimmed and does not respond to clicks.
   * Use this for a value the caller cannot change here — e.g. the base
   * currency's "enabled" state — rather than hiding the control outright, so
   * its current state stays visible.
   */
  disabled?: boolean
}>(), {
  disabled: false
})

const emit = defineEmits<{
  /** Emitted when the toggle is clicked. Never fires while disabled. */
  'update:modelValue': [value: boolean]
}>()
</script>

<template>
  <button
    type="button"
    class="w-9 h-5 rounded-full transition-colors duration-200 flex items-center px-0.5 shrink-0"
    :class="[
      modelValue ? 'bg-primary-500' : 'bg-border',
      disabled ? 'opacity-40 cursor-not-allowed' : ''
    ]"
    :disabled="disabled"
    :aria-disabled="disabled"
    @click="!disabled && emit('update:modelValue', !modelValue)"
  >
    <div
      class="w-4 h-4 rounded-full bg-white shadow transition-transform duration-200"
      :class="modelValue ? 'translate-x-[14px]' : 'translate-x-0'"
    />
  </button>
</template>
