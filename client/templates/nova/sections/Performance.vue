<template>
  <section v-if="features.length" class="border-b border-nova-border bg-nova-surface py-16 lg:py-24">
    <div class="mx-auto max-w-[1240px] px-4 sm:px-6 lg:px-8">
      <p class="text-[13px] font-semibold uppercase tracking-[0.1em] text-nova-brand">
        {{ t('nova.performance.eyebrow') }}
      </p>
      <h2 class="mt-3 text-[clamp(1.6rem,3.4vw,2.4rem)] font-bold tracking-tight text-nova-ink">
        {{ t('nova.performance.title') }}
      </h2>
      <p class="mt-3 max-w-[620px] text-[16px] leading-[1.7] text-nova-muted">
        {{ t('nova.performance.lead') }}
      </p>

      <ul class="mt-10 grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
        <NovaFeatureCard v-for="feature in features" :key="feature.key" :feature="feature" />
      </ul>
    </div>
  </section>
</template>

<script setup lang="ts">
/**
 * nova performance section.
 *
 * Storage medium, caching and a CDN are properties of the operator's machines,
 * not of this application — it provisions through cPanel/WHM or CWP and never
 * learns what is underneath — so those three wait on a setting. What remains is
 * true of every install, and the section hides itself if even that is empty.
 */
import NovaFeatureCard from '~/templates/nova/sections/FeatureCard.vue'
import { PERFORMANCE_FEATURES } from '~/templates/nova/content'
import { useVisibleFeatures } from '~/templates/nova/features'

const { t } = useI18n()
const features = useVisibleFeatures(PERFORMANCE_FEATURES)
</script>
