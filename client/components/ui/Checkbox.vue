<template>
  <div class="flex items-start gap-3" :class="disabled ? 'opacity-60' : ''">
    <input
      :id="id"
      type="checkbox"
      :checked="modelValue"
      :disabled="disabled"
      :required="required"
      class="sr-only"
      @change="handleChange"
    />
    <button
      type="button"
      role="checkbox"
      :aria-checked="modelValue"
      :disabled="disabled"
      class="flex-shrink-0 mt-0.5 w-5 h-5 rounded border-2 transition-all duration-150 flex items-center justify-center focus:outline-none focus:ring-2 focus:ring-primary-500/40"
      :class="modelValue
        ? 'bg-primary-500 border-primary-500 dark:bg-primary-500 dark:border-primary-400'
        : 'bg-white dark:bg-white/5 border-gray-300 dark:border-white/20 hover:border-primary-500 dark:hover:border-primary-400'"
      :style="disabled ? 'cursor: not-allowed;' : 'cursor: pointer;'"
      @click="!disabled && emit('update:modelValue', !modelValue)"
    >
      <svg v-if="modelValue" viewBox="0 0 12 10" fill="none" class="w-3 h-3">
        <path d="M1 5l3.5 3.5L11 1" stroke="white" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" />
      </svg>
    </button>
    <div v-if="label || $slots.default" class="flex-1 text-sm leading-relaxed">
      <label
        :for="id"
        class="font-medium text-gray-700 dark:text-gray-300 transition-colors"
        :class="disabled ? 'cursor-not-allowed' : 'cursor-pointer hover:text-gray-900 dark:hover:text-white'"
        @click.prevent="!disabled && emit('update:modelValue', !modelValue)"
      >
        {{ label }}
        <slot />
      </label>
      <p v-if="description" class="text-gray-500 dark:text-gray-400 text-xs mt-1">{{ description }}</p>
    </div>
  </div>
</template>

<script setup lang="ts">
interface Props {
  id?: string
  label?: string
  description?: string
  modelValue?: boolean
  disabled?: boolean
  required?: boolean
}

const props = withDefaults(defineProps<Props>(), { modelValue: false })
const emit = defineEmits<{ 'update:modelValue': [value: boolean] }>()

function handleChange(event: Event) {
  emit('update:modelValue', (event.target as HTMLInputElement).checked)
}
</script>
