<script setup lang="ts">
/**
 * Row of stat tiles under the header of a report: the headline figures before the chart and table.
 *
 * Tiles flow into the kit grid: one column on narrow screens, two from `md` and four from `lg`.
 */
import { inject } from 'vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiStatTile from '@/components/ui/UiStatTile.vue'
import { NOT_LOADING, REPORT_LOADING } from '@/lib/reportLoading'
import type { ReportStat } from '@/types/reportstat'

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
    />
  </UiGrid>
</template>
