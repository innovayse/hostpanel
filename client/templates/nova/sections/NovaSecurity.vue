<template>
  <UiSection v-if="features.length" padding="lg" class="border-b border-nova-border" data-testid="nova-security">
    <UiStack gap="none" class="max-w-[75ch]">
      <NovaSectionHeader
        :eyebrow="t('nova.security.eyebrow')"
        :title="t('nova.security.title')"
        :lead="t('nova.security.lead')"
      />
    </UiStack>

    <UiGrid :as="UiGridTag.Ul" cols="3" gap="lg" class="mt-10">
      <NovaFeatureCard v-for="feature in features" :key="feature.key" :feature="feature" />
    </UiGrid>
  </UiSection>
</template>

<script setup lang="ts">
/**
 * nova security section.
 *
 * The cards the operator has switched on (SSL, backups, DDoS protection, malware scanning,
 * firewall) come from `useSecurityFeatures()` and are drawn from `SECURITY_FEATURE_CARDS`;
 * two-factor sign-in and account isolation are implemented in this codebase and always stated.
 */
import NovaFeatureCard from '~/templates/nova/sections/FeatureCard.vue'
import NovaSectionHeader from '~/templates/nova/sections/NovaSectionHeader.vue'
import { SECURITY_FEATURES, SECURITY_FEATURE_CARDS } from '~/templates/nova/content'
import type { Feature } from '~/templates/nova/types/feature'
import { UiGridTag } from '~/utils/ui/enums'

/** Translator for the copy. */
const { t } = useI18n()
/** The operator-configured security claims that are switched on. */
const enabled = useSecurityFeatures()

/** Every card to render: the switched-on claims first, then the always-true statements. */
const features = computed<Feature[]>(() => [
  ...enabled.value.map(key => SECURITY_FEATURE_CARDS[key]),
  ...SECURITY_FEATURES,
])
</script>
