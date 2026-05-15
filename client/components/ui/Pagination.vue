<template>
  <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3 mt-4">
    <div class="flex items-center gap-2 text-sm text-gray-500 dark:text-gray-400">
      <span>{{ $t('ui.pagination.show') }}</span>
      <div class="w-24">
        <UiSelect
          :model-value="perPage"
          :options="sizeOptions"
          size="sm"
          @update:model-value="emit('update:perPage', Number($event)); emit('update:modelValue', 1)"
        />
      </div>
      <span>{{ $t('ui.pagination.entries') }}</span>
    </div>

    <div class="flex items-center gap-1">
      <button
        class="px-3 py-1.5 rounded-lg border text-sm transition-all bg-white dark:bg-transparent"
        :class="modelValue <= 1
          ? 'border-gray-200 dark:border-white/5 text-gray-300 dark:text-gray-600 cursor-not-allowed'
          : 'border-gray-200 dark:border-white/10 text-gray-600 dark:text-gray-400 hover:border-primary-500/40 hover:text-gray-900 dark:hover:text-white'"
        :disabled="modelValue <= 1"
        @click="emit('update:modelValue', modelValue - 1)"
      >{{ $t('ui.pagination.prev') }}</button>

      <button
        v-for="p in pageRange"
        :key="p"
        class="w-8 h-8 rounded-lg border text-sm transition-all"
        :class="p === modelValue
          ? 'bg-primary-500 border-primary-500 text-white font-semibold'
          : 'bg-white dark:bg-transparent border-gray-200 dark:border-white/10 text-gray-600 dark:text-gray-400 hover:border-primary-500/40 hover:text-gray-900 dark:hover:text-white'"
        @click="emit('update:modelValue', p)"
      >{{ p }}</button>

      <button
        class="px-3 py-1.5 rounded-lg border text-sm transition-all bg-white dark:bg-transparent"
        :class="modelValue >= totalPages
          ? 'border-gray-200 dark:border-white/5 text-gray-300 dark:text-gray-600 cursor-not-allowed'
          : 'border-gray-200 dark:border-white/10 text-gray-600 dark:text-gray-400 hover:border-primary-500/40 hover:text-gray-900 dark:hover:text-white'"
        :disabled="modelValue >= totalPages"
        @click="emit('update:modelValue', modelValue + 1)"
      >{{ $t('ui.pagination.next') }}</button>
    </div>
  </div>
</template>

<script setup lang="ts">
interface Props {
  modelValue: number
  totalPages: number
  perPage: number
  pageSizes?: number[]
}

const props = withDefaults(defineProps<Props>(), { pageSizes: () => [10, 25, 50] })

const emit = defineEmits<{
  'update:modelValue': [page: number]
  'update:perPage': [size: number]
}>()

const sizeOptions = computed(() =>
  props.pageSizes.map(n => ({ value: n, label: String(n) }))
)

const pageRange = computed(() => {
  const delta = 2
  const pages: number[] = []
  for (let i = Math.max(1, props.modelValue - delta); i <= Math.min(props.totalPages, props.modelValue + delta); i++) {
    pages.push(i)
  }
  return pages
})
</script>
