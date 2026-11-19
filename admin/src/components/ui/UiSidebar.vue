<script setup lang="ts">
/**
 * Collapsible icon sidebar for a section's sub-navigation, drawn on the frosted `glass` surface.
 *
 * Folded it is a rail of centred icons (each with its caption as tooltip and accessible name) and it opens on
 * hover or focus to reveal the captions, which are cut with an ellipsis (the full caption is the tooltip) when they
 * do not fit. Entries that share a `group` sit under one heading (a hairline in the rail). The optional `header` slot
 * (a search box) is drawn only while the sidebar is open. The sidebar scrolls on its own when its entries are taller than
 * it. Icons are drawn from stored SVG markup by `UiIcon` (no `v-html`). The active entry is marked by `activeKey`, so
 * the sidebar knows nothing about the router state.
 */
import { computed, ref, watch } from 'vue'
import { RouterLink } from 'vue-router'
import UiIcon from '@/components/ui/UiIcon.vue'
import { navItemTextClasses } from '@/utils/ui/nav'
import type { UiSidebarItem } from '@/utils/ui/sidebar'
import { sidebarEntryClasses, sidebarListClasses, sidebarOpenWidthClass, sidebarRailWidthClass, sidebarRows, sidebarShellClasses } from '@/utils/ui/sidebar'

/** Props for UiSidebar. */
const props = withDefaults(defineProps<{
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

const emit = defineEmits<{
  /** The sidebar opened or folded; lets a caller whose header filters the entries show every entry in the rail. */
  'update:expanded': [expanded: boolean]
}>()

/** Whether the pointer is over the sidebar. */
const hovered = ref(false)

/** Whether focus is inside the sidebar (a search box keeps it open while it is typed in). */
const focused = ref(false)

/** Whether the sidebar is expanded. */
const expanded = computed<boolean>(() => hovered.value || focused.value)

watch(expanded, value => {
  emit('update:expanded', value)
})

/** The entries with the heading each group opens with. */
const rows = computed(() => sidebarRows(props.items))
</script>

<template>
  <div
    :class="[sidebarShellClasses, expanded ? sidebarOpenWidthClass : sidebarRailWidthClass]"
    @mouseenter="hovered = true"
    @mouseleave="hovered = false"
    @focusin="focused = true"
    @focusout="focused = false"
  >
    <div v-if="expanded && $slots.header" class="px-2 pt-2">
      <slot name="header" />
    </div>
    <nav :aria-label="ariaLabel" :class="sidebarListClasses">
      <template v-for="row in rows" :key="row.item.key">
        <p
          v-if="row.heading !== undefined && expanded"
          class="truncate px-3 pt-2 text-xs font-semibold uppercase tracking-wider text-text-muted"
          :data-testid="`${testid}-group-${row.item.key}`"
        >
          {{ row.heading }}
        </p>
        <div v-else-if="row.heading !== undefined" class="mx-2 mt-1 border-t border-border" aria-hidden="true" />
        <RouterLink
          :to="row.item.to"
          :aria-current="row.item.key === activeKey ? 'page' : undefined"
          :aria-label="row.item.label"
          :title="row.item.title ?? row.item.label"
          :data-testid="`${testid}-${row.item.key}`"
          class="transition-all duration-150 no-underline outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-primary-500/60"
          :class="[sidebarEntryClasses, navItemTextClasses, expanded ? 'gap-2.5 px-3' : 'justify-center px-0', row.item.key === activeKey
            ? 'border-(--nav-active-border) bg-(--nav-active-bg) text-text-primary'
            : 'border-transparent text-text-secondary hover:text-text-primary hover:bg-surface-hover']"
        >
          <UiIcon :markup="row.item.icon" size="lg" class="shrink-0" />
          <span v-if="expanded" class="min-w-0 flex-1 truncate">
            {{ row.item.label }}
          </span>
        </RouterLink>
      </template>
    </nav>
  </div>
</template>
