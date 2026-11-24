<template>
  <UiSection v-if="comparable" padding="lg" class="border-b border-nova-border">
    <NovaSectionHeader
      :eyebrow="t('nova.comparison.eyebrow')"
      :title="t('nova.comparison.title')"
      :lead="t('nova.comparison.lead')"
    />

    <!--
      The table's scroll area is a labelled focus stop, which is what lets a
      keyboard user scroll a table wider than the screen; the label tells them
      what it is before they do. The first column stays pinned, so a value is
      never orphaned from the feature it belongs to. The `!` classes recolour the
      kit table with nova's tokens: the kit's own light and dark greys are
      variants and would otherwise win.
    -->
    <UiTable
      scrollable
      :label="t('nova.comparison.title')"
      class="mt-8 !border-nova-border !bg-nova-surface"
      data-testid="nova-comparison-table"
    >
      <UiText :as="UiTextTag.Caption" size="inherit" tone="inherit" class="sr-only">{{ t('nova.comparison.scrollHint') }}</UiText>
      <UiTableHead class="!border-nova-border !bg-transparent">
        <UiTableRow :hoverable="false">
          <UiTableTh
            scope="col"
            class="sticky left-0 z-10 min-w-[200px] px-5 !bg-nova-surface !text-[13px] !tracking-[0.06em] !text-nova-muted"
          >
            {{ t('nova.comparison.feature') }}
          </UiTableTh>
          <UiTableTh
            v-for="plan in plans"
            :key="plan.id"
            scope="col"
            class="min-w-[160px] px-5 !text-[15px] !font-bold !normal-case !tracking-normal !text-nova-ink"
          >
            {{ plan.name }}
          </UiTableTh>
        </UiTableRow>
      </UiTableHead>
      <UiTableBody class="!divide-nova-border">
        <UiTableRow v-for="row in orderedRows" :key="row.label" :hoverable="false">
          <UiTableTh
            scope="row"
            class="sticky left-0 z-10 px-5 py-3.5 !bg-nova-surface !text-[15px] !font-medium !normal-case !tracking-normal !text-nova-muted"
          >
            {{ row.label }}
          </UiTableTh>
          <UiTableTd
            v-for="(value, index) in row.values"
            :key="`${row.label}-${plans[index]?.id ?? index}`"
            class="px-5 py-3.5 !text-[15px] text-nova-ink"
          >
            <!-- A plan without this line reads as absent, never as zero. -->
            <template v-if="value">{{ value }}</template>
            <template v-else>
              <UiText :as="UiTextTag.Span" size="inherit" tone="inherit" aria-hidden="true">—</UiText>
              <UiText :as="UiTextTag.Span" size="inherit" tone="inherit" class="sr-only">{{ t('nova.comparison.absent') }}</UiText>
            </template>
          </UiTableTd>
        </UiTableRow>
      </UiTableBody>
    </UiTable>
  </UiSection>
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
import NovaSectionHeader from '~/templates/nova/sections/NovaSectionHeader.vue'
import { orderComparisonRows } from '~/templates/nova/content'
import { isComparable } from '~/utils/comparisonRows'
import type { ComparisonRow } from '~/types/comparisonrow'
import type { PlanCard } from '~/types/plancard'
import { UiTextTag } from '~/utils/ui/enums'

/** Props of ComparisonTable. */
const props = withDefaults(defineProps<{
  /** The plans, one column each. */
  plans?: PlanCard[]
  /** Rows in display order, each already aligned to the plan columns. */
  rows?: ComparisonRow[]
}>(), { plans: () => [], rows: () => [] })

/** Translator for the copy. */
const { t } = useI18n()

/** Whether the plans share enough lines for the table to add value. */
const comparable = computed(() => isComparable(props.rows))

/** The rows rearranged into the order these lines usually read best in. */
const orderedRows = computed(() => orderComparisonRows(props.rows))
</script>
