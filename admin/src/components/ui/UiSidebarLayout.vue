<script setup lang="ts">
/**
 * Layout of a section that has an inner sidebar.
 *
 * The `sidebar` slot sits against the edge of the shell (the layout cancels the shell gutter so the
 * sidebar meets it, on every side by exactly the gutter `AppLayout` sets, so the sidebar starts on the
 * top bar's hairline) and the default slot is the content, which scrolls on its own beside it.
 *
 * The layout is exactly as tall as the scroller it sits in (`100cqh`, the `<main>` of `AppLayout`, which is a size
 * container), never as tall as its content: the shell's gutter wrapper only has a minimum height, so `h-full` has nothing
 * to resolve against and the whole page would scroll. With a definite height the inner sidebar scrolls by itself and so
 * does the content pane. Both columns may shrink (`min-w-0`), so a wide report never widens the shell sideways.
 *
 * A section whose sidebar would eat the width of a phone passes `strip`: from `md` up the sidebar is drawn as it is,
 * below it the sidebar is hidden and the same entries become a tab strip above the content. The strip is the one
 * place this swap lives, so the reports, the integrations and the skeleton of both cannot drift apart.
 */
import UiTabs from '@/components/ui/UiTabs.vue'
import type { UiTabItem } from '@/utils/ui/tabs'

/** Props for UiSidebarLayout. */
withDefaults(defineProps<{
  /** The entries of the tab strip shown below `md`; absent keeps the sidebar on every screen size. */
  strip?: UiTabItem[]
  /** Value of the open entry of the strip; none is marked when empty. */
  stripValue?: string
  /** `data-testid` of the tab strip. */
  stripTestid?: string
  /** Draws the strip inert, as a placeholder that cannot be used yet. */
  stripInert?: boolean
}>(), {
  strip: undefined,
  stripValue: '',
  stripTestid: undefined,
  stripInert: false,
})

/** Events of UiSidebarLayout. */
const emit = defineEmits<{
  /** A strip entry was chosen; carries its value. */
  selectStrip: [value: string]
}>()
</script>

<template>
  <div class="flex h-[100cqh] min-h-0 min-w-0 -mx-4 -my-4 sm:-mx-6 sm:-my-6 lg:-mx-8 lg:-my-7">
    <div v-if="strip" class="hidden min-h-0 md:flex">
      <slot name="sidebar" />
    </div>
    <slot v-else name="sidebar" />
    <div class="min-h-0 min-w-0 flex-1 overflow-auto p-4 sm:p-6 lg:p-8">
      <UiTabs
        v-if="strip"
        :items="strip"
        :model-value="stripValue"
        hide-scrollbar
        :inert="stripInert || undefined"
        class="mb-4 md:hidden"
        :data-testid="stripTestid"
        @update:model-value="emit('selectStrip', $event)"
      />
      <slot />
    </div>
  </div>
</template>
