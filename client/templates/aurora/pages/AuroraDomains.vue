<template>
  <UiStack gap="none" class="relative overflow-hidden font-aurora text-tx">
    <UiBackdrop variant="aurora" />

    <UiContainer class="relative">
      <UiStack as="section" align="center" gap="none" class="relative pb-9 pt-[clamp(48px,7vw,80px)] text-center">
        <UiText tone="inherit" size="inherit" :class="[EYEBROW_CLASS, 'text-ac1']">{{ t('aurora.domainsPage.eyebrow') }}</UiText>
        <UiHeading :level="1" size="bare" :class="[PAGE_TITLE_CLASS, 'mt-3.5']" data-testid="domains-title">
          {{ t('aurora.domainsPage.title') }}
        </UiHeading>
        <UiText size="inherit" tone="inherit" class="mx-auto mt-4 max-w-[75ch] text-[17px] leading-relaxed text-mut">
          {{ t('aurora.domainsPage.lead') }}
        </UiText>

        <UiStack gap="none" class="mt-[30px] w-full">
          <AuroraDomainSearchBar
            :categories="categories"
            :active="activeCategory"
            :has-zones="priceRows.length > 0"
            :initial-query="query"
            @search="term => emit('search', term)"
            @update:category="value => emit('update:category', value)"
          />
        </UiStack>
      </UiStack>

      <AuroraDomainResults
        :results="results"
        :pending="pending"
        :has-zones="priceRows.length > 0"
        :in-cart="inCart"
        @add="domain => emit('add', domain)"
      />

      <AuroraTldTable :rows="priceRows" :active="activeCategory" />
    </UiContainer>
  </UiStack>
</template>

<script setup lang="ts">
/**
 * aurora domains page.
 * Presentation only: pricing, lookup results and cart state arrive from
 * pages/domains/index.vue, and adding to the cart is emitted back to it.
 */
import AuroraDomainSearchBar from '~/templates/aurora/sections/DomainSearchBar.vue'
import AuroraDomainResults from '~/templates/aurora/sections/DomainResults.vue'
import AuroraTldTable from '~/templates/aurora/sections/TldTable.vue'
import { EYEBROW_CLASS, PAGE_TITLE_CLASS } from '~/templates/aurora/typography'
import { ALL_CATEGORY } from '~/templates/aurora/types/allcategory'
import type { DomainResult } from '~/types/domainresult'
import type { TldPriceRow } from '~/types/tldpricerow'

withDefaults(defineProps<{
  /** Priced extensions, for the price table and the "has zones" decision. */
  priceRows?: TldPriceRow[]
  /** Lookup results. */
  results?: DomainResult[]
  /** Whether a lookup is under way. */
  pending?: boolean
  /** Category names for the filter, including the "all" sentinel. */
  categories?: string[]
  /** The selected category. */
  activeCategory?: string
  /** Domains already in the cart. */
  inCart?: string[]
  /** The name the last search ran for; the search field shows it. */
  query?: string
}>(), {
  priceRows: () => [],
  results: () => [],
  pending: false,
  categories: () => [],
  activeCategory: ALL_CATEGORY,
  inCart: () => [],
  query: '',
})

/** Events this component raises. */
const emit = defineEmits<{
  /** The visitor submitted a domain search term. */
  search: [term: string]
  /** The visitor picked a category. */
  'update:category': [category: string]
  /** The visitor added an available domain to the cart. */
  add: [domain: string]
}>()

/** Translator for the active locale. */
const { t } = useI18n()
</script>
