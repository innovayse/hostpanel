<script setup lang="ts">
/**
 * Radio button bound with `v-model`.
 *
 * Renders the native `<input type="radio">` so grouping by `name` and arrow-key navigation come
 * from the browser. It is drawn like `UiCheckbox`: a field-surface disc that fills with the brand
 * gradient and shows a white dot when selected. Emits the value it stands for when the user selects it.
 */

import { focusRingClasses } from '@/utils/ui/input'

/** Props for UiRadio. */
const props = defineProps<{
  /** Currently selected value of the group (two-way bound with `v-model`). */
  modelValue?: string | number | boolean
  /** Value this radio stands for. */
  value: string | number | boolean
  /** Blocks interaction and dims the radio. */
  disabled?: boolean
}>()

const emit = defineEmits<{
  /** Emitted with this radio's value when the user selects it. */
  'update:modelValue': [value: string | number | boolean]
}>()

/** Classes of the radio: zero-specificity size (a screen's own wins), the field disc, the gradient fill with a dot when selected, the focus ring, a muted disabled state. */
const classes = `[:where(&)]:w-4 [:where(&)]:h-4 shrink-0 appearance-none cursor-pointer rounded-full border border-border-control bg-surface-field not-disabled:hover:border-text-secondary radio-dot disabled:cursor-not-allowed disabled:bg-surface-hover ${focusRingClasses}`

/** Reports this radio's value as the group's new selection. */
const onChange = (): void => {
  emit('update:modelValue', props.value)
}
</script>

<template>
  <input
    type="radio"
    :checked="modelValue === value"
    :disabled="disabled"
    :class="classes"
    @change="onChange"
  >
</template>
