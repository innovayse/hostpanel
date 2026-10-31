<script setup lang="ts">
/**
 * Collapsible icon sidebar for a section's sub-navigation.
 *
 * Shows icons only and expands on hover/focus to reveal the captions. Icons are drawn from stored
 * SVG markup by `UiIcon` (no `v-html`). The active entry is marked by `activeKey`, so the sidebar
 * knows nothing about the router state.
 */
import { ref } from 'vue'
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
  /** Accessible name of the navigation, for a screen that has more than one. */
  ariaLabel?: string
}>(), {
  activeKey: undefined,
  testid: 'sidebar',
  ariaLabel: undefined,
})

/** Whether the sidebar is expanded. */
const expanded = ref(false)
</script>

<template>
  <div
    class="flex flex-col bg-surface-card border-r border-border transition-all duration-200 ease-in-out overflow-y-auto overflow-x-hidden shrink-0"
    :class="expanded ? 'w-[230px]' : 'w-[48px]'"
    @mouseenter="expanded = true"
    @mouseleave="expanded = false"
    @focusin="expanded = true"
    @focusout="expanded = false"
  >
    <nav :aria-label="ariaLabel" class="flex flex-col gap-0.5 pt-2 pb-4">
      <RouterLink
        v-for="item in items"
        :key="item.key"
        :to="item.to"
        :aria-current="item.key === activeKey ? 'page' : undefined"
        :data-testid="`${testid}-${item.key}`"
        class="relative flex items-center gap-2.5 px-3 py-2.5 transition-all duration-150 no-underline outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-primary-500/60"
        :class="item.key === activeKey
          ? 'text-primary-500 light:text-primary-600 bg-primary-500/15'
          : 'text-text-secondary hover:text-text-primary hover:bg-surface-hover'"
      >
        <span
          v-if="item.key === activeKey"
          class="absolute left-0 top-1/2 -translate-y-1/2 w-[3px] h-[18px] rounded-r-full gradient-brand"
        />
        <UiIcon :markup="item.icon" size="lg" />
        <span
          class="text-[0.78rem] font-medium whitespace-nowrap transition-opacity duration-200 flex-1"
          :class="expanded ? 'opacity-100' : 'opacity-0'"
        >
          {{ item.label }}
        </span>
      </RouterLink>
    </nav>
  </div>
</template>
