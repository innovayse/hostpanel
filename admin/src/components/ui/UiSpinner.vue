<script setup lang="ts">
import { computed, ref } from 'vue'

const props = defineProps<{
  modelValue: number
  min?: number
  max?: number
  step?: number
  placeholder?: string
}>()

const emit = defineEmits<{
  'update:modelValue': [value: number]
}>()

let holdInterval: ReturnType<typeof setInterval> | null = null

const displayValue = computed(() => {
  if (props.modelValue === 0 || props.modelValue === undefined) return ''
  return props.modelValue.toFixed(2)
})

function increment() {
  const step = props.step ?? 1
  const newValue = (props.modelValue ?? 0) + step
  if (props.max === undefined || newValue <= props.max) {
    emit('update:modelValue', newValue)
  }
}

function decrement() {
  const step = props.step ?? 1
  const newValue = (props.modelValue ?? 0) - step
  if (props.min === undefined || newValue >= props.min) {
    emit('update:modelValue', newValue)
  }
}

function startIncrement() {
  increment()
  holdInterval = setInterval(() => {
    increment()
  }, 100)
}

function startDecrement() {
  decrement()
  holdInterval = setInterval(() => {
    decrement()
  }, 100)
}

function stopHold() {
  if (holdInterval) {
    clearInterval(holdInterval)
    holdInterval = null
  }
}

function handleInput(event: Event) {
  const value = (event.target as HTMLInputElement).value
  const num = value === '' ? 0 : parseFloat(value)
  if (!isNaN(num)) {
    emit('update:modelValue', num)
  }
}

function handleBlur(event: Event) {
  const value = (event.target as HTMLInputElement).value
  if (value === '' || value === '.') {
    emit('update:modelValue', 0)
  }
}
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
      @input="handleInput"
      @blur="handleBlur"
      class="w-full px-3 py-2 text-[0.82rem] text-text-primary bg-white/[0.05] border border-border rounded-[9px] placeholder:text-text-muted focus:outline-none focus:border-primary-500/40 transition-colors pr-[68px] [&::-webkit-outer-spin-button]:appearance-none [&::-webkit-inner-spin-button]:appearance-none [&]:[-moz-appearance:textfield]"
    />
    <div class="absolute right-0 top-0 bottom-0 flex flex-col items-center justify-center gap-0.5 pr-1">
      <button
        type="button"
        @mousedown="startIncrement"
        @mouseup="stopHold"
        @mouseleave="stopHold"
        class="flex items-center justify-center w-8 h-4 text-text-muted hover:text-text-primary hover:bg-white/[0.05] transition-colors rounded"
        :disabled="max !== undefined && modelValue >= max"
      >
        <svg class="w-3 h-3" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3">
          <polyline points="6 15 12 9 18 15" />
        </svg>
      </button>
      <button
        type="button"
        @mousedown="startDecrement"
        @mouseup="stopHold"
        @mouseleave="stopHold"
        class="flex items-center justify-center w-8 h-4 text-text-muted hover:text-text-primary hover:bg-white/[0.05] transition-colors rounded"
        :disabled="min !== undefined && modelValue <= min"
      >
        <svg class="w-3 h-3" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3">
          <polyline points="18 9 12 15 6 9" />
        </svg>
      </button>
    </div>
  </div>
</template>
