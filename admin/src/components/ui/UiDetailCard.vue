<script setup lang="ts">
/**
 * One card of a detail screen, drawn from a `SkeletonDetailCard` of its schema.
 *
 * The card has its title and either rows of a caption over a value, or fields, lines, boxes, a table, a chart, a made-of-data head
 * and a row of buttons. The screen puts the real body in the default slot; while `loading` the title, the row captions
 * and the table header captions are real page text, the values are bars and the slot is not drawn, so the screen and its
 * skeleton are one card. A card with `tabs` is a bare tab strip with no card around it: real tabs in the slot, the schema's
 * captions (not clickable) while `loading`. The body is an `@container`, so a grid in the slot or in the skeleton that is
 * given `container` counts its columns by the width of the card, not of the screen. `UiDetailCards` lays the cards out.
 */
import { useI18n } from 'vue-i18n'
import UiCard from '@/components/ui/UiCard.vue'
import UiChartSkeleton from '@/components/ui/UiChartSkeleton.vue'
import UiField from '@/components/ui/UiField.vue'
import UiFormActions from '@/components/ui/UiFormActions.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiSkeletonBar from '@/components/ui/UiSkeletonBar.vue'
import UiSkeletonTable from '@/components/ui/UiSkeletonTable.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiTabs from '@/components/ui/UiTabs.vue'
import UiText from '@/components/ui/UiText.vue'
import { UiChartHeight } from '@/utils/ui/chart'
import type { SkeletonDetailCard, SkeletonTab } from '@/utils/ui/skeletonSchema'
import { tabItemsOfSchema } from '@/utils/ui/tabs'
import type { UiTabItem } from '@/utils/ui/tabs'

/** Resolves the locale keys the schema names into the operator's language. */
const { t } = useI18n()

/** Props for UiDetailCard. */
defineProps<{
  /** The card of the screen's schema. */
  card: SkeletonDetailCard
  /** Draws the skeleton of the card in place of the slot. */
  loading?: boolean
}>()

/**
 * The tab strip of a part, with the captions resolved.
 *
 * @param tabs - The tabs of the schema.
 * @returns Items for `UiTabs`.
 */
const tabItemsOf = (tabs: readonly SkeletonTab[]): UiTabItem[] => tabItemsOfSchema(tabs, t)

/**
 * The widths of the bars of a block of lines: full width, the last one shorter, as text wraps.
 *
 * @param count - How many lines.
 * @returns One width class per line.
 */
const lineWidthsOf = (count: number): string[] => Array.from({ length: count }, (_, index) => (index === count - 1 && count > 1 ? 'w-2/3' : 'w-full'))
</script>

<template>
  <div v-if="card.tabs" :data-testid="`ui-detail-card-${card.key}`">
    <UiTabs v-if="loading" :items="tabItemsOf(card.tabs)" model-value="" hide-scrollbar inert />
    <slot v-else />
  </div>
  <UiCard v-else padding="md" :aria-busy="loading ? 'true' : undefined" :data-testid="`ui-detail-card-${card.key}`">
    <UiStack gap="md" class="@container">
      <UiHeading v-if="card.title" :level="2" variant="card">
        {{ t(card.title) }}
      </UiHeading>
      <template v-if="loading">
        <UiStack v-if="card.identity" direction="row" align="center" gap="md" aria-hidden="true" data-testid="ui-detail-identity">
          <UiSkeletonBar class="h-10 w-10 shrink-0 rounded-control" />
          <UiStack gap="xs" class="min-w-0 flex-1">
            <UiSkeletonBar class="h-5 w-40 rounded-md" />
            <UiSkeletonBar class="h-3.5 w-64 max-w-full rounded-md" />
          </UiStack>
        </UiStack>
        <UiStack v-for="row in card.rows" :key="row" gap="xs" data-testid="ui-detail-row">
          <UiText tone="muted" size="sm">
            {{ t(row) }}
          </UiText>
          <UiSkeletonBar class="h-4 w-2/3 rounded-md" aria-hidden="true" />
        </UiStack>
        <UiGrid v-if="card.fields" :columns="2" gap="md" container>
          <UiField v-for="n in card.fields" :key="n" loading />
        </UiGrid>
        <UiStack v-if="card.lines" gap="xs" aria-hidden="true">
          <UiSkeletonBar v-for="(width, n) in lineWidthsOf(card.lines)" :key="n" class="h-3.5 rounded-md" :class="width" />
        </UiStack>
        <UiGrid v-if="card.tiles" :columns="3" gap="md" container data-testid="ui-detail-tiles">
          <UiCard v-for="n in card.tiles" :key="n" padding="md" aria-hidden="true">
            <UiStack gap="sm">
              <UiSkeletonBar class="h-5 w-40 max-w-full rounded-md" />
              <UiSkeletonBar class="h-3.5 w-32 max-w-full rounded-md" />
              <UiSkeletonBar class="h-3.5 w-24 rounded-md" />
            </UiStack>
          </UiCard>
        </UiGrid>
        <UiSkeletonTable v-if="card.table" :table="card.table" />
        <UiChartSkeleton v-if="card.chart" :kind="card.chart.kind" :height="card.chart.height ?? UiChartHeight.Md" />
        <UiFormActions v-if="card.actions">
          <UiSkeletonBar v-for="n in card.actions" :key="n" class="h-10 w-28 rounded-control" aria-hidden="true" />
        </UiFormActions>
      </template>
      <slot v-else />
    </UiStack>
  </UiCard>
</template>
