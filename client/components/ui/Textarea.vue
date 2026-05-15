<template>
  <div class="w-full">
    <!-- Label -->
    <label v-if="label" :for="id" class="block text-sm font-medium text-gray-300 mb-2">
      {{ label }}
      <span v-if="required" class="text-primary-400">*</span>
    </label>

    <!-- Textarea -->
    <textarea
      :id="id"
      :value="modelValue"
      :placeholder="placeholder"
      :required="required"
      :disabled="disabled"
      :rows="rows"
      :maxlength="maxLength"
      :class="textareaClasses"
      @input="handleInput"
      @blur="$emit('blur')"
      @focus="$emit('focus')"
    />

    <!-- Character count and error -->
    <div class="mt-2 flex items-center justify-between">
      <div>
        <p v-if="error" class="text-sm text-red-400">
          {{ error }}
        </p>
        <p v-else-if="hint" class="text-sm text-gray-500">
          {{ hint }}
        </p>
      </div>

      <!-- Character counter -->
      <p v-if="maxLength" class="text-sm text-gray-500">
        {{ characterCount }}/{{ maxLength }}
      </p>
    </div>
  </div>
</template>

<script setup lang="ts">
/**
 * Textarea component with label, error states, and character counter
 */

interface Props {
  /** Unique ID for the textarea */
  id?: string
  /** Label text above textarea */
  label?: string
  /** Current value */
  modelValue?: string
  /** Placeholder text */
  placeholder?: string
  /** Mark as required field */
  required?: boolean
  /** Disable the textarea */
  disabled?: boolean
  /** Error message to display */
  error?: string
  /** Hint text below textarea */
  hint?: string
  /** Number of visible rows */
  rows?: number
  /** Maximum character length */
  maxLength?: number
  /** Allow resize */
  resize?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  rows: 4,
  resize: true
})

const emit = defineEmits<{
  'update:modelValue': [value: string]
  blur: []
  focus: []
}>()

/** Handle input event */
const handleInput = (event: Event) => {
  const target = event.target as HTMLTextAreaElement
  emit('update:modelValue', target.value)
}

/** Current character count */
const characterCount = computed(() => {
  return props.modelValue?.length || 0
})

/** Computed classes for textarea */
const textareaClasses = computed(() => {
  const base = 'block w-full border rounded-lg transition-all duration-200 focus:outline-none focus:ring-2 px-4 py-2.5 text-white placeholder:text-gray-500'

  const resize = props.resize ? 'resize-y' : 'resize-none'

  const state = props.error
    ? 'border-red-500/50 focus:border-red-500 focus:ring-red-500/50 bg-red-500/5'
    : 'border-gray-700 focus:border-primary-500 focus:ring-primary-500/50 bg-white/5 hover:border-gray-600'

  const disabled = props.disabled ? 'bg-gray-800/50 cursor-not-allowed opacity-60' : ''

  return `${base} ${resize} ${state} ${disabled}`
})
</script>
