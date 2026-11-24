<template>
  <UiStack :as="UiStackTag.Li" gap="none" class="h-full">
    <UiCard
      padding="lg"
      class="flex h-full flex-col transition-[transform,border-color] motion-safe:hover:-translate-y-1 !border-nova-border !bg-nova-surface hover:!border-nova-brand"
    >
      <UiIconTile :icon="feature.icon" size="lg" tone="nova" />
      <UiHeading :level="3" size="bare" class="mt-5 text-[17px] font-bold text-nova-ink">
        {{ t(feature.titleKey) }}
      </UiHeading>
      <UiText
        v-if="feature.bodyKey"
        size="inherit"
        tone="inherit"
        class="mt-2.5 text-[15px] leading-relaxed text-nova-muted"
      >
        {{ t(feature.bodyKey) }}
      </UiText>
    </UiCard>
  </UiStack>
</template>

<script setup lang="ts">
/**
 * One nova capability card.
 *
 * A list item rather than a bare div: the grids that use it are lists of
 * capabilities, and a screen reader announcing "6 items" before reading them is
 * the whole reason to mark it up that way. The surface is a `UiCard` recoloured
 * with nova's tokens (`!` so the kit's own light and dark greys cannot win).
 *
 * Whether this card should appear at all was decided by `useVisibleFeatures`
 * before it was rendered.
 */
import type { Feature } from '~/templates/nova/types/feature'
import { UiStackTag } from '~/utils/ui/enums'

/** Props of FeatureCard. */
defineProps<{
  /** The capability to state: icon, title key and optional body key. */
  feature: Feature
}>()

/** Translator for the keys the feature names. */
const { t } = useI18n()
</script>
