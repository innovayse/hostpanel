<template>
  <div id="nova-main" class="tpl-nova bg-nova-bg font-nova text-nova-ink">
    <NovaHero />
    <NovaTrustBar />
    <NovaPricingSection
      :plans="plans"
      :yearly="yearly"
      @update:yearly="value => emit('update:yearly', value)"
    />
    <NovaComparisonTable :plans="plans" :rows="comparisonRows" />
    <NovaWhyInnovayse />
    <NovaPerformance />
    <NovaSecurity />
    <NovaDashboard />
    <NovaMigration />
    <NovaUseCases />
    <NovaTestimonials />
    <NovaFaq />
    <NovaFinalCta />
  </div>
</template>

<script setup lang="ts">
/**
 * nova template homepage.
 *
 * Presentation only — every value it displays arrives as a prop from
 * pages/index.vue, which owns the fetching, the SEO and the structured data.
 *
 * `id="nova-main"` is what the header's skip link points at. It sits here
 * rather than on the shared layout's <main>, because that element belongs to
 * every template and nova must not reach outside itself.
 *
 * The domain-search props are declared but unused: the page hands the same set
 * to whichever template is active, and declaring them keeps Vue from falling
 * them through onto this root element as stray DOM attributes. nova puts its
 * domain search on /domains, where the aurora components it reuses already are.
 */
import NovaHero from '~/templates/nova/sections/Hero.vue'
import NovaTrustBar from '~/templates/nova/sections/TrustBar.vue'
import NovaPricingSection from '~/templates/nova/sections/PricingSection.vue'
import NovaComparisonTable from '~/templates/nova/sections/ComparisonTable.vue'
import NovaWhyInnovayse from '~/templates/nova/sections/WhyInnovayse.vue'
import NovaPerformance from '~/templates/nova/sections/Performance.vue'
import NovaSecurity from '~/templates/nova/sections/Security.vue'
import NovaDashboard from '~/templates/nova/sections/Dashboard.vue'
import NovaMigration from '~/templates/nova/sections/Migration.vue'
import NovaUseCases from '~/templates/nova/sections/UseCases.vue'
import NovaTestimonials from '~/templates/nova/sections/Testimonials.vue'
import NovaFaq from '~/templates/nova/sections/Faq.vue'
import NovaFinalCta from '~/templates/nova/sections/FinalCta.vue'
import type { DomainResult, PlanCard } from '~/templates/aurora/types'
import type { ComparisonRow } from '~/templates/nova/types'
import type { TldPriceRow } from '~/types/tldpricerow'

withDefaults(defineProps<{
  plans?: PlanCard[]
  yearly?: boolean
  /** Specification lines, already aligned to the plan columns. */
  comparisonRows?: ComparisonRow[]
  domainResults?: DomainResult[]
  domainPending?: boolean
  hasZones?: boolean
  priceHints?: TldPriceRow[]
}>(), {
  plans: () => [],
  yearly: false,
  comparisonRows: () => [],
  domainResults: () => [],
  domainPending: false,
  hasZones: true,
  priceHints: () => [],
})

const emit = defineEmits<{
  search: [term: string]
  'update:yearly': [value: boolean]
}>()
</script>
