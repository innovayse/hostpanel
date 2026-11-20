<script setup lang="ts">
/**
 * Columns of cards of a detail screen, drawn from the `columns` of its schema.
 *
 * Each card is a `UiDetailCard`: its title and either rows of a caption over a value or a table, fields, lines and boxes.
 * The screen puts the real body of a card in the slot `card-<key>`. While `loading` the title, the row captions and the
 * table header captions are real page text and the values are bars, so the screen and its skeleton are made of the same
 * cards; the slots are not drawn then. `loadingKeys` does the same for single cards whose data arrives on its own.
 *
 * `ratio` shares the width between two columns (three quarters and a quarter, say). A single column whose cards are half
 * wide flows two across in the same ratio, cards that take the whole row spanning both, so a header card over a split is
 * one column of three cards.
 */
import { computed } from 'vue'
import UiDetailCard from '@/components/ui/UiDetailCard.vue'
import UiStack from '@/components/ui/UiStack.vue'
import { gapClasses, gridColumnClasses, splitRatioClasses, UiSplitRatio } from '@/utils/ui/layout'
import { gridColumnsOf } from '@/utils/ui/skeletonLayout'
import { SkeletonSpan } from '@/utils/ui/skeletonSchema'
import type { SkeletonDetailCard } from '@/utils/ui/skeletonSchema'

/** Props for UiDetailCards. */
const props = withDefaults(defineProps<{
  /** Columns of cards side by side; each inner list is one column's stack. */
  columns: readonly (readonly SkeletonDetailCard[])[]
  /** How the width is shared between two columns, or between the two tracks of a single column of half cards. */
  ratio?: UiSplitRatio
  /** Draws the skeleton of each card in place of its slot. */
  loading?: boolean
  /** Keys of the cards whose own data is still being read, drawn as skeletons while the other cards show their slots. */
  loadingKeys?: readonly string[]
}>(), {
  ratio: undefined,
  loading: false,
  /**
   * Starts with no card loading on its own.
   *
   * @returns An empty list of keys.
   */
  loadingKeys: () => [],
})

/** The cards of the single column when they flow two across (some card is half wide); empty when the columns stack. */
const flowing = computed<readonly SkeletonDetailCard[]>(() => {
  const only = props.columns.length === 1 ? (props.columns[0] ?? []) : []
  return only.some((card) => card.span === SkeletonSpan.Half) ? only : []
})

/** Classes of the grid the columns sit in: the ratio's template for a split or a flow, equal columns otherwise. */
const gridClasses = computed<string[]>(() => {
  if (flowing.value.length > 0) {
    return ['grid items-start', splitRatioClasses[props.ratio ?? UiSplitRatio.Half], gapClasses.md]
  }
  if (props.columns.length === 2 && props.ratio !== undefined) {
    return ['grid', splitRatioClasses[props.ratio], gapClasses.md]
  }
  return ['grid', gridColumnClasses[gridColumnsOf(props.columns.length)], gapClasses.md]
})
</script>

<template>
  <div :class="gridClasses">
    <template v-if="flowing.length > 0">
      <UiDetailCard v-for="card in flowing" :key="card.key" :card="card" :loading="loading || loadingKeys.includes(card.key)" :class="card.span === SkeletonSpan.Half ? '' : 'col-span-full'">
        <slot :name="`card-${card.key}`" />
      </UiDetailCard>
    </template>
    <template v-else>
      <UiStack v-for="(column, index) in columns" :key="index">
        <UiDetailCard v-for="card in column" :key="card.key" :card="card" :loading="loading || loadingKeys.includes(card.key)">
          <slot :name="`card-${card.key}`" />
        </UiDetailCard>
      </UiStack>
    </template>
  </div>
</template>
