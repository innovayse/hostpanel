<script setup lang="ts">
/**
 * Themed number input with increment/decrement buttons.
 *
 * Replaces `<input type="number">`, whose spinner arrows cannot be themed. Emits a clamped number.
 * `data-testid` and classes land on the root element.
 */
import { computed } from 'vue'
import { fieldInvalidClasses } from '@/utils/ui/input'

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
}>(), {
  min: undefined,
  max: undefined,
  step: 1,
  placeholder: '0',
  disabled: false,
  id: undefined,
  invalid: false,
})

const emit = defineEmits<{
  /** Emitted when the value changes. */
  'update:modelValue': [value: number]
}>()

/**
 * The model as a number.
 *
 * `Number()` reproduces the coercion the arithmetic below always performed on the `null` and `''`
 * a caller may hold: both become 0, a numeric string becomes its number, anything else is `NaN`.
 */
const numericValue = computed<number>(() => Number(props.modelValue))

/** Extra classes of a field flagged invalid. */
const invalidClasses = fieldInvalidClasses

/** Whether decrement is allowed. */
const canDecrement = computed<boolean>(() => props.min === undefined || numericValue.value - props.step >= props.min)

/** Whether increment is allowed. */
const canIncrement = computed<boolean>(() => props.max === undefined || numericValue.value + props.step <= props.max)

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
 * Rounds to the step's precision (at least two decimals) to avoid floating point drift.
 *
 * @param v - Value to round.
 * @returns The rounded value.
 */
const round = (v: number): number => {
  const decimals = (props.step.toString().split('.')[1] || '').length
  return parseFloat(v.toFixed(Math.max(decimals, 2)))
}

/**
 * Updates the model from typed input.
 *
 * @param e - The input event.
 */
const onInput = (e: Event): void => {
  const num = parseFloat((e.target as HTMLInputElement).value)
  if (!isNaN(num)) {
    emit('update:modelValue', clamp(num))
  }
}

/**
 * Commits the value on blur: emits it clamped and rewrites the typed text to match.
 *
 * When the model already holds the clamped value nothing re-renders, so a typed out-of-range text
 * (such as a negative number under `min`) would otherwise stay on screen.
 *
 * @param e - The blur event.
 */
const onBlur = (e: Event): void => {
  const clamped = clamp(numericValue.value)
  emit('update:modelValue', clamped)
  ;(e.target as HTMLInputElement).value = String(clamped)
}

/** Decrements the value by one step. */
const decrement = (): void => {
  if (!canDecrement.value || props.disabled) {
    return
  }
  emit('update:modelValue', clamp(round(numericValue.value - props.step)))
}

/** Increments the value by one step. */
const increment = (): void => {
  if (!canIncrement.value || props.disabled) {
    return
  }
  emit('update:modelValue', clamp(round(numericValue.value + props.step)))
}
</script>

<template>
  <div class="relative flex items-stretch" :class="disabled ? 'opacity-50 cursor-not-allowed' : ''">
    <input
      type="text"
      inputmode="decimal"
      :value="modelValue"
      :placeholder="placeholder"
      :disabled="disabled"
      :id="id"
      :aria-invalid="invalid || undefined"
      class="w-full bg-surface-field border border-border rounded-[10px] pl-3 pr-8 py-2 text-[0.82rem] text-text-primary placeholder:text-text-muted outline-none focus:border-primary-500/50 focus:ring-2 focus:ring-primary-500/10 transition-colors disabled:cursor-not-allowed [appearance:textfield]"
      :class="invalid ? invalidClasses : ''"
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
        :disabled="disabled || !canIncrement"
        class="flex-1 flex items-center justify-center bg-surface-elevated text-text-secondary hover:bg-primary-600 hover:text-white transition-colors rounded-tr-[10px] disabled:opacity-30 disabled:cursor-not-allowed"
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
        :disabled="disabled || !canDecrement"
        class="flex-1 flex items-center justify-center bg-surface-elevated text-text-secondary hover:bg-primary-600 hover:text-white transition-colors rounded-br-[10px] disabled:opacity-30 disabled:cursor-not-allowed"
        @mousedown.prevent="decrement"
      >
        <svg class="w-2.5 h-2.5" viewBox="0 0 10 6" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
          <polyline points="1 1 5 5 9 1" />
        </svg>
      </button>
    </div>
  </div>
</template>
