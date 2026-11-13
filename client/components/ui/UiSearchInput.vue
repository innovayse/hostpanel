<template>
  <div class="relative">
    <Search
      :size="14"
      :stroke-width="2"
      class="absolute left-3 top-1/2 -translate-y-1/2 text-gray-500 dark:text-gray-400 pointer-events-none"
    />
    <input
      :value="modelValue"
      type="text"
      :placeholder="placeholder ?? $t('ui.search')"
      :data-testid="testid"
      :class="inputClasses"
      @input="$emit('update:modelValue', ($event.target as HTMLInputElement).value)"
      @keydown.escape="$emit('update:modelValue', '')"
    >
    <button
      v-if="modelValue"
      type="button"
      :aria-label="$t('ui.clearSearch')"
      data-testid="search-input-clear"
      :class="clearClasses"
      @click="$emit('update:modelValue', '')"
    >
      <X :size="13" :stroke-width="2" />
    </button>
  </div>
</template>

<script setup lang="ts">
/**
 * SearchInput (`UiSearchInput`): a compact filter field with a search icon and a clear button;
 * Escape clears it too. For filtering lists in place (no submit).
 */
import { computed } from 'vue'
import { Search, X } from 'lucide-vue-next'
import { UiSize } from '~/utils/ui/enums'
import { FOCUS_RING } from '~/utils/ui/tones'

/** Props of SearchInput. */
interface Props {
  /** Query text (v-model). */
  modelValue?: string
  /** Placeholder; defaults to the translated "Search". */
  placeholder?: string
  /** Size step: `sm` or `md`. */
  size?: `${UiSize.Sm | UiSize.Md}`
  /** `data-testid` of the inner input element. */
  testid?: string
}

/** Props of the component with their defaults applied. */
const props = withDefaults(defineProps<Props>(), {
  modelValue: '',
  placeholder: undefined,
  size: UiSize.Sm,
  testid: undefined,
})

defineEmits<{
  /** The query changed. */
  'update:modelValue': [value: string]
}>()

/** Class list of the clear button: muted icon, one hover step, the kit focus ring. */
const clearClasses = `absolute right-2.5 top-1/2 -translate-y-1/2 rounded text-gray-500 dark:text-gray-400 hover:text-gray-800 dark:hover:text-gray-200 transition-colors ${FOCUS_RING}`

/** Class list of the input. */
const inputClasses = computed(() => [
  'ui-field w-full pl-8 pr-7 rounded-lg border bg-white dark:bg-white/5 text-gray-900 dark:text-white placeholder:text-gray-500 dark:placeholder:text-gray-400 border-gray-300 dark:border-gray-700 hover:border-gray-400 dark:hover:border-gray-600 focus-visible:border-primary-500 transition-all duration-200',
  FOCUS_RING,
  props.size === UiSize.Md ? 'py-2 text-base' : 'py-1.5 text-sm',
].join(' '))
</script>
