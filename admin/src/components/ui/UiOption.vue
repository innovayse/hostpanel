<script setup lang="ts">
/**
 * One option of a listbox: a row that is chosen with a click, is highlighted while it is the active one and scrolls
 * itself into view when it becomes active, so the arrow keys can walk a long list without the highlight leaving the
 * visible part. The listbox that owns it keeps the active index and the keyboard; `id` and `data-testid` fall through
 * to the row.
 */
import { nextTick, ref, watch } from 'vue'

/** Props for UiOption. */
const props = defineProps<{
  /** Whether this option is the highlighted one (`aria-selected`). */
  active: boolean
}>()

/** Events emitted by UiOption. */
const emit = defineEmits<{
  /** The option was clicked. */
  select: []
  /** The pointer moved over the option, so the listbox can make it the active one. */
  hover: []
}>()

/** The row element, scrolled into view when the option becomes active. */
const row = ref<HTMLElement | null>(null)

// The highlighted option stays visible while the arrow keys walk a long list.
watch(() => props.active, async (active) => {
  if (active) {
    await nextTick()
    row.value?.scrollIntoView?.({ block: 'nearest' })
  }
})
</script>

<template>
  <div
    ref="row"
    role="option"
    :aria-selected="active"
    class="flex cursor-pointer items-center gap-3 rounded-control px-3 py-2 text-sm transition-colors"
    :class="active ? 'bg-surface-hover text-text-primary' : 'text-text-secondary'"
    @click="emit('select')"
    @mousemove="emit('hover')"
  >
    <slot />
  </div>
</template>
