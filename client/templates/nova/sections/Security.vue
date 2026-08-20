<template>
  <section v-if="features.length" class="border-b border-nova-border py-16 lg:py-24">
    <div class="mx-auto max-w-[1240px] px-4 sm:px-6 lg:px-8">
      <div class="max-w-[620px]">
        <p class="text-[13px] font-semibold uppercase tracking-[0.1em] text-nova-brand">
          {{ t('nova.security.eyebrow') }}
        </p>
        <h2 class="mt-3 text-[clamp(1.6rem,3.4vw,2.4rem)] font-bold tracking-tight text-nova-ink">
          {{ t('nova.security.title') }}
        </h2>
        <p class="mt-3 text-[16px] leading-[1.7] text-nova-muted">{{ t('nova.security.lead') }}</p>
      </div>

      <ul class="mt-10 grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
        <NovaFeatureCard v-for="feature in features" :key="feature.key" :feature="feature" />
      </ul>
    </div>
  </section>
</template>

<script setup lang="ts">
/**
 * nova security section.
 *
 * SSL and two-factor sign-in are implemented in this codebase and stated
 * plainly. A firewall, a malware scanner and a backup schedule live on the
 * operator's servers, so each of those waits for its setting before it claims
 * anything.
 */
import NovaFeatureCard from '~/templates/nova/sections/FeatureCard.vue'
import { SECURITY_FEATURES } from '~/templates/nova/content'
import { useVisibleFeatures } from '~/templates/nova/features'

const { t } = useI18n()
const features = useVisibleFeatures(SECURITY_FEATURES)
</script>
