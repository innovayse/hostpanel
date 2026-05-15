<template>
  <div class="flex items-center gap-3" :class="disabled ? 'opacity-60' : ''">
    <!-- Hidden native input for form compatibility -->
    <input
      :id="id"
      type="radio"
      :name="name"
      :value="value"
      :checked="modelValue === value"
      :disabled="disabled"
      class="sr-only"
      @change="emit('update:modelValue', value)"
    />

    <!-- Custom radio circle -->
    <button
      type="button"
      role="radio"
      :aria-checked="modelValue === value"
      :disabled="disabled"
      class="flex-shrink-0 w-5 h-5 rounded-full border-2 transition-all duration-150 flex items-center justify-center focus:outline-none focus:ring-2 focus:ring-primary-500/40"
      :class="modelValue === value
        ? 'border-primary-500 dark:border-primary-400 bg-transparent'
        : 'bg-transparent border-gray-300 dark:border-white/30 hover:border-primary-500 dark:hover:border-primary-400'"
      :style="disabled ? 'cursor: not-allowed' : 'cursor: pointer'"
      @click="!disabled && emit('update:modelValue', value)"
    >
      <!-- Inner dot when selected -->
      <span
        v-if="modelValue === value"
        class="w-2.5 h-2.5 rounded-full bg-primary-500 dark:bg-primary-400"
      />
    </button>

    <!-- Label -->
    <label
      v-if="label || $slots.default"
      :for="id"
      class="text-sm text-gray-700 dark:text-gray-300 leading-snug select-none"
      :class="disabled ? 'cursor-not-allowed' : 'cursor-pointer hover:text-gray-900 dark:hover:text-white'"
      @click.prevent="!disabled && emit('update:modelValue', value)"
    >
      {{ label }}
      <slot />
      <p v-if="description" class="text-gray-500 dark:text-gray-400 text-xs mt-0.5 font-normal">{{ description }}</p>
    </label>
  </div>
</template>

<script setup lang="ts">
interface Props {
  id?: string
  name: string
  value: string | number | boolean
  label?: string
  description?: string
  modelValue?: string | number | boolean
  disabled?: boolean
}

const props = defineProps<Props>()
const emit  = defineEmits<{ 'update:modelValue': [value: string | number | boolean] }>()
</script>
