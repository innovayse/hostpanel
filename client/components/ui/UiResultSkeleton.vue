<template>
  <UiStack gap="xs" role="status" :aria-label="$t('ui.loading')" data-testid="ui-result-skeleton">
    <UiCard
      v-for="index in rows"
      :key="index"
      padding="none"
      aria-hidden="true"
      class="px-5 py-[18px]"
      data-testid="ui-result-skeleton-row"
    >
      <UiStack direction="row" align="center" justify="between" gap="md">
        <div class="min-w-0 flex-1" data-testid="ui-result-skeleton-name">
          <UiSkeleton :width="UiSkeletonWidth.ThreeQuarters" />
        </div>
        <UiStack direction="row" align="center" gap="lg" class="shrink-0">
          <div class="w-16" data-testid="ui-result-skeleton-price">
            <UiSkeleton />
          </div>
          <div class="w-24" data-testid="ui-result-skeleton-action">
            <UiSkeleton :shape="UiSkeletonShape.Block" class="h-10" />
          </div>
        </UiStack>
      </UiStack>
    </UiCard>
  </UiStack>
</template>

<script setup lang="ts">
/**
 * UiResultSkeleton: the loading placeholder of a list of result rows.
 *
 * Each row repeats the shape of a real result: a name that stops short, a price and an action
 * button, all drawn with the kit's `UiSkeleton`, so the list does not jump when the rows arrive.
 * The group announces one loading status; the rows themselves are hidden from assistive tech.
 */
import { UiSkeletonShape, UiSkeletonWidth } from '~/utils/ui/enums'

/** Props of UiResultSkeleton. */
interface Props {
  /** Number of placeholder rows. */
  rows?: number
}

withDefaults(defineProps<Props>(), {
  rows: 3,
})
</script>
