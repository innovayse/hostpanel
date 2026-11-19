<script setup lang="ts">
/**
 * The placeholder of a bar of action buttons: one button shaped bar per action.
 *
 * On a phone the real bar keeps only its primary action as a button and folds the rest into one "…" menu
 * (`useFoldedActions`), so the placeholder does the same: the first bar stays, the others hide below `sm` and one square
 * bar stands in for the menu. It is drawn by the skeleton of a page header, a list toolbar and a tab layout's header card,
 * so the three fold the same way the screens they stand in for do.
 */
import UiSkeletonBar from '@/components/ui/UiSkeletonBar.vue'

/** Props for UiSkeletonActions. */
withDefaults(defineProps<{
  /** How many actions the bar has. */
  count: number
  /** Draws the smaller buttons of a card's header, not those of a page header. */
  compact?: boolean
  /** Index of the action that stays a button on a phone, as the real bar's `primaryIndex` picks it; the first when omitted. */
  primary?: number
}>(), {
  compact: false,
  primary: 0,
})
</script>

<template>
  <template v-if="count > 0">
    <UiSkeletonBar
      v-for="n in count"
      :key="n"
      class="w-28 shrink-0 rounded-control"
      :class="[compact ? 'h-9' : 'h-10', n - 1 !== primary ? 'max-sm:hidden' : '']"
      aria-hidden="true"
    />
    <UiSkeletonBar
      v-if="count > 1"
      class="shrink-0 rounded-control sm:hidden"
      :class="compact ? 'size-9' : 'size-10'"
      aria-hidden="true"
      data-testid="ui-skeleton-actions-more"
    />
  </template>
</template>
