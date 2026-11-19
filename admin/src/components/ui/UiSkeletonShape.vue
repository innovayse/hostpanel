<script setup lang="ts">
/**
 * Placeholder of one part a report's data fills in, whose place on the screen is fixed: a world map, lines of text
 * (totals per currency, a balance), a notice bar or a line of small print.
 *
 * `UiSkeletonOf` draws it for the matching part of a report schema while the screen's chunk downloads, and `ReportPage`
 * draws it in the same place while the report loads, so the page does not change shape when the answer arrives.
 */
import UiCard from '@/components/ui/UiCard.vue'
import UiSkeletonBar from '@/components/ui/UiSkeletonBar.vue'
import UiStack from '@/components/ui/UiStack.vue'
import { SkeletonPartKind } from '@/utils/ui/skeletonSchema'

/** Props for UiSkeletonShape. */
defineProps<{
  /** Which part the placeholder stands in for. */
  kind: typeof SkeletonPartKind.Map | typeof SkeletonPartKind.Lines | typeof SkeletonPartKind.Alert | typeof SkeletonPartKind.Note
}>()
</script>

<template>
  <UiCard v-if="kind === SkeletonPartKind.Map" padding="md" aria-hidden="true" data-testid="ui-skeleton-map">
    <UiSkeletonBar class="aspect-[1010/666] w-full rounded-md" />
  </UiCard>
  <UiStack v-else-if="kind === SkeletonPartKind.Lines" gap="xs" aria-hidden="true" data-testid="ui-skeleton-lines">
    <UiSkeletonBar class="h-5 w-64 max-w-full rounded-md" />
  </UiStack>
  <UiSkeletonBar v-else-if="kind === SkeletonPartKind.Alert" class="h-16 w-full rounded-control" aria-hidden="true" data-testid="ui-skeleton-alert" />
  <UiSkeletonBar v-else class="h-4 w-72 max-w-full rounded-md" aria-hidden="true" data-testid="ui-skeleton-note" />
</template>
