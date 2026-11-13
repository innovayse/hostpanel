<script setup lang="ts">
/**
 * Themed number input with increment/decrement buttons.
 *
 * Replaces `<input type="number">`, whose spinner arrows cannot be themed. Emits a clamped number,
 * or `null` when a `nullable` field is cleared.
 * `data-testid` and classes land on the root element.
 */
import { computed } from 'vue'
import { fieldBaseClasses, fieldInvalidClasses } from '@/utils/ui/input'
import { fieldSizeClasses } from '@/utils/ui/size'
import { StepDirection, stepValue } from '@/utils/ui/stepper'

/** Props for UiNumberInput. */
const props = withDefaults(defineProps<{
  /**
   * Current value.
   *
   * `null` and `''` both mean "no value yet" and are what the callers actually hold:
   * `ServerFormModal`'s optional `maxAccounts` is `number | null`, and `DnsRecordsTable` seeds its
   * priority fields with `''`. The arithmetic coerces both to 0; narrowing this to `number` would
   * change how those screens represent "unset".
   */
  modelValue: number | string | null
  /** Minimum allowed value. */
  min?: number
  /** Maximum allowed value. */
  max?: number
  /** Step amount for increment/decrement buttons. */
  step?: number
  /** Hint shown while the field is empty. */
  placeholder?: string
  /** Disables the input. */
  disabled?: boolean
  /** Id of the native input, so a caption can point at it (`UiField` hands one out). */
  id?: string
  /** Marks the value as refused by the server (red border, `aria-invalid`). */
  invalid?: boolean
  /** Id of the element that describes the value (a hint or the error line, from `UiField`). */
  describedby?: string
  /**
   * Lets the admin clear the field: when the typed text becomes empty the model is set to `null`.
   * Off by default, so a required field keeps its last number while the text is being retyped.
   */
  nullable?: boolean
}>(), {
  min: undefined,
  max: undefined,
  step: 1,
  placeholder: '0',
  disabled: false,
  id: undefined,
  invalid: false,
  describedby: undefined,
  nullable: false,
})

const emit = defineEmits<{
  /** Emitted when the value changes; `null` only from a `nullable` field whose text was cleared. */
  'update:modelValue': [value: number | null]
}>()

/**
 * The model as a number.
 *
 * `Number()` reproduces the coercion the arithmetic below always performed on the `null` and `''`
 * a caller may hold: both become 0, a numeric string becomes its number, anything else is `NaN`.
 */
const numericValue = computed<number>(() => Number(props.modelValue))

/** Classes of the typed text: the kit's field look, room on the right for the spinner column. */
const inputClasses = computed<string[]>(() => [
  'w-full pr-8 [appearance:textfield]',
  fieldBaseClasses,
  fieldSizeClasses.md,
  props.invalid ? fieldInvalidClasses : '',
])

/** The value one step down, or `null` at the lower edge. */
const decremented = computed<number | null>(() => stepValue(numericValue.value, props.step, StepDirection.Down, props.min, props.max))

/** The value one step up, or `null` at the upper edge. */
const incremented = computed<number | null>(() => stepValue(numericValue.value, props.step, StepDirection.Up, props.min, props.max))

/**
 * Clamps a value to the min/max range.
 *
 * @param v - Value to clamp.
 * @returns The clamped value.
 */
const clamp = (v: number): number => {
  if (props.min !== undefined && v < props.min) {
    return props.min
  }
  if (props.max !== undefined && v > props.max) {
    return props.max
  }
  return v
}

/**
 * Updates the model from typed input.
 *
 * @param e - The input event.
 */
const onInput = (e: Event): void => {
  const text = (e.target as HTMLInputElement).value
  if (props.nullable && text.trim() === '') {
    emit('update:modelValue', null)
    return
  }
  const num = parseFloat(text)
  if (!isNaN(num)) {
    emit('update:modelValue', clamp(num))
  }
}

/**
 * Commits the value on blur: emits it clamped and rewrites the typed text to match.
 *
 * When the model already holds the clamped value nothing re-renders, so a typed out-of-range text
 * (such as a negative number under `min`) would otherwise stay on screen. An empty text commits
 * nothing: the field stays empty (or shows the value the model still holds).
 *
 * @param e - The blur event.
 */
const onBlur = (e: Event): void => {
  const input = e.target as HTMLInputElement
  if (input.value.trim() === '') {
    // An empty field stays empty: committing 0 (or `min`) would turn "left blank" into a real value.
    const held = props.modelValue
    input.value = held === null || held === '' ? '' : String(held)
    return
  }
  const clamped = clamp(numericValue.value)
  emit('update:modelValue', clamped)
  ;(e.target as HTMLInputElement).value = String(clamped)
}

/** Decrements the value by one step. */
const decrement = (): void => {
  if (decremented.value === null || props.disabled) {
    return
  }
  emit('update:modelValue', decremented.value)
}

/** Increments the value by one step. */
const increment = (): void => {
  if (incremented.value === null || props.disabled) {
    return
  }
  emit('update:modelValue', incremented.value)
}
</script>

<template>
  <div class="relative flex items-stretch">
    <input
      type="text"
      inputmode="decimal"
      :value="modelValue"
      :placeholder="placeholder"
      :disabled="disabled"
      :id="id"
      :aria-invalid="invalid || undefined"
      :aria-describedby="describedby"
      :class="inputClasses"
      @input="onInput"
      @blur="onBlur"
    >
    <!-- Spinner buttons -->
    <div class="absolute right-0 top-0 bottom-0 flex flex-col w-6 border-l border-border">
      <button
        type="button"
        tabindex="-1"
        aria-hidden="true"
        data-testid="number-increment"
        :disabled="disabled || incremented === null"
        class="flex-1 flex items-center justify-center bg-surface-elevated text-text-secondary not-disabled:hover:bg-primary-400 light:not-disabled:hover:bg-primary-600 not-disabled:hover:text-surface-base transition-colors rounded-tr-control disabled:text-text-muted disabled:cursor-not-allowed"
        @mousedown.prevent="increment"
      >
        <svg class="w-2.5 h-2.5" viewBox="0 0 10 6" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
          <polyline points="1 5 5 1 9 5" />
        </svg>
      </button>
      <span class="h-px bg-border" />
      <button
        type="button"
        tabindex="-1"
        aria-hidden="true"
        data-testid="number-decrement"
        :disabled="disabled || decremented === null"
        class="flex-1 flex items-center justify-center bg-surface-elevated text-text-secondary not-disabled:hover:bg-primary-400 light:not-disabled:hover:bg-primary-600 not-disabled:hover:text-surface-base transition-colors rounded-br-control disabled:text-text-muted disabled:cursor-not-allowed"
        @mousedown.prevent="decrement"
      >
        <svg class="w-2.5 h-2.5" viewBox="0 0 10 6" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
          <polyline points="1 1 5 5 9 1" />
        </svg>
      </button>
    </div>
  </div>
</template>
