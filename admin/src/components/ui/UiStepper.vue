<script setup lang="ts">
/**
 * Money-style number field with press-and-hold stepper buttons.
 *
 * Shows the value with two decimals (empty while 0) and keeps stepping at 100 ms intervals while
 * a stepper is held. The hold timer is cleared on release, on leave and on unmount.
 */
import { computed, onUnmounted } from 'vue'
import { fieldBaseClasses, fieldWidthClasses } from '@/utils/ui/input'
import { fieldSizeClasses } from '@/utils/ui/size'
import { StepDirection, stepValue } from '@/utils/ui/stepper'

/** Props for UiStepper. */
const props = defineProps<{
  /** Current value. */
  modelValue: number
  /** Minimum allowed value (the field itself defaults to 0). */
  min?: number
  /** Maximum allowed value. */
  max?: number
  /** Amount added or removed per step. */
  step?: number
  /** Hint shown while the field is empty. */
  placeholder?: string
}>()

const emit = defineEmits<{
  /** Emitted with the new value. */
  'update:modelValue': [value: number]
}>()

/** Delay between repeated steps while a stepper is held, in milliseconds. */
const HOLD_REPEAT_MS = 100

/** Field classes: the kit's themed field box, with room on the right for the steppers and the native ones hidden. */
const inputClasses = [
  fieldWidthClasses,
  fieldBaseClasses,
  fieldSizeClasses.md,
  'pr-[68px] [&::-webkit-outer-spin-button]:appearance-none [&::-webkit-inner-spin-button]:appearance-none [&]:[-moz-appearance:textfield]',
]

/** Interval id while a stepper is held. */
let holdInterval: ReturnType<typeof setInterval> | null = null

/** Value as shown in the field: empty for 0, otherwise two decimals. */
const displayValue = computed<string>(() => {
  if (props.modelValue === 0 || props.modelValue === undefined) {
    return ''
  }
  return props.modelValue.toFixed(2)
})

/**
 * Moves the value one step, unless that leaves the range.
 *
 * @param direction - Whether to raise or lower the value.
 * @returns The stepped value, or `null` at the range edge.
 */
const stepped = (direction: StepDirection): number | null =>
  stepValue(props.modelValue ?? 0, props.step ?? 1, direction, props.min, props.max)

/** Raises the value by one step, unless that passes `max`. */
const increment = (): void => {
  const next = stepped(StepDirection.Up)
  if (next !== null) {
    emit('update:modelValue', next)
  }
}

/** Lowers the value by one step, unless that passes `min`. */
const decrement = (): void => {
  const next = stepped(StepDirection.Down)
  if (next !== null) {
    emit('update:modelValue', next)
  }
}

/** Stops the repeating step. */
const stopHold = (): void => {
  if (holdInterval) {
    clearInterval(holdInterval)
    holdInterval = null
  }
}

/** Steps up once, then keeps stepping while held. */
const startIncrement = (): void => {
  stopHold()
  increment()
  holdInterval = setInterval(increment, HOLD_REPEAT_MS)
}

/** Steps down once, then keeps stepping while held. */
const startDecrement = (): void => {
  stopHold()
  decrement()
  holdInterval = setInterval(decrement, HOLD_REPEAT_MS)
}

/**
 * Parses typed text into the model (empty becomes 0, unparsable text is ignored).
 *
 * @param event - The input event.
 */
const handleInput = (event: Event): void => {
  const value = (event.target as HTMLInputElement).value
  const num = value === '' ? 0 : parseFloat(value)
  if (!isNaN(num)) {
    emit('update:modelValue', num)
  }
}

/**
 * Normalises an empty or lone-dot field to 0 when it loses focus.
 *
 * @param event - The blur event.
 */
const handleBlur = (event: Event): void => {
  const value = (event.target as HTMLInputElement).value
  if (value === '' || value === '.') {
    emit('update:modelValue', 0)
  }
}

onUnmounted(stopHold)
</script>

<template>
  <div class="relative flex items-center">
    <input
      :value="displayValue"
      type="number"
      :step="step ?? 0.01"
      :min="min ?? 0"
      :max="max"
      :placeholder="placeholder ?? '0.00'"
      :class="inputClasses"
      @input="handleInput"
      @blur="handleBlur"
    >
    <div class="absolute right-0 top-0 bottom-0 flex flex-col items-center justify-center gap-0.5 pr-1">
      <button
        type="button"
        aria-hidden="true"
        tabindex="-1"
        data-testid="stepper-increment"
        class="flex items-center justify-center w-8 h-4 text-text-secondary hover:text-text-primary hover:bg-surface-hover transition-colors rounded cursor-pointer disabled:opacity-40 disabled:cursor-not-allowed disabled:hover:bg-transparent"
        :disabled="stepped(StepDirection.Up) === null"
        @mousedown="startIncrement"
        @mouseup="stopHold"
        @mouseleave="stopHold"
      >
        <svg class="w-3 h-3" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" aria-hidden="true">
          <polyline points="6 15 12 9 18 15" />
        </svg>
      </button>
      <button
        type="button"
        aria-hidden="true"
        tabindex="-1"
        data-testid="stepper-decrement"
        class="flex items-center justify-center w-8 h-4 text-text-secondary hover:text-text-primary hover:bg-surface-hover transition-colors rounded cursor-pointer disabled:opacity-40 disabled:cursor-not-allowed disabled:hover:bg-transparent"
        :disabled="stepped(StepDirection.Down) === null"
        @mousedown="startDecrement"
        @mouseup="stopHold"
        @mouseleave="stopHold"
      >
        <svg class="w-3 h-3" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" aria-hidden="true">
          <polyline points="18 9 12 15 6 9" />
        </svg>
      </button>
    </div>
  </div>
</template>
