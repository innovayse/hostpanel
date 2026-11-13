<template>
  <div class="flex flex-col items-center gap-4">
    <p v-if="label" class="text-sm font-medium text-gray-700 dark:text-gray-300">
      {{ label }}
    </p>

    <div class="flex items-center gap-3">
      <input
        v-for="(_, i) in digits"
        :key="i"
        :ref="(el) => setInputRef(el, i)"
        type="text"
        inputmode="numeric"
        maxlength="1"
        autocomplete="one-time-code"
        placeholder="·"
        :aria-label="$t('ui.digit', { n: i + 1 })"
        :data-testid="`otp-input-${i}`"
        :value="digits[i]"
        :disabled="disabled"
        :class="cellClasses(i)"
        @keydown="onKeyDown($event, i)"
        @input="onInput($event, i)"
        @paste="onPaste($event)"
        @focus="($event.target as HTMLInputElement).select()"
      >
    </div>

    <p v-if="error" class="text-sm text-red-500 dark:text-red-400">{{ error }}</p>
  </div>
</template>

<script setup lang="ts">
/**
 * OtpInput (`UiOtpInput`): a row of single-digit cells for a one-time code.
 *
 * Accepts digits only, moves focus as the reader types, backspaces across cells and takes a
 * pasted code. Emits `update:modelValue` on every change and `complete` once all cells are
 * filled. `focus()` is exposed so a parent can focus the first empty cell.
 */
import { computed, nextTick, ref } from 'vue'

/** Props of OtpInput. */
interface Props {
  /** The code typed so far (v-model). */
  modelValue: string
  /** Number of cells. */
  length?: number
  /** Caption above the cells. */
  label?: string
  /** Disables every cell. */
  disabled?: boolean
  /** Error line below the cells; also paints the cells red. */
  error?: string
}

/** Props of the component with their defaults applied. */
const props = withDefaults(defineProps<Props>(), {
  length: 6,
  label: undefined,
  disabled: false,
  error: undefined,
})

/** Emits the component raises. */
const emit = defineEmits<{
  /** The code changed. */
  'update:modelValue': [value: string]
  /** Every cell is filled; carries the full code. */
  complete: [value: string]
}>()

/** The cell elements, indexed by position. */
const inputRefs = ref<HTMLInputElement[]>([])

/** The model split into one entry per cell, padded with empty strings. */
const digits = computed<string[]>(() => {
  const cells = props.modelValue.split('').slice(0, props.length)
  while (cells.length < props.length) {
    cells.push('')
  }
  return cells
})

/**
 * Remembers a cell element so focus can be moved to it.
 *
 * @param element The element (or component instance) the ref callback received.
 * @param index Cell position.
 */
const setInputRef = (element: unknown, index: number): void => {
  if (element) {
    inputRefs.value[index] = element as HTMLInputElement
  }
}

/**
 * Class list of one cell.
 *
 * @param index Cell position.
 * @returns The cell's classes for its current state.
 */
const cellClasses = (index: number): string => {
  const base = 'w-12 h-14 text-center text-xl font-bold rounded-xl border-2 transition-all duration-200 focus:outline-none focus-visible:ring-2 focus-visible:ring-offset-2 focus-visible:ring-offset-transparent focus-visible:scale-105 text-gray-900 dark:text-white placeholder:text-gray-400 dark:placeholder:text-gray-500'
  if (props.error) {
    return `${base} bg-white dark:bg-white/5 border-red-400 dark:border-red-500 focus-visible:ring-red-500/50 shadow-[0_0_0_3px] shadow-red-500/20`
  }
  if (props.disabled) {
    return `${base} bg-gray-100 dark:bg-white/5 border-gray-300 dark:border-white/15 cursor-not-allowed opacity-60`
  }
  if (digits.value[index]) {
    return `${base} bg-white dark:bg-white/10 border-primary-500 dark:border-primary-400 focus-visible:ring-primary-500/50 shadow-[0_0_0_3px] shadow-primary-500/20`
  }
  return `${base} bg-white dark:bg-white/5 border-gray-300 dark:border-gray-600 hover:border-primary-400 dark:hover:border-primary-500/60 focus-visible:ring-primary-500/50 focus-visible:border-primary-500 focus-visible:shadow-[0_0_0_3px] focus-visible:shadow-primary-500/20`
}

/**
 * Emits the new code and, when every cell is filled, `complete`.
 *
 * @param newDigits The cells after the change.
 */
const updateValue = (newDigits: string[]): void => {
  const value = newDigits.join('')
  emit('update:modelValue', value)
  if (value.length === props.length && !newDigits.includes('')) {
    emit('complete', value)
  }
}

/**
 * Moves focus to a cell once the DOM has updated.
 *
 * @param index Cell position.
 */
const focusAt = (index: number): void => {
  void nextTick(() => inputRefs.value[index]?.focus())
}

/**
 * Stores the last digit typed into a cell and advances.
 *
 * @param event The native input event.
 * @param index Cell position.
 */
const onInput = (event: Event, index: number): void => {
  const input = event.target as HTMLInputElement
  const char = input.value.replace(/\D/g, '').slice(-1)
  const newDigits = [...digits.value]
  newDigits[index] = char
  updateValue(newDigits)

  if (char && index < props.length - 1) {
    focusAt(index + 1)
  }
}

/**
 * Handles Backspace, Delete and the arrow keys.
 *
 * @param event The native key event.
 * @param index Cell position.
 */
const onKeyDown = (event: KeyboardEvent, index: number): void => {
  if (event.key === 'Backspace') {
    event.preventDefault()
    const newDigits = [...digits.value]
    if (newDigits[index]) {
      newDigits[index] = ''
      updateValue(newDigits)
    } else if (index > 0) {
      newDigits[index - 1] = ''
      updateValue(newDigits)
      focusAt(index - 1)
    }
  } else if (event.key === 'ArrowLeft' && index > 0) {
    event.preventDefault()
    focusAt(index - 1)
  } else if (event.key === 'ArrowRight' && index < props.length - 1) {
    event.preventDefault()
    focusAt(index + 1)
  } else if (event.key === 'Delete') {
    event.preventDefault()
    const newDigits = [...digits.value]
    newDigits[index] = ''
    updateValue(newDigits)
  }
}

/**
 * Fills the cells from a pasted code.
 *
 * @param event The native paste event.
 */
const onPaste = (event: ClipboardEvent): void => {
  event.preventDefault()
  const text = event.clipboardData?.getData('text') ?? ''
  const pasted = text.replace(/\D/g, '').slice(0, props.length)
  if (!pasted) {
    return
  }

  const newDigits = [...digits.value]
  for (let i = 0; i < pasted.length; i++) {
    newDigits[i] = pasted[i] ?? ''
  }
  updateValue(newDigits)

  focusAt(Math.min(pasted.length, props.length - 1))
}

/** Focuses the first empty cell, or the first cell when all are filled. */
const focus = (): void => {
  const firstEmpty = digits.value.findIndex((digit) => !digit)
  focusAt(firstEmpty === -1 ? 0 : firstEmpty)
}

defineExpose({ focus })
</script>
