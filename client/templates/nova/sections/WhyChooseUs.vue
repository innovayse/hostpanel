<template>
  <UiSection v-if="features.length" padding="lg" class="border-b border-nova-border">
    <NovaSectionHeader
      :eyebrow="t('nova.why.eyebrow', { site: siteName })"
      :title="t('nova.why.title', { site: siteName })"
      :lead="t('nova.why.lead')"
    />

    <UiGrid :as="UiGridTag.Ul" cols="3" gap="lg" class="mt-10">
      <NovaFeatureCard v-for="feature in features" :key="feature.key" :feature="feature" />
    </UiGrid>
  </UiSection>
</template>

<script setup lang="ts">
/**
 * nova "why choose us" grid. Every card states something the platform does,
 * so none of them is gated, but the section still guards on an empty list,
 * because a heading with nothing under it is worse than no heading.
 */
import NovaFeatureCard from '~/templates/nova/sections/FeatureCard.vue'
import NovaSectionHeader from '~/templates/nova/sections/NovaSectionHeader.vue'
import { WHY_FEATURES } from '~/templates/nova/content'
import { useVisibleFeatures } from '~/templates/nova/features'
import { UiGridTag } from '~/utils/ui/enums'

/** Translator for the copy. */
const { t } = useI18n()

/** Operator-set site name that the copy interpolates as `{site}`. */
const { name: siteName } = useSiteIdentity()
/** The cards this install may show. */
const features = useVisibleFeatures(WHY_FEATURES)
</script>
