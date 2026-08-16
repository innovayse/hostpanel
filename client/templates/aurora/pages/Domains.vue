<template>
  <div class="relative mx-auto max-w-[1440px] overflow-hidden font-aurora text-tx">
    <div
      class="pointer-events-none absolute -top-[280px] left-1/2 h-[620px] w-[1000px] -translate-x-1/2 bg-glow1"
    />

    <section class="relative px-[clamp(20px,5vw,48px)] pb-9 pt-[clamp(48px,7vw,80px)] text-center">
      <div class="text-[13px] uppercase tracking-[0.12em] text-ac1">{{ t('aurora.domainsPage.eyebrow') }}</div>
      <h1 class="mt-3.5 font-display text-[clamp(30px,5vw,52px)] font-bold -tracking-[0.02em] text-tx">
        {{ t('aurora.domainsPage.title') }}
      </h1>
      <p class="mx-auto mt-4 max-w-[560px] text-[17px] leading-relaxed text-mut">
        {{ t('aurora.domainsPage.lead') }}
      </p>

      <div class="mx-auto mt-8 max-w-[720px]">
        <AuroraDomainSearch
          :results="results"
          :pending="pending"
          :has-zones="priceRows.length > 0"
          @search="term => emit('search', term)"
        />
      </div>
    </section>

    <AuroraTldTable :rows="priceRows" />
  </div>
</template>

<script setup lang="ts">
/**
 * aurora domains page.
 * Presentation only — pricing and lookup results arrive from pages/domains/index.vue.
 */
import AuroraDomainSearch from '~/templates/aurora/sections/DomainSearch.vue'
import AuroraTldTable from '~/templates/aurora/sections/TldTable.vue'
import type { DomainResult } from '~/templates/aurora/types'
import type { TldPriceRow } from '~/composables/useDomainLookup'

withDefaults(defineProps<{
  priceRows?: TldPriceRow[]
  results?: DomainResult[]
  pending?: boolean
}>(), { priceRows: () => [], results: () => [], pending: false })

const emit = defineEmits<{ search: [term: string] }>()

const { t } = useI18n()
</script>
