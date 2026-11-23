<template>
  <UiSection v-if="stats.length" :contained="false" padding="lg" class="bg-classic-surface border-y border-classic-border" data-testid="sections-performance">
    <UiContainer>
      <ClassicSectionHeader
        class="mb-12"
        :eyebrow="$t('hostingSections.performance.badge')"
        :title="$t('hostingSections.performance.title')"
        :subtitle="$t('hostingSections.performance.subtitle')"
      />

      <UiBalancedGrid :count="stats.length" data-testid="sections-performance-grid">
        <ClassicCard
          v-for="stat in stats"
          :key="stat.key"
          class="text-center"
          :data-testid="`performance-${stat.key}`"
        >
          <UiStack align="center" gap="sm">
            <ClassicIconTile :icon="STAT_ICONS[stat.key]" />
            <UiText :as="UiTextTag.P" tone="inherit" weight="bold" class="text-3xl md:text-4xl tabular-nums text-classic-ink" :data-testid="`performance-value-${stat.key}`">{{ stat.value }}</UiText>
            <UiText tone="inherit" size="sm" class="text-classic-muted">{{ $t(`hostingSections.performance.stats.${stat.key}`) }}</UiText>
          </UiStack>
        </ClassicCard>
      </UiBalancedGrid>
    </UiContainer>
  </UiSection>
</template>

<script setup lang="ts">
/**
 * Performance section: the figures the operator publishes (uptime, response time, sites hosted,
 * support response) as large numbers. Only set figures show; with none the section renders nothing.
 */
import { PerformanceStatKey } from '~/constants/performanceStatKey'
import { UiTextTag } from '~/utils/ui/enums'

/** The published figures, in display order. */
const stats = usePerformanceStats()

/** Icon of each figure. */
const STAT_ICONS: Record<PerformanceStatKey, string> = {
  [PerformanceStatKey.Uptime]: 'mdi:server-network',
  [PerformanceStatKey.ResponseTime]: 'mdi:speedometer',
  [PerformanceStatKey.SitesHosted]: 'mdi:web',
  [PerformanceStatKey.SupportResponse]: 'mdi:timer-outline',
}
</script>
