<script setup lang="ts">
/**
 * Themed number input with increment/decrement buttons.
 *
 * Replaces `<input type="number">`, whose spinner arrows cannot be themed. Emits a clamped number,
 * or `null` when a `nullable` field is cleared. A `suffix` (a unit such as "min") is drawn inside the field, left of the
 * spinner buttons, so the number is read with its unit.
 * The field never narrows below the room its value needs next to the spinner column (and the suffix), so a number in a
 * table cell stays readable whatever width the column is given. `data-testid` and classes land on the root element.
 * The spinner column sits inside the input's own border and radius, so the field is one control with one focus ring,
 * in both themes. A field that steps by fractions (a price, 0.01) shows that many decimals once it is left ("23.70").
 */
import { computed, ref } from 'vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import { Icon } from '@/lib/icons'
import { fixedAmount } from '@/lib/money'
import { UiSize } from '@/utils/ui/size'
import { fieldBaseClasses, fieldInvalidClasses } from '@/utils/ui/input'
import { fieldSizeClasses } from '@/utils/ui/size'
import { StepDirection, stepDecimals, stepValue } from '@/utils/ui/stepper'

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
  /** Unit written inside the field after the number (already translated); none when omitted. */
  suffix?: string
  /** Disables the input. */
  disabled?: boolean
  /** Id of the native input, so a caption can point at it (`UiField` hands one out). */
  id?: string
  /** Marks the value as refused by the server (red border, `aria-invalid`). */
  invalid?: boolean
  /** Marks a value as needed: the native `required` and `aria-required` of the input. */
  required?: boolean
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
  suffix: undefined,
  disabled: false,
  id: undefined,
  invalid: false,
  required: false,
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

/** Whether the typed text has the caret: the text is shown as typed meanwhile, and in its fixed decimals once the field is left. */
const focused = ref(false)

/** How many decimals the field shows: those of its step, so a field stepping by 0.01 reads "23.70", and a whole-number one is left as it is. */
const decimals = computed<number>(() => stepDecimals(props.step))

/**
 * Writes a value in the field's decimals through the shared money formatting.
 *
 * @param value - The value to write.
 * @returns The text, or the value as held when it is empty or not a number or the field has no decimals.
 */
const formatValue = (value: number | string | null): string => {
  const number = Number(value)
  if (decimals.value === 0 || value === null || value === '' || Number.isNaN(number)) {
    return value === null ? '' : String(value)
  }
  return fixedAmount(number, decimals.value)
}

/** The text of the field: as typed while it has the caret, in fixed decimals otherwise. */
const shownText = computed<string>(() => (focused.value ? String(props.modelValue ?? '') : formatValue(props.modelValue)))

/** Classes of the field's box: the room for about eight characters of the value plus the spinner column, more when a suffix shares the line. */
const rootClasses = computed<string>(() => (props.suffix === undefined ? 'min-w-28' : 'min-w-40'))

/** Classes of the typed text: the kit's field look, room on the right for the spinner column. */
const inputClasses = computed<string[]>(() => [
  props.suffix === undefined ? 'w-full pr-8 [appearance:textfield]' : 'w-full pr-20 [appearance:textfield]',
  fieldBaseClasses,
  fieldSizeClasses.md,
  props.invalid ? fieldInvalidClasses : '',
])

/** Classes of the spinner column: laid over the input's right edge inside its 1px border, rounded to the inside of the control's radius, a hairline of the control border on its left. */
const stepperClasses = 'absolute right-px top-px bottom-px flex w-6 flex-col overflow-hidden rounded-r-[calc(var(--radius-control)-1px)] border-l border-border-control'

/** Classes of one spinner button: no fill of its own (the field's shows through), the accent fill on hover. */
const stepButtonClasses = 'flex flex-1 items-center justify-center bg-transparent text-text-secondary transition-colors not-disabled:hover:bg-primary-400 light:not-disabled:hover:bg-primary-600 not-disabled:hover:text-surface-base disabled:cursor-not-allowed disabled:text-text-muted'

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
 * Commits the value on blur: emits it clamped and rewrites the typed text to match (in the field's decimals).
 *
 * When the model already holds the clamped value nothing re-renders, so a typed out-of-range text
 * (such as a negative number under `min`) would otherwise stay on screen. An empty text commits
 * nothing: the field stays empty (or shows the value the model still holds).
 *
 * @param e - The blur event.
 */
const onBlur = (e: Event): void => {
  focused.value = false
  const input = e.target as HTMLInputElement
  if (input.value.trim() === '') {
    // An empty field stays empty: committing 0 (or `min`) would turn "left blank" into a real value.
    input.value = formatValue(props.modelValue)
    return
  }
  const clamped = clamp(numericValue.value)
  emit('update:modelValue', clamped)
  input.value = formatValue(clamped)
}

/** Shows the text as typed while the caret is in the field. */
const onFocus = (): void => {
  focused.value = true
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
  <div class="relative flex items-stretch" :class="rootClasses">
    <input
      type="text"
      inputmode="decimal"
      :value="shownText"
      :placeholder="placeholder"
      :disabled="disabled"
      :id="id"
      :required="required"
      :aria-required="required || undefined"
      :aria-invalid="invalid || undefined"
      :aria-describedby="describedby"
      :class="inputClasses"
      @input="onInput"
      @focus="onFocus"
      @blur="onBlur"
    >
    <span
      v-if="suffix !== undefined"
      class="pointer-events-none absolute right-8 top-1/2 -translate-y-1/2 text-sm text-text-muted"
      data-testid="number-suffix"
    >{{ suffix }}</span>
    <!-- Spinner buttons: inside the input's border (1px inset), divided from the text by the control border token -->
    <div :class="stepperClasses">
      <button
        type="button"
        tabindex="-1"
        aria-hidden="true"
        data-testid="number-increment"
        :disabled="disabled || incremented === null"
        :class="stepButtonClasses"
        @mousedown.prevent="increment"
      >
        <UiIcon :markup="Icon.ChevronUp" :size="UiSize.Xs" />
      </button>
      <span class="h-px bg-border-control" />
      <button
        type="button"
        tabindex="-1"
        aria-hidden="true"
        data-testid="number-decrement"
        :disabled="disabled || decremented === null"
        :class="stepButtonClasses"
        @mousedown.prevent="decrement"
      >
        <UiIcon :markup="Icon.ChevronDown" :size="UiSize.Xs" />
      </button>
    </div>
  </div>
</template>
