<script setup lang="ts">
/**
 * Tab strip.
 *
 * Renders the tab buttons (`role="tablist"`); the screen shows the matching panel from the model
 * (`v-if="tab === 'x'"`) or from the scoped slot's `active` value. The slot is drawn in a panel
 * 22px under the tab line (the standard section gap), so content never touches it. Left/Right arrow keys move
 * between enabled tabs.
 */
import type { UiTabItem } from '@/utils/ui/tabs'
import { neighbourValue } from '@/utils/ui/tabs'

/** Props for UiTabs. */
const props = withDefaults(defineProps<{
  /** Tabs in order. */
  items: UiTabItem[]
  /** Value of the selected tab (two-way bound with `v-model`). */
  modelValue: string
  /** Accessible name of the tab list, for a screen that has more than one. */
  ariaLabel?: string
  /**
   * Hides the scrollbar of a strip that overflows. The selected tab's own underline sits at the bottom edge, and
   * a scrollbar drawn there runs into it; the strip still scrolls by touch, wheel and the arrow keys.
   */
  hideScrollbar?: boolean
}>(), {
  ariaLabel: undefined,
  hideScrollbar: false,
})

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
  const target = neighbourValue(props.items, index, event.key)
  if (target !== undefined) {
    event.preventDefault()
    emit('update:modelValue', target)
  }
}
</script>

<template>
  <div>
    <div
      role="tablist"
      :aria-label="ariaLabel"
      class="flex gap-1 border-b border-border overflow-x-auto overflow-y-hidden"
      :class="hideScrollbar ? '[scrollbar-width:none] [&::-webkit-scrollbar]:hidden' : ''"
    >
      <button
        v-for="(item, index) in items"
        :key="item.value"
        type="button"
        role="tab"
        :aria-selected="item.value === modelValue"
        :tabindex="item.value === modelValue ? 0 : -1"
        :disabled="item.disabled"
        :data-testid="item.testid"
        class="px-4 py-2.5 text-[0.8125rem] font-medium whitespace-nowrap border-b-2 -mb-px transition-colors outline-none focus-visible:ring-2 focus-visible:ring-primary-500/40 disabled:opacity-40 disabled:cursor-not-allowed cursor-pointer"
        :class="item.value === modelValue
          ? 'border-primary-500 text-primary-500 light:text-primary-600'
          : 'border-transparent text-text-secondary hover:text-text-primary hover:border-border-strong'"
        @click="emit('update:modelValue', item.value)"
        @keydown="onKeydown($event, index)"
      >
        {{ item.label }}
      </button>
    </div>
    <div v-if="$slots.default" role="tabpanel" class="mt-5.5" data-testid="ui-tabs-panel">
      <slot :active="modelValue" />
    </div>
  </div>
</template>
