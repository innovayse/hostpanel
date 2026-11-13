<script setup lang="ts">
/**
 * Stand-in shown in place of a screen whose code is still downloading.
 *
 * `RoutedView` shows it as soon as a navigation passes the guards, so a click is answered at once
 * instead of the content area sitting on the old page until the chunk arrives. It sketches a
 * generic screen (a title, a row of cards and a table card) out of shimmering placeholders; the
 * screen's own skeleton takes over from it.
 */
import { useI18n } from 'vue-i18n'
import UiCard from '@/components/ui/UiCard.vue'
import UiSkeleton from '@/components/ui/UiSkeleton.vue'
import UiStack from '@/components/ui/UiStack.vue'
import { UiSkeletonShape } from '@/utils/ui/skeleton'

/** Translation function. */
const { t } = useI18n()
</script>

<template>
  <UiStack gap="md" role="status" aria-busy="true" data-testid="view-loading">
    <UiSkeleton :lines="1" class="w-1/3" />
    <UiSkeleton :shape="UiSkeletonShape.Cards" :count="3" />
    <UiCard padding="md">
      <UiSkeleton :shape="UiSkeletonShape.Table" :count="5" />
    </UiCard>
    <span class="sr-only">{{ t('ui.loading') }}</span>
  </UiStack>
</template>
