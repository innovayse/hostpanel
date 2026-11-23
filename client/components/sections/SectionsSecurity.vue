<template>
  <UiSection v-if="features.length" :contained="false" padding="lg" class="bg-classic-page" data-testid="sections-security">
    <UiContainer>
      <ClassicSectionHeader
        class="mb-12"
        :eyebrow="$t('hostingSections.security.badge')"
        :title="$t('hostingSections.security.title')"
        :subtitle="$t('hostingSections.security.subtitle')"
      />

      <UiBalancedGrid :count="features.length" data-testid="sections-security-grid">
        <ClassicCard
          v-for="feature in features"
          :key="feature"
          hover
          :data-testid="`security-${feature}`"
        >
          <UiStack gap="sm">
            <ClassicIconTile :icon="FEATURE_ICONS[feature]" />
            <UiHeading :level="3" size="bare" class="text-lg font-semibold text-classic-ink">{{ $t(`hostingSections.security.features.${feature}.title`) }}</UiHeading>
            <UiText tone="inherit" size="sm" class="text-classic-muted">{{ $t(`hostingSections.security.features.${feature}.description`) }}</UiText>
          </UiStack>
        </ClassicCard>
      </UiBalancedGrid>
    </UiContainer>
  </UiSection>
</template>

<script setup lang="ts">
/**
 * Security section: one card per protection the plans include (SSL, backups, DDoS protection,
 * malware scanning, firewall). Only enabled features show; with none the section renders nothing.
 */
import { SecurityFeatureKey } from '~/constants/securityFeatureKey'

/** The enabled protections, in display order. */
const features = useSecurityFeatures()

/** Icon of each protection. */
const FEATURE_ICONS: Record<SecurityFeatureKey, string> = {
  [SecurityFeatureKey.Ssl]: 'mdi:lock-check',
  [SecurityFeatureKey.Backups]: 'mdi:backup-restore',
  [SecurityFeatureKey.Ddos]: 'mdi:shield-alert',
  [SecurityFeatureKey.Malware]: 'mdi:bug-check',
  [SecurityFeatureKey.Firewall]: 'mdi:wall-fire',
}
</script>
