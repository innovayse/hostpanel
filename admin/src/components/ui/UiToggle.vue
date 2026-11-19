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

/**
 * Classes of the track that do not depend on its state. Its width, its inset and its height come from the switch tokens
 * (`--spacing-toggle-*` in `assets/main.css`): the height is the knob plus an inset above and below.
 */
const baseClasses = `group w-toggle-track h-[calc(var(--spacing-toggle-knob)+2*var(--spacing-toggle-inset))] rounded-full transition-colors duration-200 flex items-center px-toggle-inset shrink-0 cursor-pointer focus-visible:ring-offset-1 focus-visible:ring-offset-surface-base disabled:cursor-not-allowed disabled:bg-surface-hover disabled:shadow-[inset_0_0_0_1px_var(--text-muted)] ${focusRingClasses}`

/** Classes of the knob: its own size, its colour and the movement between the two states. */
const knobClasses = 'w-toggle-knob h-toggle-knob rounded-full bg-text-on-primary shadow transition-transform duration-200 group-disabled:bg-text-muted'

/**
 * Travel of the knob when the switch is on: what the track leaves once the knob and an inset on each side are taken out
 * of it. That keeps the knob centred in both states.
 */
const knobOnClasses = 'translate-x-[calc(var(--spacing-toggle-track)-var(--spacing-toggle-knob)-2*var(--spacing-toggle-inset))]'

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
    <span :class="[knobClasses, modelValue ? knobOnClasses : 'translate-x-0']" />
  </button>
</template>
