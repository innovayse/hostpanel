<template>
  <div class="flex flex-col sm:flex-row gap-3">
    <input
      :value="modelValue"
      type="text"
      :placeholder="placeholder"
      :disabled="loading"
      class="flex-1 px-5 py-4 rounded-xl bg-white dark:bg-white/5 border border-gray-300 dark:border-white/20 text-gray-900 dark:text-white placeholder-gray-400 dark:placeholder-gray-500 focus:outline-none focus:border-primary-500/60 transition-all duration-200 text-base"
      @input="$emit('update:modelValue', ($event.target as HTMLInputElement).value)"
      @keydown.enter="$emit('search')"
    />
    <button
      type="button"
      class="px-8 py-4 rounded-xl font-bold text-white bg-gradient-to-r from-primary-600 to-cyan-600 hover:from-primary-500 hover:to-cyan-500 transition-all duration-300 hover:scale-105 shadow-lg shadow-primary-500/30 flex items-center gap-2 justify-center disabled:opacity-60 disabled:cursor-not-allowed disabled:hover:scale-100"
      :disabled="loading"
      @click="$emit('search')"
    >
      <Loader2 v-if="loading" :size="20" :stroke-width="2" class="animate-spin" />
      <Search v-else :size="20" :stroke-width="2" />
      {{ buttonLabel }}
    </button>
  </div>
</template>

<script setup lang="ts">
import { Search, Loader2 } from 'lucide-vue-next'

interface Props {
  modelValue?: string
  placeholder?: string
  loading?: boolean
  buttonLabel?: string
}

withDefaults(defineProps<Props>(), {
  modelValue: '',
  placeholder: '',
  loading: false,
  buttonLabel: 'Search'
})

defineEmits<{
  'update:modelValue': [value: string]
  search: []
}>()
</script>
