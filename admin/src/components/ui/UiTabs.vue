<script setup lang="ts">
/**
 * Tab strip.
 *
 * Renders the tab buttons (`role="tablist"`); the screen shows the matching panel from the model
 * (`v-if="tab === 'x'"`) or from the scoped slot's `active` value. Left/Right arrow keys move
 * between enabled tabs.
 */
import type { UiTabItem } from '@/utils/ui/tabs'
import { Key } from '@/utils/ui/keys'

/** Props for UiTabs. */
const props = defineProps<{
  /** Tabs in order. */
  items: UiTabItem[]
  /** Value of the selected tab (two-way bound with `v-model`). */
  modelValue: string
}>()

const emit = defineEmits<{
  /** Emitted with the value of the tab the user selected. */
  'update:modelValue': [value: string]
}>()

/**
 * Moves the selection to the neighbouring enabled tab.
 *
 * @param event - The keydown event; only Left and Right are handled.
 * @param index - Index of the tab that has focus.
 */
const onKeydown = (event: KeyboardEvent, index: number): void => {
  if (event.key !== Key.ArrowRight && event.key !== Key.ArrowLeft) {
    return
  }
  const step = event.key === Key.ArrowRight ? 1 : -1
  const enabled = props.items.filter(item => item.disabled !== true)
  const current = props.items[index]
  const position = current === undefined ? -1 : enabled.indexOf(current)
  const target = enabled[(position + step + enabled.length) % enabled.length]
  if (target !== undefined) {
    event.preventDefault()
    emit('update:modelValue', target.value)
  }
}
</script>

<template>
  <div>
    <div role="tablist" class="flex gap-1 border-b border-border overflow-x-auto overflow-y-hidden">
      <button
        v-for="(item, index) in items"
        :key="item.value"
        type="button"
        role="tab"
        :aria-selected="item.value === modelValue"
        :tabindex="item.value === modelValue ? 0 : -1"
        :disabled="item.disabled"
        :data-testid="item.testid"
        class="px-4 py-2.5 text-[0.82rem] font-medium whitespace-nowrap border-b-2 -mb-px transition-colors outline-none focus-visible:ring-2 focus-visible:ring-primary-500/40 disabled:opacity-40 disabled:cursor-not-allowed cursor-pointer"
        :class="item.value === modelValue
          ? 'border-primary-500 text-primary-500 light:text-primary-600'
          : 'border-transparent text-text-secondary hover:text-text-primary hover:border-border-strong'"
        @click="emit('update:modelValue', item.value)"
        @keydown="onKeydown($event, index)"
      >
        {{ item.label }}
      </button>
    </div>
    <slot :active="modelValue" />
  </div>
</template>
