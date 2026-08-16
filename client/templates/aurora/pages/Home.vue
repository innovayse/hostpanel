<template>
  <div class="relative mx-auto max-w-[1440px] overflow-hidden font-aurora text-tx">
    <div
      class="pointer-events-none absolute -top-[260px] left-1/2 h-[700px] w-[1100px] -translate-x-1/2 bg-glow1"
    />
    <div
      class="pointer-events-none absolute -right-[180px] top-[120px] h-[620px] w-[620px] bg-glow2"
    />

    <section
      id="domains"
      class="relative grid items-center gap-[clamp(36px,5vw,56px)] px-[clamp(20px,5vw,48px)] pb-[clamp(48px,6vw,72px)] pt-[clamp(52px,8vw,92px)] [grid-template-columns:repeat(auto-fit,minmax(min(100%,420px),1fr))]"
    >
      <AuroraHero />
      <AuroraDomainSearch
        :results="domainResults"
        :pending="domainPending"
        :has-zones="hasZones"
        @search="term => emit('search', term)"
      />
    </section>

    <AuroraTrustBar />

    <AuroraPlanCards
      :plans="plans"
      :yearly="yearly"
      @update:yearly="value => emit('update:yearly', value)"
    />

    <AuroraServices />
    <AuroraProcess v-if="showProcess" />
    <AuroraTestimonials />
    <AuroraFaq v-if="showFaq" />
    <AuroraCta />
  </div>
</template>

<script setup lang="ts">
/**
 * aurora template homepage.
 * Presentation only — every value it displays arrives as a prop from pages/index.vue.
 */
import AuroraHero from '~/templates/aurora/sections/Hero.vue'
import AuroraDomainSearch from '~/templates/aurora/sections/DomainSearch.vue'
import AuroraTrustBar from '~/templates/aurora/sections/TrustBar.vue'
import AuroraPlanCards from '~/templates/aurora/sections/PlanCards.vue'
import AuroraServices from '~/templates/aurora/sections/Services.vue'
import AuroraProcess from '~/templates/aurora/sections/Process.vue'
import AuroraTestimonials from '~/templates/aurora/sections/Testimonials.vue'
import AuroraFaq from '~/templates/aurora/sections/Faq.vue'
import AuroraCta from '~/templates/aurora/sections/Cta.vue'
import type { DomainResult, PlanCard } from '~/templates/aurora/types'

withDefaults(defineProps<{
  domainResults?: DomainResult[]
  domainPending?: boolean
  hasZones?: boolean
  plans?: PlanCard[]
  yearly?: boolean
  /** Both default to on: these sections carry indexable content. */
  showProcess?: boolean
  showFaq?: boolean
}>(), {
  domainResults: () => [],
  domainPending: false,
  hasZones: true,
  plans: () => [],
  yearly: false,
  showProcess: true,
  showFaq: true,
})

const emit = defineEmits<{
  search: [term: string]
  'update:yearly': [value: boolean]
}>()
</script>
