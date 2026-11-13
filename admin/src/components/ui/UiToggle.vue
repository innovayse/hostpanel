<script setup lang="ts">
/**
 * On/off switch.
 *
 * Replaces the repeated "button + inner knob" markup across forms. It is a real
 * `role="switch"` button, so assistive technology announces the state. `data-testid` and other
 * attributes fall through to the button.
 */

import { focusRingClasses } from '@/utils/ui/input'

/** Props for UiToggle. */
const props = withDefaults(defineProps<{
  /** Current state (two-way bound with `v-model`). */
  modelValue: boolean
  /**
   * When true the switch is shown dimmed and ignores clicks. Use it for a value the caller cannot
   * change here (e.g. the base currency's "enabled" state) rather than hiding the control, so its
   * current state stays visible.
   */
  disabled?: boolean
}>(), {
  disabled: false,
})

const emit = defineEmits<{
  /** Emitted with the flipped state on click. Never fires while disabled. */
  'update:modelValue': [value: boolean]
}>()

/** Classes of the track that do not depend on its state. */
const baseClasses = `group w-9 h-5 rounded-full transition-colors duration-200 flex items-center px-0.5 shrink-0 cursor-pointer focus-visible:ring-offset-1 focus-visible:ring-offset-surface-base disabled:cursor-not-allowed disabled:bg-surface-hover disabled:shadow-[inset_0_0_0_1px_var(--text-muted)] ${focusRingClasses}`

/** Flips the state unless the switch is disabled. */
const flip = (): void => {
  if (props.disabled) {
    return
  }
  emit('update:modelValue', !props.modelValue)
}
</script>

<template>
  <button
    type="button"
    role="switch"
    :aria-checked="modelValue"
    :disabled="disabled"
    :class="[baseClasses, modelValue ? 'not-disabled:gradient-brand' : 'bg-control-track']"
    @click="flip"
  >
    <span
      class="w-4 h-4 rounded-full bg-text-on-primary shadow transition-transform duration-200 group-disabled:bg-text-muted"
      :class="modelValue ? 'translate-x-[14px]' : 'translate-x-0'"
    />
  </button>
</template>
