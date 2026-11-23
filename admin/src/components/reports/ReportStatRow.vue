<script setup lang="ts">
/**
 * Row of stat tiles under the header of a report: the headline figures before the chart and table.
 *
 * Tiles flow into the kit grid: one column on narrow screens, two from `md` and four from `lg`. A stat's `hint`
 * is the line under its figure; its `tooltip` explains the metric on hover or focus of an info icon beside the label.
 */
import { inject } from 'vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiStatTile from '@/components/ui/UiStatTile.vue'
import UiText from '@/components/ui/UiText.vue'
import UiTooltip from '@/components/ui/UiTooltip.vue'
import { Icon } from '@/lib/icons'
import { NOT_LOADING, REPORT_LOADING } from '@/lib/reportLoading'
import type { ReportStat } from '@/types/reportstat'
import { UiSize } from '@/utils/ui/size'
import { UiTextTag, UiTextTone } from '@/utils/ui/text'

/** Props for ReportStatRow. */
defineProps<{
  /** Tiles in display order. */
  stats: readonly ReportStat[]
}>()

/** True while the report loads: the tiles keep their place and draw skeletons. */
const loading = inject(REPORT_LOADING, NOT_LOADING)
</script>

<template>
  <UiGrid :columns="4" data-testid="report-stats">
    <UiStatTile
      v-for="stat in stats"
      :key="stat.key"
      :label="stat.label"
      :value="stat.value"
      :hint="stat.hint"
      :tone="stat.tone"
      :loading="loading"
      :data-testid="`report-stat-${stat.key}`"
    >
      <template v-if="stat.tooltip" #icon>
        <UiTooltip :text="stat.tooltip" placement="bottom">
          <UiText :as="UiTextTag.Span" :tone="UiTextTone.Muted" tabindex="0" :aria-label="stat.tooltip" :data-testid="`report-stat-${stat.key}-tooltip`">
            <UiIcon :markup="Icon.Info" :size="UiSize.Sm" />
          </UiText>
        </UiTooltip>
      </template>
    </UiStatTile>
  </UiGrid>
</template>
