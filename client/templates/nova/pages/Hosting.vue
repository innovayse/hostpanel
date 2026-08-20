<template>
  <div id="nova-main" class="tpl-nova bg-nova-bg font-nova text-nova-ink">
    <section class="border-b border-nova-border py-14 lg:py-20">
      <div class="mx-auto max-w-[1240px] px-4 sm:px-6 lg:px-8">
        <h1 class="max-w-[720px] text-[clamp(1.9rem,4.5vw,3rem)] font-extrabold leading-tight tracking-tight text-nova-ink">
          {{ t('nova.plans.title') }}
        </h1>
        <p class="mt-4 max-w-[620px] text-[17px] leading-[1.7] text-nova-muted">
          {{ t('nova.plans.lead') }}
        </p>
      </div>
    </section>

    <NovaTrustBar />
    <NovaPricingSection
      :plans="plans"
      :yearly="yearly"
      :show-header="false"
      @update:yearly="value => emit('update:yearly', value)"
    />
    <NovaComparisonTable :plans="plans" :rows="comparisonRows" />
    <NovaSecurity />
    <NovaMigration />
    <NovaFaq />
    <NovaFinalCta />
  </div>
</template>

<script setup lang="ts">
/**
 * nova hosting page.
 *
 * Its own h1 states what the page is, so the pricing section below it renders
 * without a heading — two headings saying the same thing is a hierarchy problem
 * rather than emphasis. Data arrives from pages/hosting/index.vue.
 */
import NovaTrustBar from '~/templates/nova/sections/TrustBar.vue'
import NovaPricingSection from '~/templates/nova/sections/PricingSection.vue'
import NovaComparisonTable from '~/templates/nova/sections/ComparisonTable.vue'
import NovaSecurity from '~/templates/nova/sections/Security.vue'
import NovaMigration from '~/templates/nova/sections/Migration.vue'
import NovaFaq from '~/templates/nova/sections/Faq.vue'
import NovaFinalCta from '~/templates/nova/sections/FinalCta.vue'
import type { PlanCard } from '~/templates/aurora/types'
import type { ComparisonRow } from '~/templates/nova/types'

withDefaults(defineProps<{
  plans?: PlanCard[]
  yearly?: boolean
  comparisonRows?: ComparisonRow[]
}>(), { plans: () => [], yearly: false, comparisonRows: () => [] })

const emit = defineEmits<{ 'update:yearly': [value: boolean] }>()

const { t } = useI18n()
</script>
