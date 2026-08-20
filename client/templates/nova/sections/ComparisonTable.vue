<template>
  <section v-if="rows.length > 0" class="border-b border-nova-border py-16 lg:py-24">
    <div class="mx-auto max-w-[1240px] px-4 sm:px-6 lg:px-8">
      <p class="text-[13px] font-semibold uppercase tracking-[0.1em] text-nova-brand">
        {{ t('nova.comparison.eyebrow') }}
      </p>
      <h2 class="mt-3 text-[clamp(1.6rem,3.4vw,2.4rem)] font-bold tracking-tight text-nova-ink">
        {{ t('nova.comparison.title') }}
      </h2>
      <p class="mt-3 text-[16px] text-nova-muted">{{ t('nova.comparison.lead') }}</p>

      <!--
        tabindex makes the scroller focusable, which is what lets a keyboard user
        scroll a table wider than the screen; the label tells them what it is
        before they do. On a narrow screen the first column stays pinned, so a
        value is never orphaned from the feature it belongs to.
      -->
      <div
        class="mt-8 overflow-x-auto rounded-2xl border border-nova-border bg-nova-surface"
        tabindex="0"
        role="region"
        :aria-label="t('nova.comparison.title')"
      >
        <table class="w-full min-w-[680px] border-collapse text-left">
          <caption class="sr-only">{{ t('nova.comparison.scrollHint') }}</caption>
          <thead>
            <tr class="border-b border-nova-border">
              <th
                scope="col"
                class="sticky left-0 z-10 bg-nova-surface px-5 py-4 text-[13px] font-semibold uppercase tracking-[0.06em] text-nova-muted"
              >{{ t('nova.comparison.feature') }}</th>
              <th
                v-for="plan in plans"
                :key="plan.id"
                scope="col"
                class="px-5 py-4 text-[15px] font-bold text-nova-ink"
              >{{ plan.name }}</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="row in orderedRows" :key="row.label" class="border-b border-nova-border last:border-0">
              <th
                scope="row"
                class="sticky left-0 z-10 bg-nova-surface px-5 py-3.5 text-[15px] font-medium text-nova-muted"
              >{{ row.label }}</th>
              <td
                v-for="(value, index) in row.values"
                :key="`${row.label}-${plans[index]?.id ?? index}`"
                class="px-5 py-3.5 text-[15px] text-nova-ink"
              >
                <!-- A plan without this line reads as absent, never as zero. -->
                <template v-if="value">{{ value }}</template>
                <template v-else>
                  <span aria-hidden="true">—</span>
                  <span class="sr-only">{{ t('nova.comparison.absent') }}</span>
                </template>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
/**
 * nova plan comparison table.
 *
 * Every row is a specification line an operator entered under Admin → Products →
 * Specification; nothing is added here. `orderComparisonRows` only rearranges
 * what arrived, into the order these lines usually read best in, and an install
 * that has specified nothing renders no section at all rather than a table of
 * dashes.
 */
import { orderComparisonRows } from '~/templates/nova/content'
import type { ComparisonRow } from '~/templates/nova/types'
import type { PlanCard } from '~/templates/aurora/types'

const props = withDefaults(defineProps<{
  plans?: PlanCard[]
  /** Rows in display order, each already aligned to the plan columns. */
  rows?: ComparisonRow[]
}>(), { plans: () => [], rows: () => [] })

const { t } = useI18n()

const orderedRows = computed(() => orderComparisonRows(props.rows))
</script>
