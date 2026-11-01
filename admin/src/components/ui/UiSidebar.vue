<script setup lang="ts">
/**
 * Collapsible icon sidebar for a section's sub-navigation.
 *
 * Shows icons only and expands on hover/focus to reveal the captions. The sidebar always keeps its
 * narrow slot in the layout and the expanded panel floats over the content, so opening it never
 * pushes or re-lays-out the page. Opening and closing wait a moment (hover intent), so moving the
 * pointer back and forth across the edge does not restart the animation. Captions have a fixed
 * width, so they never re-wrap while the panel grows, and fade in after it has started to open.
 * Icons are drawn from stored SVG markup by `UiIcon` (no `v-html`). The active entry is marked by
 * `activeKey`, so the sidebar knows nothing about the router state.
 */
import { onUnmounted, ref } from 'vue'
import { RouterLink } from 'vue-router'
import UiIcon from '@/components/ui/UiIcon.vue'
import type { UiSidebarItem } from '@/utils/ui/sidebar'

/** Props for UiSidebar. */
withDefaults(defineProps<{
  /** Entries in order. */
  items: UiSidebarItem[]
  /** Key of the entry for the current page. */
  activeKey?: string
  /** Prefix of each entry's `data-testid` (`<prefix>-<key>`). */
  testid?: string
}>(), {
  activeKey: undefined,
  testid: 'sidebar',
})

/** Pause before the panel opens once the pointer is over it, in milliseconds. */
const OPEN_DELAY = 120

/** Pause before the panel closes once the pointer has left it, in milliseconds. */
const CLOSE_DELAY = 250

/** Whether the sidebar is expanded. */
const expanded = ref(false)

/** Pending open or close, cancelled whenever the pointer comes back. */
let timer: ReturnType<typeof setTimeout> | undefined

/** Cancels the pending open or close. */
const clearTimer = (): void => {
  clearTimeout(timer)
  timer = undefined
}

/**
 * Expands or collapses the panel after a delay.
 *
 * @param open - Whether the panel should end up expanded.
 */
const schedule = (open: boolean): void => {
  clearTimer()
  timer = setTimeout(() => {
    expanded.value = open
  }, open ? OPEN_DELAY : CLOSE_DELAY)
}

/** Expands at once for the keyboard, where there is no pointer to wait on. */
const openNow = (): void => {
  clearTimer()
  expanded.value = true
}

onUnmounted(clearTimer)
</script>

<template>
  <div class="relative w-[48px] shrink-0">
    <!-- The slot keeps the narrow width in the layout; the panel inside grows over the content. -->
    <div
      class="absolute inset-y-0 left-0 z-20 flex flex-col overflow-y-auto overflow-x-hidden border-r border-border bg-surface-card transition-[width,box-shadow] duration-300 ease-in-out"
      :class="expanded ? 'w-[260px] shadow-xl' : 'w-[48px]'"
      data-testid="ui-sidebar-panel"
      @mouseenter="schedule(true)"
      @mouseleave="schedule(false)"
      @focusin="openNow"
      @focusout="schedule(false)"
    >
      <nav class="flex flex-col gap-0.5 pt-2 pb-4">
        <RouterLink
          v-for="item in items"
          :key="item.key"
          :to="item.to"
          :aria-current="item.key === activeKey ? 'page' : undefined"
          :data-testid="`${testid}-${item.key}`"
          class="relative flex items-center gap-2.5 px-3 py-2.5 transition-colors duration-150 no-underline outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-primary-500/60"
          :class="item.key === activeKey
            ? 'text-primary-500 light:text-primary-600 bg-primary-500/15'
            : 'text-text-secondary hover:text-text-primary hover:bg-surface-hover'"
        >
          <span
            v-if="item.key === activeKey"
            class="absolute left-0 top-1/2 -translate-y-1/2 w-[3px] h-[18px] rounded-r-full gradient-brand"
          />
          <UiIcon :markup="item.icon" size="lg" class="shrink-0" />
          <!-- Fixed width (260 - paddings - icon - gap): the caption never re-wraps while the panel grows. -->
          <span
            class="w-[204px] shrink-0 text-[0.78rem] font-medium leading-snug transition-opacity duration-200"
            :class="expanded ? 'opacity-100 delay-150' : 'opacity-0'"
          >
            {{ item.label }}
          </span>
        </RouterLink>
      </nav>
    </div>
  </div>
</template>
