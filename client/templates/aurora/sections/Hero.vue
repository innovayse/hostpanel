<template>
  <div class="min-w-0">
    <div
      class="inline-flex items-center gap-2.5 rounded-full border border-line2 bg-acbg px-3.5 py-[7px] text-[13px] text-ac1"
    >
      <span class="h-[7px] w-[7px] rounded-full bg-ok" />
      {{ t('aurora.hero.badge') }}
    </div>

    <h1
      class="mt-[22px] font-display text-[clamp(26px,6vw,60px)] font-bold leading-[1.08] -tracking-[0.02em] text-tx"
    >
      {{ t('aurora.hero.titleLine1') }}<br>
      {{ t('aurora.hero.titleLine2') }}<br>
      <span class="bg-hero-grad bg-clip-text text-transparent">{{ t('aurora.hero.titleAccent') }}</span>
    </h1>

    <p class="mt-6 max-w-[520px] text-[18px] leading-[1.65] text-mut">
      {{ t('aurora.hero.lead') }}
    </p>

    <div class="mt-[34px] flex flex-wrap gap-3.5">
      <NuxtLink
        :to="localePath('/domains')"
        class="rounded-xl bg-brand px-[26px] py-[15px] text-base font-semibold text-[#08090F] hover:brightness-110"
      >{{ t('aurora.hero.ctaPrimary') }}</NuxtLink>
      <NuxtLink
        :to="localePath('/contact')"
        class="rounded-xl border border-line2 px-[26px] py-[15px] text-base font-semibold text-tx hover:border-ac1 hover:text-ac1"
      >{{ t('aurora.hero.ctaSecondary') }}</NuxtLink>
    </div>

    <div class="mt-11 flex flex-wrap gap-x-10 gap-y-7">
      <div v-for="stat in stats" :key="stat.label">
        <div class="text-[26px] font-bold text-tx">{{ stat.value }}</div>
        <div class="mt-1 text-sm text-mut2">{{ stat.label }}</div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
/**
 * aurora hero — the homepage's headline, calls to action and headline figures.
 * Carries the page's only visible h1.
 */
const { t } = useI18n()
const localePath = useLocalePath()

const stats = computed(() => [
  { value: '2 400+', label: t('aurora.hero.statDomains') },
  { value: '180+', label: t('aurora.hero.statProjects') },
  { value: '24/7', label: t('aurora.hero.statSupport') },
])
</script>
