<template>
  <UiStack gap="none" class="relative overflow-hidden font-aurora text-tx" data-testid="aurora-home">
    <UiBackdrop variant="aurora" />

    <UiContainer class="relative">
      <UiSection id="domains" padding="lg" :contained="false" class="relative md:pb-14">
        <UiSplit gap="xl" class="items-center">
          <template #start>
            <AuroraHero />
          </template>
          <template #end>
            <!-- A brand glow behind the search card ties it to the headline and gives the
                 right column the same weight as the left. -->
            <UiStack gap="none" class="relative">
              <UiDecor class="-inset-8 rounded-[40px] bg-glow1 opacity-60 blur-3xl" data-testid="hero-search-glow" />
              <AuroraDomainSearch
                :results="domainResults"
                :pending="domainPending"
                :has-zones="hasZones"
                :price-hints="priceHints"
                @search="term => emit('search', term)"
              />
            </UiStack>
          </template>
        </UiSplit>
      </UiSection>

      <AuroraTrustBar v-if="isVisible(HomeSection.Stats)" />
      <AuroraPartners v-if="isPartnersVisible(isVisible)" />

      <AuroraPlanCards
        v-if="isVisible(HomeSection.Plans)"
        :plans="plans"
        :cycle="cycle"
        @update:cycle="value => emit('update:cycle', value)"
      />

      <AuroraServices v-if="isVisible(HomeSection.Services)" />
      <AuroraPerformance v-if="isVisible(HomeSection.Performance)" />
      <AuroraSecurity v-if="isVisible(HomeSection.Security)" />
      <AuroraApps v-if="isVisible(HomeSection.Apps)" />
      <AuroraLocations v-if="isVisible(HomeSection.Locations)" />
      <AuroraGuarantees v-if="isVisible(HomeSection.Guarantees)" />
      <AuroraProcess v-if="showProcess && isVisible(HomeSection.Process)" />
      <AuroraProjects v-if="isVisible(HomeSection.Projects)" />
      <AuroraTestimonials v-if="isVisible(HomeSection.Testimonials)" />
      <AuroraSupport v-if="isVisible(HomeSection.Support)" />
      <AuroraFaq v-if="showFaq && isVisible(HomeSection.Faq)" />
      <AuroraCta v-if="isVisible(HomeSection.Cta)" />
    </UiContainer>
  </UiStack>
</template>

<script setup lang="ts">
/**
 * aurora template homepage.
 * Presentation only: every value it displays arrives as a prop from pages/index.vue.
 */
import AuroraHero from '~/templates/aurora/sections/AuroraHero.vue'
import AuroraDomainSearch from '~/templates/aurora/sections/DomainSearch.vue'
import AuroraTrustBar from '~/templates/aurora/sections/AuroraTrustBar.vue'
import AuroraPlanCards from '~/templates/aurora/sections/PlanCards.vue'
import AuroraServices from '~/templates/aurora/sections/AuroraServices.vue'
import AuroraProcess from '~/templates/aurora/sections/AuroraProcess.vue'
import AuroraPartners from '~/templates/aurora/sections/AuroraPartners.vue'
import AuroraProjects from '~/templates/aurora/sections/AuroraProjects.vue'
import AuroraTestimonials from '~/templates/aurora/sections/AuroraTestimonials.vue'
import AuroraFaq from '~/templates/aurora/sections/AuroraFaq.vue'
import AuroraCta from '~/templates/aurora/sections/AuroraCta.vue'
import AuroraLocations from '~/templates/aurora/sections/AuroraLocations.vue'
import AuroraPerformance from '~/templates/aurora/sections/AuroraPerformance.vue'
import AuroraSecurity from '~/templates/aurora/sections/AuroraSecurity.vue'
import AuroraApps from '~/templates/aurora/sections/AuroraApps.vue'
import AuroraGuarantees from '~/templates/aurora/sections/AuroraGuarantees.vue'
import AuroraSupport from '~/templates/aurora/sections/AuroraSupport.vue'
import { HomeSection } from '~/constants/homeSection'
import { isPartnersVisible } from '~/utils/partnersVisible'
import { useHomeSections } from '~/composables/useHomeSections'
import type { DomainResult } from '~/types/domainresult'
import type { PlanCard } from '~/types/plancard'
import { PricingCycle } from '~/types/pricingcycle'
import type { TldPriceRow } from '~/types/tldpricerow'

withDefaults(defineProps<{
  /** Lookup results of the search card. */
  domainResults?: DomainResult[]
  /** Whether a lookup is under way. */
  domainPending?: boolean
  /** False when the operator has priced no extensions. */
  hasZones?: boolean
  /** Extensions the search offers, shown as a price hint under the field. */
  priceHints?: TldPriceRow[]
  /** Hosting plans for the plan cards. */
  plans?: PlanCard[]
  /** The billing cycle the prices are shown for. */
  cycle?: PricingCycle
  /** Whether to show the process section; on by default because it carries indexable content. */
  showProcess?: boolean
  /** Whether to show the FAQ section; on by default because it carries indexable content. */
  showFaq?: boolean
}>(), {
  domainResults: () => [],
  domainPending: false,
  hasZones: true,
  priceHints: () => [],
  plans: () => [],
  cycle: PricingCycle.Monthly,
  showProcess: true,
  showFaq: true,
})

/** Whether the operator switched a home section on. */
const { isVisible } = useHomeSections()

/** Events this component raises. */
const emit = defineEmits<{
  /** The visitor submitted a domain search term. */
  search: [term: string]
  /** The visitor switched the billing cycle. */
  'update:cycle': [value: PricingCycle]
}>()
</script>
