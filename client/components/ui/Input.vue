<template>
  <div class="w-full">
    <label v-if="label" :for="id" class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
      {{ label }}
      <span v-if="required" class="text-primary-500">*</span>
    </label>

    <div class="relative">
      <!-- Prefix slot (e.g. search icon) -->
      <div v-if="$slots.prefix" class="absolute left-3 top-1/2 -translate-y-1/2 pointer-events-none">
        <slot name="prefix" />
      </div>

      <Icon
        v-else-if="icon"
        :name="icon"
        class="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400"
      />

      <input
        :id="id"
        :type="resolvedType"
        :value="modelValue"
        :placeholder="placeholder"
        :required="required"
        :disabled="disabled"
        :autocomplete="autocomplete"
        :class="inputClasses"
        @input="$emit('update:modelValue', ($event.target as HTMLInputElement).value)"
        @blur="$emit('blur')"
        @focus="$emit('focus')"
      >

      <button
        v-if="type === 'password'"
        type="button"
        tabindex="-1"
        class="absolute right-3 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600 dark:hover:text-gray-300 transition-colors"
        @click="showPassword = !showPassword"
      >
        <Eye v-if="!showPassword" :size="18" :stroke-width="2" />
        <EyeOff v-else :size="18" :stroke-width="2" />
      </button>

      <AlertCircle
        v-else-if="error"
        :size="20"
        :stroke-width="2"
        class="absolute right-3 top-1/2 -translate-y-1/2 text-red-500"
      />
    </div>

    <p v-if="error" class="mt-2 text-sm text-red-500 dark:text-red-400">{{ error }}</p>
    <p v-else-if="hint" class="mt-2 text-sm text-gray-500">{{ hint }}</p>
  </div>
</template>

<script setup lang="ts">
import { AlertCircle, Eye, EyeOff } from 'lucide-vue-next'

interface Props {
  id?: string
  label?: string
  type?: 'text' | 'email' | 'password' | 'number' | 'tel' | 'url'
  modelValue?: string | number
  placeholder?: string
  required?: boolean
  disabled?: boolean
  error?: string
  hint?: string
  icon?: string
  size?: 'sm' | 'md' | 'lg'
  autocomplete?: string
}

const props = withDefaults(defineProps<Props>(), {
  type: 'text',
  size: 'md'
})

defineEmits<{
  'update:modelValue': [value: string]
  blur: []
  focus: []
}>()

const showPassword = ref(false)
const resolvedType = computed(() =>
  props.type === 'password' ? (showPassword.value ? 'text' : 'password') : props.type
)

const inputClasses = computed(() => {
  const base = 'block w-full border rounded-lg transition-all duration-200 focus:outline-none focus:ring-2 text-gray-900 dark:text-white placeholder:text-gray-400 dark:placeholder:text-gray-500'
  const sizes = {
    sm: 'px-3 py-1.5 text-sm',
    md: 'px-4 py-2.5 text-base',
    lg: 'px-5 py-3 text-lg'
  }
  const hasPrefix = false // prefix handled via pl override
  const iconPadding  = (props.icon || props.$slots?.prefix) ? 'pl-10' : ''
  const rightPadding = (props.type === 'password' || props.error) ? 'pr-10' : ''
  const state = props.error
    ? 'border-red-400 dark:border-red-500/50 focus:border-red-500 focus:ring-red-500/50 bg-red-50 dark:bg-red-500/5'
    : 'border-gray-300 dark:border-gray-700 focus:border-primary-500 focus:ring-primary-500/50 bg-white dark:bg-white/5 hover:border-gray-400 dark:hover:border-gray-600'
  const disabled = props.disabled ? 'bg-gray-100 dark:bg-gray-800/50 cursor-not-allowed opacity-60' : ''
  return `${base} ${sizes[props.size]} ${iconPadding} ${rightPadding} ${state} ${disabled}`
})
</script>
