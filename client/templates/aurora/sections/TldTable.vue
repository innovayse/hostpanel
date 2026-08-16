<template>
  <section class="relative px-[clamp(20px,5vw,48px)] pb-[clamp(56px,8vw,84px)]">
    <h2 class="mb-2 font-display text-[clamp(25px,3.6vw,36px)] font-bold -tracking-[0.02em] text-tx">
      {{ t('aurora.tldTable.title') }}
    </h2>
    <p class="mb-7 text-base text-mut">{{ t('aurora.tldTable.lead') }}</p>


    <div class="overflow-x-auto rounded-[18px] border border-line bg-surf">
      <table class="w-full min-w-[640px] border-collapse">
        <thead>
          <tr class="border-b border-line text-left text-[13px] uppercase tracking-[0.06em] text-mut2">
            <th class="px-[22px] py-4 font-normal">{{ t('aurora.tldTable.zone') }}</th>
            <th class="px-[22px] py-4 font-normal">{{ t('aurora.tldTable.register') }}</th>
            <th class="px-[22px] py-4 font-normal">{{ t('aurora.tldTable.renew') }}</th>
            <th class="px-[22px] py-4 font-normal">{{ t('aurora.tldTable.transfer') }}</th>
            <th class="px-[22px] py-4 font-normal">{{ t('aurora.tldTable.note') }}</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="row in visibleRows" :key="row.tld" class="border-b border-line last:border-0">
            <td class="px-[22px] py-[15px] font-mono text-base text-tx">{{ row.tld }}</td>
            <td class="px-[22px] py-[15px] text-[15px] font-semibold text-tx">{{ row.register }}</td>
            <td class="px-[22px] py-[15px] text-[15px] text-tx2">{{ row.renew }}</td>
            <td class="px-[22px] py-[15px] text-[15px] text-tx2">{{ row.transfer }}</td>
            <!-- The registrar's own category tag; the design's editorial note has no field behind it. -->
            <td class="px-[22px] py-[15px] text-[13px] text-ac1">{{ row.categories[0] ?? '' }}</td>
          </tr>
          <tr v-if="visibleRows.length === 0">
            <td colspan="5" class="px-[22px] py-8 text-center text-[15px] text-mut2">
              {{ t('aurora.tldTable.empty') }}
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </section>
</template>

<script setup lang="ts">
/**
 * aurora TLD price table.
 *
 * Rows and their category tags come from the API, so the table reflects whatever
 * the operator's registrar actually offers. The category filter itself lives in
 * the search bar above, because it narrows the results list as well as this
 * table; the selection arrives here as a prop.
 */
import { ALL_CATEGORY } from '~/templates/aurora/types'
import type { TldPriceRow } from '~/composables/useDomainLookup'

const props = withDefaults(defineProps<{
  rows?: TldPriceRow[]
  /** Selected category, or ALL_CATEGORY for the unfiltered view. */
  active?: string
}>(), { rows: () => [], active: ALL_CATEGORY })

const { t } = useI18n()

const visibleRows = computed(() =>
  props.active === ALL_CATEGORY
    ? props.rows
    : props.rows.filter(row => row.categories.includes(props.active)))
</script>
