<template>
  <section id="process" class="relative border-t border-line px-[clamp(20px,5vw,48px)] py-[clamp(56px,8vw,84px)]">
    <div class="grid gap-[clamp(32px,4vw,56px)] [grid-template-columns:repeat(auto-fit,minmax(min(100%,300px),1fr))]">
      <div>
        <div class="text-[13px] uppercase tracking-[0.12em] text-ac1">{{ t('aurora.process.eyebrow') }}</div>
        <h2 class="mt-3 font-display text-[clamp(27px,4vw,40px)] font-bold -tracking-[0.02em] text-tx">
          {{ t('aurora.process.title') }}
        </h2>
        <p class="mt-[18px] text-[17px] leading-relaxed text-mut">{{ t('aurora.process.lead') }}</p>
      </div>

      <div class="flex flex-col gap-3.5">
        <div
          v-for="step in steps"
          :key="step.n"
          class="grid items-start gap-4 rounded-2xl border border-line bg-surf p-[22px] [grid-template-columns:46px_1fr]"
        >
          <div class="bg-hero-grad bg-clip-text font-mono text-[26px] font-medium text-transparent">{{ step.n }}</div>
          <div>
            <div class="text-[17px] font-bold text-tx">{{ step.title }}</div>
            <div class="mt-1.5 text-[15px] leading-relaxed text-mut">{{ step.body }}</div>
          </div>
        </div>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
/** aurora process section — four numbered steps. */
const { t } = useI18n()

const steps = computed(() => [
  { n: '01', title: t('aurora.process.step1Title'), body: t('aurora.process.step1Body') },
  { n: '02', title: t('aurora.process.step2Title'), body: t('aurora.process.step2Body') },
  { n: '03', title: t('aurora.process.step3Title'), body: t('aurora.process.step3Body') },
  { n: '04', title: t('aurora.process.step4Title'), body: t('aurora.process.step4Body') },
])
</script>
