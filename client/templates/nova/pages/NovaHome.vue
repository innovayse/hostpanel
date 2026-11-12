<template>
  <UiStack id="nova-main" as="div" gap="none" class="tpl-nova bg-nova-bg font-nova text-nova-ink" data-testid="nova-home">
    <NovaHero />
    <NovaTrustBar v-if="isVisible(HomeSection.Stats)" />
    <NovaPartners v-if="isPartnersVisible(isVisible)" />
    <NovaPricingSection
      v-if="isVisible(HomeSection.Plans)"
      :plans="plans"
      :cycle="cycle"
      @update:cycle="value => emit('update:cycle', value)"
    />
    <NovaComparisonTable v-if="isVisible(HomeSection.Comparison)" :plans="plans" :rows="comparisonRows" />
    <NovaWhyChooseUs v-if="isVisible(HomeSection.Services)" />
    <NovaLocations v-if="isVisible(HomeSection.Locations)" />
    <NovaPerformance v-if="isVisible(HomeSection.Performance)" />
    <NovaSecurity v-if="isVisible(HomeSection.Security)" />
    <NovaApps v-if="isVisible(HomeSection.Apps)" />
    <NovaDashboard v-if="isVisible(HomeSection.Dashboard)" />
    <NovaMigration v-if="isVisible(HomeSection.Migration)" />
    <NovaGuarantees v-if="isVisible(HomeSection.Guarantees)" />
    <NovaUseCases v-if="isVisible(HomeSection.UseCases)" />
    <NovaProjects v-if="isVisible(HomeSection.Projects)" />
    <NovaTestimonials v-if="isVisible(HomeSection.Testimonials)" />
    <NovaSupport v-if="isVisible(HomeSection.Support)" />
    <NovaFaq v-if="isVisible(HomeSection.Faq)" />
    <NovaFinalCta v-if="isVisible(HomeSection.Cta)" />
  </UiStack>
</template>

<script setup lang="ts">
/**
 * nova template homepage.
 *
 * Presentation only: every value it displays arrives as a prop from
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
import NovaHero from '~/templates/nova/sections/NovaHero.vue'
import NovaTrustBar from '~/templates/nova/sections/NovaTrustBar.vue'
import NovaPricingSection from '~/templates/nova/sections/PricingSection.vue'
import NovaComparisonTable from '~/templates/nova/sections/NovaComparisonTable.vue'
import NovaWhyChooseUs from '~/templates/nova/sections/WhyChooseUs.vue'
import NovaPerformance from '~/templates/nova/sections/NovaPerformance.vue'
import NovaSecurity from '~/templates/nova/sections/NovaSecurity.vue'
import NovaDashboard from '~/templates/nova/sections/NovaDashboard.vue'
import NovaMigration from '~/templates/nova/sections/NovaMigration.vue'
import NovaUseCases from '~/templates/nova/sections/UseCases.vue'
import NovaPartners from '~/templates/nova/sections/NovaPartners.vue'
import NovaProjects from '~/templates/nova/sections/NovaProjects.vue'
import NovaTestimonials from '~/templates/nova/sections/NovaTestimonials.vue'
import NovaFaq from '~/templates/nova/sections/NovaFaq.vue'
import NovaFinalCta from '~/templates/nova/sections/FinalCta.vue'
import NovaLocations from '~/templates/nova/sections/NovaLocations.vue'
import NovaApps from '~/templates/nova/sections/NovaApps.vue'
import NovaGuarantees from '~/templates/nova/sections/NovaGuarantees.vue'
import NovaSupport from '~/templates/nova/sections/NovaSupport.vue'
import { HomeSection } from '~/constants/homeSection'
import { isPartnersVisible } from '~/utils/partnersVisible'
import type { DomainResult } from '~/types/domainresult'
import type { PlanCard } from '~/types/plancard'
import { PricingCycle } from '~/types/pricingcycle'
import type { ComparisonRow } from '~/types/comparisonrow'
import type { TldPriceRow } from '~/types/tldpricerow'

/** Props of NovaHome. */
withDefaults(defineProps<{
  /** Plans as the catalogue store formats them. */
  plans?: PlanCard[]
  /** The billing cycle the prices are shown for. */
  cycle?: PricingCycle
  /** Specification lines, already aligned to the plan columns. */
  comparisonRows?: ComparisonRow[]
  /** Domain-search results; declared but unused, see the file header. */
  domainResults?: DomainResult[]
  /** Domain search in flight; declared but unused. */
  domainPending?: boolean
  /** Whether any zone is sold; declared but unused. */
  hasZones?: boolean
  /** TLD price hints; declared but unused. */
  priceHints?: TldPriceRow[]
}>(), {
  plans: () => [],
  cycle: PricingCycle.Monthly,
  comparisonRows: () => [],
  domainResults: () => [],
  domainPending: false,
  hasZones: true,
  priceHints: () => [],
})

/** Whether the operator has switched a home section on (Admin > Settings). */
const { isVisible } = useHomeSections()

/** Emits the page raises. */
const emit = defineEmits<{
  /** A domain search was requested; declared so the shared page can bind it. */
  search: [term: string]
  /** The reader chose another billing cycle. */
  'update:cycle': [value: PricingCycle]
}>()
</script>
