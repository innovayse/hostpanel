<template>
  <section
    class="relative mx-[clamp(20px,5vw,48px)] mb-[clamp(56px,8vw,84px)] rounded-[26px] border border-line2 bg-card-hi px-[clamp(22px,4vw,64px)] py-[clamp(36px,5vw,64px)] text-center"
  >
    <h2 class="font-display text-[clamp(28px,4.4vw,42px)] font-bold -tracking-[0.02em] text-tx">
      {{ t('aurora.cta.title') }}
    </h2>
    <p class="mx-auto mt-4 max-w-[560px] text-[18px] leading-relaxed text-tx2">
      {{ t('aurora.cta.lead') }}
    </p>
    <div class="mt-8 flex flex-wrap justify-center gap-3.5">
      <NuxtLink
        :to="localePath('/contact')"
        class="rounded-xl bg-brand px-7 py-[15px] text-base font-bold text-[#08090F] hover:brightness-110"
      >{{ t('aurora.cta.primary') }}</NuxtLink>
      <NuxtLink
        :to="localePath('/hosting')"
        class="rounded-xl border border-line2 px-7 py-[15px] text-base font-semibold text-tx hover:border-ac1 hover:text-ac1"
      >{{ t('aurora.cta.secondary') }}</NuxtLink>
    </div>
  </section>
</template>

<script setup lang="ts">
/** aurora closing call to action. */
const { t } = useI18n()
const localePath = useLocalePath()
</script>
