<template>
  <section v-if="features.length" class="border-b border-nova-border py-16 lg:py-24">
    <div class="mx-auto max-w-[1240px] px-4 sm:px-6 lg:px-8">
      <p class="text-[13px] font-semibold uppercase tracking-[0.1em] text-nova-brand">
        {{ t('nova.why.eyebrow') }}
      </p>
      <h2 class="mt-3 text-[clamp(1.6rem,3.4vw,2.4rem)] font-bold tracking-tight text-nova-ink">
        {{ t('nova.why.title') }}
      </h2>
      <p class="mt-3 max-w-[620px] text-[16px] leading-[1.7] text-nova-muted">{{ t('nova.why.lead') }}</p>

      <ul class="mt-10 grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
        <NovaFeatureCard v-for="feature in features" :key="feature.key" :feature="feature" />
      </ul>
    </div>
  </section>
</template>

<script setup lang="ts">
/**
 * nova "why Innovayse" grid. Every card states something the platform does,
 * so none of them is gated — but the section still guards on an empty list,
 * because a heading with nothing under it is worse than no heading.
 */
import NovaFeatureCard from '~/templates/nova/sections/FeatureCard.vue'
import { WHY_FEATURES } from '~/templates/nova/content'
import { useVisibleFeatures } from '~/templates/nova/features'

const { t } = useI18n()
const features = useVisibleFeatures(WHY_FEATURES)
</script>
