<template>
  <section class="relative overflow-hidden border-b border-nova-border">
    <!-- Decorative wash. aria-hidden because it carries no information. -->
    <div
      class="pointer-events-none absolute -top-40 left-1/2 h-[560px] w-[900px] -translate-x-1/2 rounded-full bg-[radial-gradient(closest-side,var(--n-brand),transparent)] opacity-20"
      aria-hidden="true"
    />

    <div class="relative mx-auto grid max-w-[1240px] items-center gap-12 px-4 py-16 sm:px-6 lg:grid-cols-2 lg:px-8 lg:py-24">
      <div class="n-reveal min-w-0">
        <p class="inline-flex items-center gap-2 rounded-full border border-nova-border bg-nova-surface px-3.5 py-1.5 text-[13px] text-nova-muted">
          <span class="h-1.5 w-1.5 rounded-full bg-nova-success" aria-hidden="true" />
          {{ t('nova.hero.badge') }}
        </p>

        <h1 class="mt-5 text-[clamp(2rem,5.5vw,3.5rem)] font-extrabold leading-[1.08] tracking-tight text-nova-ink">
          {{ t('nova.hero.title') }}
        </h1>

        <p class="mt-5 max-w-[560px] text-[17px] leading-[1.7] text-nova-muted">
          {{ t('nova.hero.lead') }}
        </p>

        <div class="mt-8 flex flex-wrap gap-3">
          <NuxtLink
            :to="localePath('/hosting')"
            class="inline-flex min-h-[52px] items-center rounded-xl bg-nova-accent px-7 text-base font-bold text-[#12212a] transition-[filter,transform] hover:brightness-95 motion-safe:hover:-translate-y-0.5"
          >{{ t('nova.hero.ctaPrimary') }}</NuxtLink>
          <NuxtLink
            :to="localePath('/hosting#plans')"
            class="inline-flex min-h-[52px] items-center rounded-xl border border-nova-border px-7 text-base font-semibold text-nova-ink transition-colors hover:border-nova-brand hover:text-nova-brand"
          >{{ t('nova.hero.ctaSecondary') }}</NuxtLink>
        </div>

        <ul class="mt-9 grid gap-x-6 gap-y-3 sm:grid-cols-2">
          <li v-for="benefit in benefits" :key="benefit.key" class="flex items-center gap-2.5 text-[15px] text-nova-ink">
            <Icon :name="benefit.icon" class="h-[18px] w-[18px] shrink-0 text-nova-success" aria-hidden="true" />
            {{ t(benefit.titleKey) }}
          </li>
        </ul>
      </div>

      <NovaDashboard compact />
    </div>
  </section>
</template>

<script setup lang="ts">
/**
 * nova hero — the page's first conversion point, and the owner of its only h1.
 *
 * The benefit list is filtered through `useVisibleFeatures`, so a claim this
 * install cannot back never reaches the fold. Nothing here is a number: uptime
 * and customer counts are measurements, and the one place a figure can appear
 * is the trust bar, where it is read from an operator setting.
 */
import NovaDashboard from '~/templates/nova/sections/Dashboard.vue'
import { HERO_BENEFITS } from '~/templates/nova/content'
import { useVisibleFeatures } from '~/templates/nova/features'

const { t } = useI18n()
const localePath = useLocalePath()
const benefits = useVisibleFeatures(HERO_BENEFITS)
</script>
