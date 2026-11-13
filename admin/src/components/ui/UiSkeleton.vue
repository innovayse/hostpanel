<script setup lang="ts">
/**
 * Shimmering placeholder shown while content loads.
 *
 * `shape` picks a text line, a block or a circle (`lines` repeats the text shape, the last line is
 * shorter, like a paragraph), or one of two presets that mimic a whole area: a `table` and a grid of
 * `cards`; `count` sets how many rows or cards a preset draws and `columns` how many cells a table row has. The sweep stops under `prefers-reduced-motion`. It is hidden from
 * assistive technology; mark the loading region with `aria-busy` on the container.
 */
import { computed } from 'vue'
import UiSkeletonBar from '@/components/ui/UiSkeletonBar.vue'
import { skeletonPresets, UiSkeletonShape } from '@/utils/ui/skeleton'

/** Props for UiSkeleton. */
const props = withDefaults(defineProps<{
  /** Shape of the placeholder, or a preset layout. */
  shape?: UiSkeletonShape
  /** Number of text lines (for `text` only). */
  lines?: number
  /** Rows of a `table` or cards of `cards`; each preset has its own default. */
  count?: number
  /** Cells per row of a `table`. */
  columns?: number
}>(), {
  shape: UiSkeletonShape.Text,
  lines: 1,
  count: undefined,
  columns: 4,
})

/** Items a preset draws when `count` is not given. */
const presetCounts: Partial<Record<UiSkeletonShape, number>> = {
  table: 5,
  cards: 6,
}

/** Layout classes of the root, per shape. */
const rootClasses: Record<UiSkeletonShape, string> = {
  text: 'flex flex-col gap-2',
  block: 'flex flex-col gap-2',
  circle: 'flex flex-col gap-2',
  table: 'flex flex-col',
  cards: 'grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-3',
}

/** Classes of one single-shape bar. */
const singleClasses: Partial<Record<UiSkeletonShape, string>> = {
  text: 'h-3 rounded',
  block: 'h-24 rounded-card',
  circle: 'h-10 w-10 rounded-full',
}

/**
 * Builds the indexes `0..length-1`, which key a repeated placeholder.
 *
 * @param length - How many indexes to build (at least 1).
 * @returns The indexes.
 */
const indexes = (length: number): number[] => Array.from({ length: Math.max(1, length) }, (_, i) => i)

/** True when the shape is a preset layout rather than a single placeholder. */
const isPreset = computed<boolean>(() => skeletonPresets.includes(props.shape))

/** How many rows, cards or fields a preset draws. */
const itemIndexes = computed<number[]>(() => indexes(props.count ?? presetCounts[props.shape] ?? 1))

/** Cell indexes of one table row. */
const columnIndexes = computed<number[]>(() => indexes(props.columns))

/** Indexes of the placeholders of a single shape: several for a multi-line text, one otherwise. */
const singleIndexes = computed<number[]>(() => indexes(props.shape === UiSkeletonShape.Text ? props.lines : 1))

/**
 * Classes of one placeholder of a single shape: a circle keeps its intrinsic size, the final line of
 * a multi-line text is shorter.
 *
 * @param index - Zero-based index of the placeholder.
 * @returns The size, rounding and width classes.
 */
const singleBarClasses = (index: number): (string | undefined)[] => {
  if (props.shape === UiSkeletonShape.Circle) {
    return [singleClasses.circle]
  }
  const isShortLine = props.shape === UiSkeletonShape.Text && props.lines > 1 && index === singleIndexes.value.length - 1
  return [singleClasses[props.shape], isShortLine ? 'w-2/3' : 'w-full']
}
</script>

<template>
  <div :class="rootClasses[shape]" aria-hidden="true">
    <template v-if="!isPreset">
      <UiSkeletonBar v-for="index in singleIndexes" :key="index" :class="singleBarClasses(index)" />
    </template>

    <template v-else-if="shape === UiSkeletonShape.Table">
      <div class="flex gap-4 border-b border-border px-1 pb-3" data-testid="ui-skeleton-table-head">
        <UiSkeletonBar v-for="column in columnIndexes" :key="column" class="h-2.5 flex-1 rounded" />
      </div>
      <div
        v-for="row in itemIndexes"
        :key="row"
        class="flex gap-4 border-b border-border-subtle px-1 py-3.5 last:border-b-0"
        data-testid="ui-skeleton-table-row"
      >
        <UiSkeletonBar v-for="column in columnIndexes" :key="column" class="h-3 flex-1 rounded" />
      </div>
    </template>

    <template v-else>
      <div
        v-for="card in itemIndexes"
        :key="card"
        class="glass flex flex-col gap-3 rounded-card p-5"
        data-testid="ui-skeleton-card"
      >
        <UiSkeletonBar class="h-10 w-10 rounded-full" />
        <UiSkeletonBar class="h-3 w-1/2 rounded" />
        <UiSkeletonBar class="h-3 w-full rounded" />
        <UiSkeletonBar class="h-3 w-2/3 rounded" />
      </div>
    </template>
  </div>
</template>
