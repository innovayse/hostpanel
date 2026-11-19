<script setup lang="ts">
/**
 * A row of stat tiles drawn from a schema's `stats`: the headline figures of a screen.
 *
 * Tiles flow into the kit grid, two across on a phone and `columns` across from `lg` (four by default). The caption of each tile is its schema key
 * resolved here and stays while `loading`; everything else comes from `figures` by the tile's key: the value (empty until it
 * is known), the colour, the change and a caption of its own when the tile's name follows the data. A tile is a bar while
 * the row or that figure is `loading`. `variant` `inline` draws the same figures as plain captioned values, for a card that
 * frames them already. `testidPrefix` names the tiles for the screen's own tests (`<prefix>-<key>`).
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiSkeletonBar from '@/components/ui/UiSkeletonBar.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatTile from '@/components/ui/UiStatTile.vue'
import UiText from '@/components/ui/UiText.vue'
import { UiGap } from '@/utils/ui/layout'
import type { UiGridColumns } from '@/utils/ui/layout'
import type { SkeletonStat } from '@/utils/ui/skeletonSchema'
import { UiStatRowVariant } from '@/utils/ui/statRow'
import type { UiStatFigure } from '@/utils/ui/statRow'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'

/** Resolves the locale keys the schema names into the operator's language. */
const { t } = useI18n()

/** Props for UiStatRow. */
const props = withDefaults(defineProps<{
  /** Tiles in display order. */
  stats: readonly SkeletonStat[]
  /** What the screen knows of each tile, by tile key. */
  figures?: Readonly<Record<string, UiStatFigure>>
  /** Columns of the row from `lg`. */
  columns?: UiGridColumns
  /** The look of the tiles. */
  variant?: UiStatRowVariant
  /** Prefix of each tile's `data-testid`. */
  testidPrefix?: string
  /** Draws bars in place of every figure. */
  loading?: boolean
}>(), {
  /**
   * Starts with no figure known for any tile.
   *
   * @returns An empty map of figures.
   */
  figures: () => ({}),
  columns: 4,
  variant: UiStatRowVariant.Tile,
  testidPrefix: 'ui-stat-row',
  loading: false,
})

/** What the template draws for one tile: its caption, its figure and whether it is still a bar. */
interface StatCell {
  /** Key of the tile; names its `data-testid` and its place in the list. */
  key: string
  /** The caption: the figure's own, or the schema's resolved. */
  label: string
  /** What the screen knows of the tile; absent while it is unknown. */
  figure: UiStatFigure | undefined
  /** Whether the tile is a bar: the row or this figure is loading, or nothing is known of it yet. */
  loading: boolean
}

/** The tiles as drawn, in the order of the schema. */
const cells = computed<StatCell[]>(() =>
  props.stats.map((stat) => {
    const figure = props.figures[stat.key]
    return { key: stat.key, label: figure?.label ?? t(stat.label), figure, loading: props.loading || figure?.loading === true }
  }),
)

/** Gap between the tiles: tight for the inline figures of a card, the usual one between tile cards. */
const gap = computed<UiGap>(() => (props.variant === UiStatRowVariant.Inline ? UiGap.Sm : UiGap.Md))
</script>

<template>
  <UiGrid paired :columns="columns" :gap="gap" data-testid="ui-stat-row">
    <template v-for="cell in cells" :key="cell.key">
      <UiStatTile
        v-if="variant === UiStatRowVariant.Tile"
        :label="cell.label"
        :value="cell.figure?.value ?? ''"
        :tone="cell.figure?.tone"
        :delta="cell.figure?.delta"
        :delta-tone="cell.figure?.deltaTone"
        :loading="cell.loading"
        :data-testid="`${testidPrefix}-${cell.key}`"
      />
      <UiStack v-else :gap="UiGap.Xs" class="min-w-0" :data-testid="`${testidPrefix}-${cell.key}`">
        <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Xs">
          {{ cell.label }}
        </UiText>
        <UiSkeletonBar v-if="cell.loading || cell.figure === undefined" class="h-4 w-20 rounded-md" aria-hidden="true" />
        <UiText v-else :tone="UiTextTone.Primary" :size="UiTextSize.Sm" class="truncate font-medium">
          {{ cell.figure.value }}
        </UiText>
      </UiStack>
    </template>
  </UiGrid>
</template>
