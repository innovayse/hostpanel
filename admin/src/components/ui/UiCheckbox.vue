<script setup lang="ts">
/**
 * Custom dark-themed checkbox matching the admin panel design.
 * Replaces native <input type="checkbox"> whose appearance cannot be themed.
 * Unchecked: dark surface with border. Checked: primary-500 fill with white checkmark.
 */

const props = withDefaults(defineProps<{
  modelValue: boolean
  disabled?: boolean
}>(), {
  disabled: false,
})

const emit = defineEmits<{
  'update:modelValue': [value: boolean]
  change: []
}>()

function toggle(): void {
  if (props.disabled) return
  emit('update:modelValue', !props.modelValue)
  emit('change')
}
</script>

<template>
  <button
    type="button"
    role="checkbox"
    :aria-checked="modelValue"
    :disabled="disabled"
    class="w-4 h-4 rounded-[4px] border flex items-center justify-center shrink-0 transition-colors focus:outline-none focus:ring-1 focus:ring-primary-500/20"
    :class="[
      modelValue
        ? 'bg-primary-500 border-primary-500'
        : 'bg-white/[0.04] border-border hover:border-text-muted',
      disabled ? 'opacity-50 cursor-not-allowed' : 'cursor-pointer',
    ]"
    @click="toggle"
  >
    <svg
      v-if="modelValue"
      class="w-2.5 h-2.5 text-white"
      viewBox="0 0 12 10"
      fill="none"
      stroke="currentColor"
      stroke-width="2"
      stroke-linecap="round"
      stroke-linejoin="round"
    >
      <polyline points="1 5.5 4 8.5 11 1.5" />
    </svg>
  </button>
</template>
