<template>
  <div class="relative mx-auto max-w-[1440px] overflow-hidden font-aurora text-tx">
    <div
      class="pointer-events-none absolute -top-[280px] left-1/2 h-[620px] w-[1000px] -translate-x-1/2 bg-glow1"
    />

    <section class="relative px-[clamp(20px,5vw,48px)] pb-9 pt-[clamp(48px,7vw,80px)] text-center">
      <div class="text-[13px] uppercase tracking-[0.12em] text-ac1">{{ t('aurora.hostingPage.eyebrow') }}</div>
      <h1 class="mt-3.5 font-display text-[clamp(30px,5vw,52px)] font-bold -tracking-[0.02em] text-tx">
        {{ t('aurora.hostingPage.title') }}
      </h1>
      <p class="mx-auto mt-4 max-w-[580px] text-[17px] leading-relaxed text-mut">
        {{ t('aurora.hostingPage.lead') }}
      </p>
    </section>

    <AuroraPlanCards
      :plans="plans"
      :yearly="yearly"
      :show-header="false"
      @update:yearly="value => emit('update:yearly', value)"
    />

    <AuroraCta />
  </div>
</template>

<script setup lang="ts">
/**
 * aurora hosting page.
 *
 * The design's plan comparison matrix is deliberately absent: ProductDto carries
 * no per-plan feature data, and the spec records why inventing it — or copying
 * the classic page's name-matching workaround — was rejected.
 */
import AuroraPlanCards from '~/templates/aurora/sections/PlanCards.vue'
import AuroraCta from '~/templates/aurora/sections/Cta.vue'
import type { PlanCard } from '~/templates/aurora/types'

withDefaults(defineProps<{
  plans?: PlanCard[]
  yearly?: boolean
}>(), { plans: () => [], yearly: false })

const emit = defineEmits<{ 'update:yearly': [value: boolean] }>()

const { t } = useI18n()
</script>
