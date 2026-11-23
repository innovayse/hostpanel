<template>
  <UiSection v-if="stats.length" padding="lg" :contained="false" class="relative border-t border-line" data-testid="aurora-performance">
    <UiText tone="inherit" size="inherit" :class="[EYEBROW_CLASS, 'text-ac2']">{{ t('aurora.performance.eyebrow') }}</UiText>
    <UiHeading :level="2" size="bare" :class="[SECTION_TITLE_CLASS, 'mt-3']">
      {{ t('aurora.performance.title') }}
    </UiHeading>

    <UiBalancedGrid :count="stats.length" class="mt-8" data-testid="aurora-performance-grid">
      <UiCard
        v-for="stat in stats"
        :key="stat.key"
        tone="bare"
        :data-testid="`performance-${stat.key}`"
        class="border-line bg-surf text-center shadow-card"
      >
        <UiStack gap="xs" align="center">
          <UiTile size="lg" :icon="ICONS[stat.key]" class="bg-tile-mint text-white" />
          <UiText :as="UiTextTag.Span" size="inherit" tone="inherit" weight="bold" class="font-display text-[clamp(26px,3vw,34px)] text-tx" :data-testid="`performance-value-${stat.key}`">{{ stat.value }}</UiText>
          <UiText :as="UiTextTag.Span" size="inherit" tone="inherit" class="text-[14px] text-mut">{{ t(`aurora.performance.${stat.key}`) }}</UiText>
        </UiStack>
      </UiCard>
    </UiBalancedGrid>
  </UiSection>
</template>

<script setup lang="ts">
/**
 * aurora performance figures: uptime, response time, sites hosted and support response, each
 * as a large value over its label. Only the figures the operator filled in appear; with none
 * the section renders nothing.
 */
import type { Component } from 'vue'
import { Clock, Gauge, Globe, Headset } from 'lucide-vue-next'
import { PerformanceStatKey } from '~/constants/performanceStatKey'
import { usePerformanceStats } from '~/composables/usePerformanceStats'
import { EYEBROW_CLASS, SECTION_TITLE_CLASS } from '~/templates/aurora/typography'
import UiStack from '~/components/ui/UiStack.vue'
import { UiTextTag } from '~/utils/ui/enums'

/** Translator for the active locale. */
const { t } = useI18n()

/** The figures that are set, in display order. */
const stats = usePerformanceStats()

/** The tile icon of each figure. */
const ICONS: Record<PerformanceStatKey, Component> = {
  [PerformanceStatKey.Uptime]: Clock,
  [PerformanceStatKey.ResponseTime]: Gauge,
  [PerformanceStatKey.SitesHosted]: Globe,
  [PerformanceStatKey.SupportResponse]: Headset,
}
</script>
