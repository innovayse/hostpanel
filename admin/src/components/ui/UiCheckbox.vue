<script setup lang="ts">
/**
 * Themed checkbox.
 *
 * Replaces the native `<input type="checkbox">`, whose appearance cannot be themed. Unchecked it is
 * a field-coloured box with a border; checked it fills with the primary gradient and shows a tick. A
 * disabled box turns to a flat muted one (the tick muted too) with a not-allowed cursor.
 * `indeterminate` shows a dash and reports `aria-checked="mixed"` (a select-all box over a partial selection).
 * It is a `role="checkbox"` button, so it stays keyboard- and screen-reader-operable. Give it an
 * `aria-label` (or wrap it in a `UiLabel`) so it has a name.
 */

import { computed } from 'vue'
import { focusRingClasses } from '@/utils/ui/input'

/** Props for UiCheckbox. */
const props = withDefaults(defineProps<{
  /** Checked state (two-way bound with `v-model`). */
  modelValue: boolean
  /** Blocks interaction and dims the box. */
  disabled?: boolean
  /** Shows a partly checked state (a dash); a click still reports the flipped `modelValue`. */
  indeterminate?: boolean
}>(), {
  disabled: false,
  indeterminate: false,
})

const emit = defineEmits<{
  /** Emitted with the flipped state when the user toggles the box. */
  'update:modelValue': [value: boolean]
  /** Emitted after `update:modelValue` with the click that caused it (so a caller can read `shiftKey`). */
  change: [event: MouseEvent]
}>()

/** True when the box shows a mark: a tick when checked, a dash when indeterminate. */
const marked = computed<boolean>(() => props.modelValue || props.indeterminate)

/** Classes of the box that do not depend on its state. */
const baseClasses = `group w-4 h-4 rounded-[4px] border flex items-center justify-center shrink-0 transition-colors cursor-pointer disabled:cursor-not-allowed disabled:bg-surface-hover disabled:border-border-strong ${focusRingClasses}`

/**
 * Flips the state unless the checkbox is disabled.
 *
 * @param event - The click that toggled the box.
 */
const toggle = (event: MouseEvent): void => {
  if (props.disabled) {
    return
  }
  emit('update:modelValue', !props.modelValue)
  emit('change', event)
}
</script>

<template>
  <button
    type="button"
    role="checkbox"
    :aria-checked="indeterminate ? 'mixed' : modelValue"
    :disabled="disabled"
    :class="[baseClasses, marked
      ? 'not-disabled:gradient-brand border-transparent'
      : 'bg-surface-field border-border-control not-disabled:hover:border-text-secondary']"
    @click="toggle"
  >
    <svg
      v-if="indeterminate"
      class="w-2.5 h-2.5 text-text-on-primary group-disabled:text-text-muted"
      viewBox="0 0 12 10"
      fill="none"
      stroke="currentColor"
      stroke-width="2"
      stroke-linecap="round"
      aria-hidden="true"
    >
      <line x1="2" y1="5" x2="10" y2="5" />
    </svg>
    <svg
      v-else-if="modelValue"
      class="w-2.5 h-2.5 text-text-on-primary group-disabled:text-text-muted"
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
