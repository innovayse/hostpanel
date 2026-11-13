<template>
  <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3 mt-4">
    <div class="flex items-center gap-2 text-sm text-gray-500 dark:text-gray-400">
      <span>{{ $t('ui.pagination.show') }}</span>
      <div class="w-24">
        <UiSelect
          :model-value="perPage"
          :options="sizeOptions"
          :placeholder="String(perPage)"
          size="sm"
          testid="ui-pagination-size"
          @update:model-value="changePerPage(Number($event))"
        />
      </div>
      <span>{{ $t('ui.pagination.entries') }}</span>
    </div>

    <nav class="flex items-center gap-1" :aria-label="$t('ui.pagination.label')">
      <button
        type="button"
        data-testid="ui-pagination-prev"
        class="px-3 py-1.5"
        :class="[NAV_BASE, modelValue <= 1
          ? NAV_DISABLED
          : NAV_ENABLED]"
        :disabled="modelValue <= 1"
        @click="emit('update:modelValue', modelValue - 1)"
      >{{ $t('ui.pagination.prev') }}</button>

      <button
        v-for="p in pageRange"
        :key="p"
        type="button"
        :data-testid="`ui-pagination-page-${p}`"
        :aria-current="p === modelValue ? 'page' : undefined"
        class="w-8 h-8"
        :class="[NAV_BASE, p === modelValue ? PAGE_CURRENT : NAV_ENABLED]"
        @click="emit('update:modelValue', p)"
      >{{ p }}</button>

      <button
        type="button"
        data-testid="ui-pagination-next"
        class="px-3 py-1.5"
        :class="[NAV_BASE, modelValue >= totalPages
          ? NAV_DISABLED
          : NAV_ENABLED]"
        :disabled="modelValue >= totalPages"
        @click="emit('update:modelValue', modelValue + 1)"
      >{{ $t('ui.pagination.next') }}</button>
    </nav>
  </div>
</template>

<script setup lang="ts">
/**
 * UiPagination: page buttons (a window of five around the current page) plus a page-size select.
 *
 * Changing the size emits the new size and resets the page to 1 so the reader never lands past
 * the end of the shorter list.
 */
import { computed } from 'vue'
import { FOCUS_RING } from '~/utils/ui/tones'

/** Props of UiPagination. */
interface Props {
  /** Current page, 1-based (v-model). */
  modelValue: number
  /** Number of pages. */
  totalPages: number
  /** Rows per page (v-model:per-page). */
  perPage: number
  /** Page sizes offered in the select. */
  pageSizes?: number[]
}

/** Props of the component with their defaults applied. */
const props = withDefaults(defineProps<Props>(), {
  pageSizes: () => [10, 25, 50],
})

/** Emits the component raises. */
const emit = defineEmits<{
  /** The reader moved to another page. */
  'update:modelValue': [page: number]
  /** The reader chose another page size. */
  'update:perPage': [size: number]
}>()

/** Frame shared by the prev, next and page buttons: shape, surface, transition and focus ring. */
const NAV_BASE = `rounded-lg border text-sm transition-all bg-white dark:bg-transparent ${FOCUS_RING}`

/** Look of an enabled, non-current button. */
const NAV_ENABLED = 'border-gray-200 dark:border-white/10 text-gray-600 dark:text-gray-300 hover:border-primary-500/40 hover:text-gray-900 dark:hover:text-white'

/** Look of a disabled prev/next button: dimmed but still readable. */
const NAV_DISABLED = 'border-gray-200 dark:border-white/5 text-gray-500 dark:text-gray-400 opacity-60 cursor-not-allowed'

/** Look of the current page: solid primary with the on-primary text, in both themes. */
const PAGE_CURRENT = 'bg-primary-600 border-primary-600 text-on-primary font-semibold hover:bg-primary-700'

/** Options of the page-size select. */
const sizeOptions = computed(() => props.pageSizes.map((size) => ({ value: size, label: String(size) })))

/** Pages shown as buttons: two either side of the current one. */
const pageRange = computed(() => {
  const delta = 2
  const pages: number[] = []
  for (let page = Math.max(1, props.modelValue - delta); page <= Math.min(props.totalPages, props.modelValue + delta); page++) {
    pages.push(page)
  }
  return pages
})

/**
 * Emits the new page size and returns to page 1.
 *
 * @param size The chosen page size.
 */
const changePerPage = (size: number): void => {
  emit('update:perPage', size)
  emit('update:modelValue', 1)
}
</script>
