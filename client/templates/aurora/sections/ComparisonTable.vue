<template>
  <section v-if="rows.length > 0" class="relative px-[clamp(20px,5vw,48px)] pb-[clamp(56px,8vw,84px)]">
    <h2 class="mb-2 font-display text-[clamp(25px,3.6vw,36px)] font-bold -tracking-[0.02em] text-tx">
      {{ t('aurora.comparison.title') }}
    </h2>
    <p class="mb-7 text-base text-mut">{{ t('aurora.comparison.lead') }}</p>

    <div class="overflow-x-auto rounded-[18px] border border-line bg-surf">
      <table class="w-full min-w-[640px] border-collapse">
        <thead>
          <tr class="border-b border-line text-left text-[13px] uppercase tracking-[0.06em] text-mut2">
            <th class="px-[22px] py-4 font-normal">{{ t('aurora.comparison.feature') }}</th>
            <th v-for="plan in plans" :key="plan.id" class="px-[22px] py-4 font-normal">
              {{ plan.name }}
            </th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="row in rows" :key="row.label" class="border-b border-line last:border-0">
            <th scope="row" class="px-[22px] py-[15px] text-left text-[15px] font-normal text-mut">
              {{ row.label }}
            </th>
            <td
              v-for="(value, index) in row.values"
              :key="`${row.label}-${plans[index]?.id ?? index}`"
              class="px-[22px] py-[15px] text-[15px]"
              :class="value ? 'text-tx' : 'text-mut2'"
            >
              <!-- A plan that does not carry this line reads as absent, not as zero. -->
              {{ value || '—' }}
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </section>
</template>

<script setup lang="ts">
/**
 * aurora plan comparison table.
 *
 * Presentation only. The rows arrive already aligned to the plan columns from
 * pages/hosting/index.vue, and the whole section disappears when no plan has a
 * specification, so an operator who has entered none sees the page as it was.
 */
import type { PlanCard } from '~/templates/aurora/types'
import type { ComparisonRow } from '~/composables/useProductFeatures'

withDefaults(defineProps<{
  plans?: PlanCard[]
  /** Rows in display order, each with one value per plan. */
  rows?: ComparisonRow[]
}>(), { plans: () => [], rows: () => [] })

const { t } = useI18n()
</script>
