<template>
  <div class="relative">
    <Search
      :size="14"
      :stroke-width="2"
      class="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400 dark:text-gray-500 pointer-events-none"
    />
    <input
      :value="modelValue"
      type="text"
      :placeholder="placeholder"
      :class="inputClasses"
      @input="$emit('update:modelValue', ($event.target as HTMLInputElement).value)"
      @keydown.escape="$emit('update:modelValue', '')"
    />
    <button
      v-if="modelValue"
      type="button"
      class="absolute right-2.5 top-1/2 -translate-y-1/2 text-gray-400 dark:text-gray-500 hover:text-gray-600 dark:hover:text-gray-300 transition-colors"
      @click="$emit('update:modelValue', '')"
    >
      <X :size="13" :stroke-width="2" />
    </button>
  </div>
</template>

<script setup lang="ts">
import { Search, X } from 'lucide-vue-next'

interface Props {
  modelValue?: string
  placeholder?: string
  size?: 'sm' | 'md'
}

const props = withDefaults(defineProps<Props>(), {
  modelValue: '',
  placeholder: 'Search…',
  size: 'sm'
})

defineEmits<{
  'update:modelValue': [value: string]
}>()

const inputClasses = computed(() => {
  const base = 'w-full pl-8 pr-7 rounded-lg border bg-white dark:bg-white/5 text-gray-900 dark:text-white placeholder-gray-400 dark:placeholder-gray-500 border-gray-200 dark:border-white/10 focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/30 transition-all duration-200'
  const sizes = {
    sm: 'py-1.5 text-sm',
    md: 'py-2 text-base'
  }
  return `${base} ${sizes[props.size]}`
})
</script>
