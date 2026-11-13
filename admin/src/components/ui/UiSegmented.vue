<script setup lang="ts">
/**
 * Segmented toggle: a few mutually exclusive options in one pill-shaped group (a "30 days / 90 days" switch).
 *
 * A radio group (`role="radiogroup"`) of buttons: the selected one is raised on the active surface in the
 * primary text colour, the others stay muted. Sized as the concept toggle: a 4px padded frame with 6px gaps,
 * 32px high buttons with 13px medium text. Left/Right arrow keys move between enabled options, and only the
 * selected one is in the tab order.
 */
import { neighbourValue } from '@/utils/ui/tabs'
import type { UiTabItem } from '@/utils/ui/tabs'

/** Props for UiSegmented. */
const props = withDefaults(defineProps<{
  /** Options in order. */
  items: UiTabItem[]
  /** Value of the selected option (two-way bound with `v-model`). */
  modelValue: string
  /** Accessible name of the group. */
  ariaLabel?: string
}>(), {
  ariaLabel: undefined,
})

const emit = defineEmits<{
  /** Emitted with the value of the option the user selected. */
  'update:modelValue': [value: string]
}>()

/**
 * Moves the selection to the neighbouring enabled option.
 *
 * @param event - The keydown event; only Left and Right are handled.
 * @param index - Index of the option that has focus.
 */
const onKeydown = (event: KeyboardEvent, index: number): void => {
  const target = neighbourValue(props.items, index, event.key)
  if (target !== undefined) {
    event.preventDefault()
    emit('update:modelValue', target)
  }
}
</script>

<template>
  <div role="radiogroup" :aria-label="ariaLabel" class="inline-flex gap-1.5 rounded-control bg-surface-field p-1">
    <button
      v-for="(item, index) in items"
      :key="item.value"
      type="button"
      role="radio"
      :aria-checked="item.value === modelValue"
      :tabindex="item.value === modelValue ? 0 : -1"
      :disabled="item.disabled"
      :data-testid="item.testid"
      class="h-8 cursor-pointer rounded-[9px] px-3.5 text-[0.8125rem] font-medium whitespace-nowrap transition-colors outline-none focus-visible:ring-2 focus-visible:ring-primary-500/40 disabled:cursor-not-allowed disabled:opacity-40"
      :class="item.value === modelValue ? 'bg-surface-active text-text-primary' : 'text-text-muted hover:text-text-primary'"
      @click="emit('update:modelValue', item.value)"
      @keydown="onKeydown($event, index)"
    >
      {{ item.label }}
    </button>
  </div>
</template>
