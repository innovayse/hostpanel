<template>
  <UiSection v-if="features.length" padding="lg" :contained="false" class="relative border-t border-line" data-testid="aurora-security">
    <UiText tone="inherit" size="inherit" :class="[EYEBROW_CLASS, 'text-ac3']">{{ t('aurora.security.eyebrow') }}</UiText>
    <UiHeading :level="2" size="bare" :class="[SECTION_TITLE_CLASS, 'mt-3']">
      {{ t('aurora.security.title') }}
    </UiHeading>

    <UiBalancedGrid :count="features.length" class="mt-8" data-testid="aurora-security-grid">
      <UiCard
        v-for="feature in features"
        :key="feature"
        tone="bare"
        hover
        :data-testid="`security-${feature}`"
        class="border-line bg-surf shadow-card transition-[border-color,transform] duration-200 hover:-translate-y-0.5 hover:border-ac1"
      >
        <UiStack gap="sm">
          <UiTile size="lg" :icon="ICONS[feature]" class="bg-tile-lilac text-white" />
          <UiText :as="UiTextTag.Span" size="inherit" tone="inherit" weight="bold" class="text-[18px] text-tx">{{ t(`aurora.security.${feature}Title`) }}</UiText>
          <UiText :as="UiTextTag.Span" size="inherit" tone="inherit" class="text-[15px] leading-relaxed text-mut">{{ t(`aurora.security.${feature}Body`) }}</UiText>
        </UiStack>
      </UiCard>
    </UiBalancedGrid>
  </UiSection>
</template>

<script setup lang="ts">
/**
 * aurora security cards: SSL, backups, DDoS protection, malware scanning and firewall, each
 * shown only when the operator switched it on. With none on the section renders nothing.
 */
import type { Component } from 'vue'
import { DatabaseBackup, Flame, Lock, ShieldAlert, ShieldCheck } from 'lucide-vue-next'
import { SecurityFeatureKey } from '~/constants/securityFeatureKey'
import { useSecurityFeatures } from '~/composables/useSecurityFeatures'
import { EYEBROW_CLASS, SECTION_TITLE_CLASS } from '~/templates/aurora/typography'
import UiStack from '~/components/ui/UiStack.vue'
import { UiTextTag } from '~/utils/ui/enums'

/** Translator for the active locale. */
const { t } = useI18n()

/** The features that are on, in display order. */
const features = useSecurityFeatures()

/** The tile icon of each feature. */
const ICONS: Record<SecurityFeatureKey, Component> = {
  [SecurityFeatureKey.Ssl]: Lock,
  [SecurityFeatureKey.Backups]: DatabaseBackup,
  [SecurityFeatureKey.Ddos]: ShieldAlert,
  [SecurityFeatureKey.Malware]: ShieldCheck,
  [SecurityFeatureKey.Firewall]: Flame,
}
</script>
