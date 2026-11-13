<script setup lang="ts">
/**
 * Date and time picker for an optional moment, with the popup following the panel's light/dark scheme.
 *
 * Wraps the date-picker library so a screen does not configure it (or restyle its input) itself.
 * The model is a `Date` or `null`; the clear button sets `null`. `minDate` stops the pick earlier
 * than another field, e.g. an end moment before its start. The library's colours, radius and font come
 * from the panel tokens through its CSS variables (`dateLibraryThemeClasses` on a wrapper), so the popup
 * is opaque and themed in both schemes.
 */
import { VueDatePicker } from '@vuepic/vue-datepicker'
import { useThemeStore } from '@/stores/theme'
import { dateLibraryThemeClasses } from '@/utils/ui/datePicker'
import '@vuepic/vue-datepicker/dist/main.css'

/** Theme store; the library has its own dark/light switch, so it is fed the panel's scheme. */
const theme = useThemeStore()

/** Props for UiDateTimePicker. */
withDefaults(defineProps<{
  /** Chosen moment (two-way bound with `v-model`); `null` for none. */
  modelValue: Date | null
  /** Hint shown while nothing is chosen (already translated). */
  placeholder?: string
  /** Earliest moment that can be chosen. */
  minDate?: Date
}>(), {
  placeholder: undefined,
  minDate: undefined,
})

const emit = defineEmits<{
  /** Emitted with the new moment, or `null` when it was cleared. */
  'update:modelValue': [value: Date | null]
}>()

/** Classes for the library's input, so it matches the kit's fields (the library's own CSS is overridden). */
const INPUT_CLASSES = 'bg-surface-field! rounded-control! h-10.5! px-3.5! text-sm! placeholder:text-text-muted! focus-visible:ring-2! focus-visible:ring-text-accent/40!'

/**
 * Re-emits the library's value as a `Date` or `null` (its model type is wider than ours).
 *
 * @param value - The value the library reported.
 */
const onUpdate = (value: unknown): void => {
  emit('update:modelValue', value instanceof Date ? value : null)
}
</script>

<template>
  <div :class="dateLibraryThemeClasses">
    <VueDatePicker
      :model-value="modelValue"
      :enable-time-picker="true"
      :clearable="true"
      :dark="!theme.isLight"
      :auto-apply="true"
      :min-date="minDate"
      :placeholder="placeholder"
      :input-class-name="INPUT_CLASSES"
      @update:model-value="onUpdate"
    />
  </div>
</template>
