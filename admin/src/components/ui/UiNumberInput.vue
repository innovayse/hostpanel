<script setup lang="ts">
/**
 * Custom dark-themed number input with styled increment/decrement buttons.
 * Replaces native <input type="number"> whose spinner arrows cannot be themed.
 * Matches the UiSelect / UiDatePicker visual language.
 */
import { computed } from 'vue'

const props = withDefaults(defineProps<{
  /**
   * Current value.
   *
   * `null` and `''` both mean "no value yet" and are what the callers actually hold —
   * `ServerFormModal`'s optional `maxAccounts` is `number | null`, and `DnsRecordsTable`
   * seeds its priority fields with `''`. The component's arithmetic coerces both to 0, which
   * is the behaviour those screens have always had; narrowing this to `number` would mean
   * changing how they represent "unset", which is a separate change.
   */
  modelValue: number | string | null
  /** Minimum allowed value. */
  min?: number
  /** Maximum allowed value. */
  max?: number
  /** Step amount for increment/decrement buttons. */
  step?: number
  /** Placeholder text. */
  placeholder?: string
  /** Disables the input. */
  disabled?: boolean
}>(), {
  min: undefined,
  max: undefined,
  step: 1,
  placeholder: '0',
  disabled: false,
})

const emit = defineEmits<{
  /** Emitted when the value changes. */
  'update:modelValue': [value: number]
}>()

/**
 * {@link modelValue} as a number.
 *
 * `Number()` reproduces exactly the coercion the arithmetic below already performed on the
 * `null` and `''` a caller may hold: both become 0, a numeric string becomes its number, and
 * anything else becomes `NaN` — the same results as `props.modelValue - props.step` gave.
 */
const numericValue = computed(() => Number(props.modelValue))

/** Whether decrement is allowed. */
const canDecrement = computed(() => props.min === undefined || numericValue.value - props.step >= props.min)

/** Whether increment is allowed. */
const canIncrement = computed(() => props.max === undefined || numericValue.value + props.step <= props.max)

/**
 * Updates the value from direct input.
 *
 * @param e - The input event.
 */
function onInput(e: Event): void {
  const raw = (e.target as HTMLInputElement).value
  const num = parseFloat(raw)
  if (!isNaN(num)) {
    emit('update:modelValue', clamp(num))
  }
}

/** Decrements the value by step. */
function decrement(): void {
  if (!canDecrement.value || props.disabled) return
  emit('update:modelValue', clamp(round(numericValue.value - props.step)))
}

/** Increments the value by step. */
function increment(): void {
  if (!canIncrement.value || props.disabled) return
  emit('update:modelValue', clamp(round(numericValue.value + props.step)))
}

/**
 * Clamps a value to the min/max range.
 *
 * @param v - Value to clamp.
 * @returns Clamped value.
 */
function clamp(v: number): number {
  if (props.min !== undefined && v < props.min) return props.min
  if (props.max !== undefined && v > props.max) return props.max
  return v
}

/**
 * Rounds to avoid floating point drift.
 *
 * @param v - Value to round.
 * @returns Rounded value.
 */
function round(v: number): number {
  const decimals = (props.step.toString().split('.')[1] || '').length
  return parseFloat(v.toFixed(Math.max(decimals, 2)))
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
      class="w-full bg-white/[0.04] border border-border rounded-[10px] pl-3 pr-8 py-2 text-[0.82rem] text-text-primary focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors [appearance:textfield]"
      @input="onInput"
      @blur="emit('update:modelValue', clamp(numericValue))"
    />
    <!-- Spinner buttons -->
    <div class="absolute right-0 top-0 bottom-0 flex flex-col w-6 border-l border-border">
      <button
        type="button"
        tabindex="-1"
        :disabled="disabled || !canIncrement"
        class="flex-1 flex items-center justify-center bg-surface-elevated text-text-muted hover:bg-primary-500 hover:text-white transition-colors rounded-tr-[10px] disabled:opacity-30 disabled:cursor-not-allowed"
        @mousedown.prevent="increment"
      >
        <svg class="w-2.5 h-2.5" viewBox="0 0 10 6" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
          <polyline points="1 5 5 1 9 5" />
        </svg>
      </button>
      <span class="h-px bg-border" />
      <button
        type="button"
        tabindex="-1"
        :disabled="disabled || !canDecrement"
        class="flex-1 flex items-center justify-center bg-surface-elevated text-text-muted hover:bg-primary-500 hover:text-white transition-colors rounded-br-[10px] disabled:opacity-30 disabled:cursor-not-allowed"
        @mousedown.prevent="decrement"
      >
        <svg class="w-2.5 h-2.5" viewBox="0 0 10 6" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
          <polyline points="1 1 5 5 9 1" />
        </svg>
      </button>
    </div>
  </div>
</template>
