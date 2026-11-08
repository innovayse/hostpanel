<template>
  <UiStack id="nova-main" as="div" gap="none" class="tpl-nova bg-nova-bg font-nova text-nova-ink">
    <UiSection padding="lg" class="border-b border-nova-border">
      <UiHeading
        :level="1"
        size="bare"
        class="text-[clamp(1.9rem,4.5vw,3rem)] font-extrabold leading-tight tracking-tight text-nova-ink"
      >
        {{ t('nova.plans.title') }}
      </UiHeading>
      <UiText size="inherit" tone="inherit" class="mt-4 max-w-[75ch] text-[17px] leading-[1.7] text-nova-muted">
        {{ t('nova.plans.lead') }}
      </UiText>
    </UiSection>

    <NovaTrustBar />
    <NovaPricingSection
      :plans="plans"
      :cycle="cycle"
      :show-header="false"
      @update:cycle="value => emit('update:cycle', value)"
    />
    <NovaComparisonTable :plans="plans" :rows="comparisonRows" />
    <NovaSecurity />
    <NovaMigration />
    <NovaFaq />
    <NovaFinalCta />
  </UiStack>
</template>

<script setup lang="ts">
/**
 * nova hosting page.
 *
 * Its own h1 states what the page is, so the pricing section below it renders
 * without a heading: two headings saying the same thing is a hierarchy problem
 * rather than emphasis. Data arrives from pages/hosting/index.vue.
 */
import NovaTrustBar from '~/templates/nova/sections/NovaTrustBar.vue'
import NovaPricingSection from '~/templates/nova/sections/PricingSection.vue'
import NovaComparisonTable from '~/templates/nova/sections/NovaComparisonTable.vue'
import NovaSecurity from '~/templates/nova/sections/NovaSecurity.vue'
import NovaMigration from '~/templates/nova/sections/NovaMigration.vue'
import NovaFaq from '~/templates/nova/sections/NovaFaq.vue'
import NovaFinalCta from '~/templates/nova/sections/FinalCta.vue'
import type { PlanCard } from '~/types/plancard'
import { PricingCycle } from '~/types/pricingcycle'
import type { ComparisonRow } from '~/types/comparisonrow'

/** Props of NovaHosting. */
withDefaults(defineProps<{
  /** Plans as the catalogue store formats them. */
  plans?: PlanCard[]
  /** The billing cycle the prices are shown for. */
  cycle?: PricingCycle
  /** Specification lines, already aligned to the plan columns. */
  comparisonRows?: ComparisonRow[]
}>(), { plans: () => [], cycle: PricingCycle.Monthly, comparisonRows: () => [] })

/** Emits the page raises. */
const emit = defineEmits<{
  /** The reader chose another billing cycle. */
  'update:cycle': [value: PricingCycle]
}>()

/** Translator for the copy. */
const { t } = useI18n()
</script>
