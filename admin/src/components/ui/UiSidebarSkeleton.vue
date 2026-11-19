<script setup lang="ts">
/**
 * The skeleton of an inner sidebar: the same frosted rail as `UiSidebar`, with a placeholder where each entry's icon goes.
 *
 * It takes the shell, the list and the entry box from the sidebar's own class sets, so the rail has the width, the
 * spacing and the row height of the real one. The captions are the real ones, as the tooltip and accessible name of each
 * placeholder, and nothing in it is a link or takes focus.
 */
import UiSkeletonBar from '@/components/ui/UiSkeletonBar.vue'
import { sidebarEntryClasses, sidebarListClasses, sidebarRailWidthClass, sidebarShellClasses } from '@/utils/ui/sidebar'

/** One entry of the rail. */
interface UiSidebarSkeletonEntry {
  /** Identifies the entry. */
  key: string
  /** Caption of the entry (already translated). */
  label: string
}

/** Props for UiSidebarSkeleton. */
defineProps<{
  /** The entries, one placeholder each, in order. */
  entries: readonly UiSidebarSkeletonEntry[]
}>()
</script>

<template>
  <div :class="[sidebarShellClasses, sidebarRailWidthClass]" data-testid="ui-sidebar-skeleton">
    <div :class="sidebarListClasses">
      <div
        v-for="entry in entries"
        :key="entry.key"
        :class="[sidebarEntryClasses, 'justify-center border-transparent px-0']"
        :title="entry.label"
        data-testid="ui-sidebar-skeleton-entry"
      >
        <UiSkeletonBar class="h-5 w-5 shrink-0 rounded-md" aria-hidden="true" />
        <span class="sr-only">{{ entry.label }}</span>
      </div>
    </div>
  </div>
</template>
