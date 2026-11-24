<script setup lang="ts">
/**
 * A screen whose feature is announced but not built yet: page header plus a centred "coming soon"
 * block. Used instead of placeholder data, so no screen shows records that do not exist.
 */
import UiCard from '@/components/ui/UiCard.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiStack from '@/components/ui/UiStack.vue'
import { UiSize } from '@/utils/ui/size'

/** Props for ComingSoonPage. */
defineProps<{
  /** Screen title (already translated). */
  title: string
  /** Line under the title. */
  subtitle: string
  /** Headline of the block ("Coming soon", already translated). */
  heading: string
  /** What will be available and when. */
  description: string
  /** Inner SVG markup of the icon shown above the headline. */
  icon: string
}>()
</script>

<template>
  <UiStack>
    <UiPageHeader :title="title" :subtitle="subtitle" data-testid="coming-soon-header" />
    <UiCard>
      <UiEmptyState :title="heading" :description="description" data-testid="coming-soon-empty">
        <template #icon>
          <UiIcon :markup="icon" :size="UiSize.Lg" />
        </template>
      </UiEmptyState>
    </UiCard>
  </UiStack>
</template>
