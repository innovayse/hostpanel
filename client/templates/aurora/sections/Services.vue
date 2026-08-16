<template>
  <section id="services" class="relative border-t border-line px-[clamp(20px,5vw,48px)] py-[clamp(56px,8vw,84px)]">
    <div class="text-[13px] uppercase tracking-[0.12em] text-ac2">{{ t('aurora.services.eyebrow') }}</div>
    <h2 class="mt-3 max-w-[680px] font-display text-[clamp(27px,4vw,40px)] font-bold -tracking-[0.02em] text-tx">
      {{ t('aurora.services.title') }}
    </h2>

    <div class="mt-10 grid gap-5 [grid-template-columns:repeat(auto-fit,minmax(min(100%,210px),1fr))]">
      <div
        v-for="service in services"
        :key="service.tag"
        class="flex flex-col gap-3 rounded-[18px] border border-line bg-surf p-[26px] hover:border-ac1"
      >
        <div
          class="grid h-10 w-10 place-items-center rounded-[11px] font-mono text-[15px] font-medium text-[#08090F]"
          :class="service.tint"
        >{{ service.tag }}</div>
        <div class="text-[18px] font-bold text-tx">{{ service.title }}</div>
        <div class="text-[15px] leading-relaxed text-mut">{{ service.body }}</div>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
/** aurora services grid — four editorial cards, no data dependency. */
const { t } = useI18n()

const services = computed(() => [
  { tag: 'Web', title: t('aurora.services.webTitle'), body: t('aurora.services.webBody'), tint: 'bg-gradient-to-br from-[#7DE3FF] to-[#5D3FFF]' },
  { tag: 'App', title: t('aurora.services.appTitle'), body: t('aurora.services.appBody'), tint: 'bg-gradient-to-br from-[#B9A6FF] to-[#5D3FFF]' },
  { tag: 'SEO', title: t('aurora.services.seoTitle'), body: t('aurora.services.seoBody'), tint: 'bg-gradient-to-br from-[#38E8A0] to-[#00D1FF]' },
  { tag: 'PPC', title: t('aurora.services.ppcTitle'), body: t('aurora.services.ppcBody'), tint: 'bg-gradient-to-br from-[#FF9CE0] to-[#B9A6FF]' },
])
</script>
