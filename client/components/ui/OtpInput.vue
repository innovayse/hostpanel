<template>
  <div class="flex flex-col items-center gap-4">
    <label v-if="label" class="text-sm font-medium text-gray-700 dark:text-gray-300">
      {{ label }}
    </label>

    <div class="flex items-center gap-3">
      <input
        v-for="(_, i) in digits"
        :key="i"
        :ref="el => { if (el) inputRefs[i] = el as HTMLInputElement }"
        type="text"
        inputmode="numeric"
        maxlength="1"
        autocomplete="one-time-code"
        :value="digits[i]"
        :disabled="disabled"
        :class="[
          'w-12 h-14 text-center text-xl font-bold rounded-xl border-2 transition-all duration-200',
          'focus:outline-none focus:scale-105',
          'text-gray-900 dark:text-white',
          disabled
            ? 'bg-gray-100 dark:bg-white/5 border-gray-200 dark:border-white/10 cursor-not-allowed opacity-60'
            : digits[i]
              ? 'bg-white dark:bg-white/10 border-primary-500 dark:border-primary-400 shadow-[0_0_0_3px] shadow-primary-500/20'
              : 'bg-white dark:bg-white/5 border-gray-300 dark:border-white/15 hover:border-primary-400 dark:hover:border-primary-500/60 focus:border-primary-500 focus:shadow-[0_0_0_3px] focus:shadow-primary-500/20',
          error ? '!border-red-400 dark:!border-red-500 !shadow-[0_0_0_3px] !shadow-red-500/20' : ''
        ]"
        @keydown="onKeyDown($event, i)"
        @input="onInput($event, i)"
        @paste="onPaste($event)"
        @focus="($event.target as HTMLInputElement).select()"
      />
    </div>

    <p v-if="error" class="text-sm text-red-500 dark:text-red-400">{{ error }}</p>
  </div>
</template>

<script setup lang="ts">
interface Props {
  modelValue: string
  length?: number
  label?: string
  disabled?: boolean
  error?: string
}

const props = withDefaults(defineProps<Props>(), {
  length: 6,
})

const emit = defineEmits<{
  'update:modelValue': [value: string]
  complete: [value: string]
}>()

const inputRefs = ref<HTMLInputElement[]>([])

// Split modelValue into individual digit slots
const digits = computed<string[]>(() => {
  const arr = props.modelValue.split('').slice(0, props.length)
  while (arr.length < props.length) arr.push('')
  return arr
})

function updateValue(newDigits: string[]) {
  const val = newDigits.join('')
  emit('update:modelValue', val)
  if (val.length === props.length && !newDigits.includes('')) {
    emit('complete', val)
  }
}

function focusAt(index: number) {
  nextTick(() => inputRefs.value[index]?.focus())
}

function onInput(event: Event, index: number) {
  const input = event.target as HTMLInputElement
  // Allow only digits
  const char = input.value.replace(/\D/g, '').slice(-1)
  const newDigits = [...digits.value]
  newDigits[index] = char
  updateValue(newDigits)

  if (char && index < props.length - 1) {
    focusAt(index + 1)
  }
}

function onKeyDown(event: KeyboardEvent, index: number) {
  if (event.key === 'Backspace') {
    event.preventDefault()
    const newDigits = [...digits.value]
    if (newDigits[index]) {
      // Clear current cell
      newDigits[index] = ''
      updateValue(newDigits)
    } else if (index > 0) {
      // Move back and clear previous
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

function onPaste(event: ClipboardEvent) {
  event.preventDefault()
  const text = event.clipboardData?.getData('text') ?? ''
  const pasted = text.replace(/\D/g, '').slice(0, props.length)
  if (!pasted) return

  const newDigits = [...digits.value]
  for (let i = 0; i < pasted.length; i++) {
    newDigits[i] = pasted[i]
  }
  updateValue(newDigits)

  // Focus after last pasted digit
  const nextIndex = Math.min(pasted.length, props.length - 1)
  focusAt(nextIndex)
}

/** Expose focus method to parent */
function focus() {
  // Focus first empty cell, or first cell
  const firstEmpty = digits.value.findIndex(d => !d)
  focusAt(firstEmpty === -1 ? 0 : firstEmpty)
}

defineExpose({ focus })
</script>
