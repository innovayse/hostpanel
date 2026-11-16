<script setup lang="ts">
/**
 * Revenue card of the dashboard: income per day of the chosen period as bars, with a dashed line at the daily
 * average as the target to beat, and the total of the period with its change against the period before in the
 * title row. A day with no income is a zero bar, never a gap. The title row stays while the figures load.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import DashboardChartCard from '@/components/dashboard/DashboardChartCard.vue'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiChart from '@/components/ui/UiChart.vue'
import UiMetric from '@/components/ui/UiMetric.vue'
import UiStack from '@/components/ui/UiStack.vue'
import { formatChange, formatDayCaption } from '@/lib/dashboardOverview'
import { formatCurrencyWhole } from '@/lib/money'
import type { DashboardIncome } from '@/types/dashboardcharts'
import { UiChartHeight, UiChartType, UiChartVariant } from '@/utils/ui/chart'
import type { UiChartSeries } from '@/utils/ui/chart'
import { UiSize } from '@/utils/ui/size'
import { UiTone } from '@/utils/ui/tone'

/** Props for DashboardRevenueCard. */
const props = defineProps<{
  /** Income of the period, null until the first load answers. */
  income: DashboardIncome | null
  /** ISO 4217 code of the base currency the amounts are in. */
  currency: string
  /** Whether the figures are still loading. */
  loading: boolean
}>()

const { t, locale } = useI18n()

/** Whether there is nothing to draw yet. */
const waiting = computed<boolean>(() => props.income === null)

/** Day captions of the x axis. */
const labels = computed<string[]>(() => (props.income?.days ?? []).map((day) => formatDayCaption(day.date, locale.value)))

/** The one series of the chart. */
const series = computed<UiChartSeries[]>(() => [{ label: t('dashboard.revenue.income'), data: (props.income?.days ?? []).map((day) => day.amount) }])

/** The total of the period, formatted. */
const total = computed<string>(() => formatCurrencyWhole(props.income?.total ?? 0, props.currency))

/** The change against the previous period with its sign; undefined while unknown. */
const change = computed<string | undefined>(() => {
  const percent = props.income?.changePercent
  return percent === null || percent === undefined ? undefined : formatChange(percent)
})

/** Colour of the change: red for a drop, green otherwise. */
const changeTone = computed<UiTone>(() => ((props.income?.changePercent ?? 0) < 0 ? UiTone.Danger : UiTone.Success))

/**
 * Formats a chart number in the base currency.
 *
 * @param value - Amount to format.
 * @returns The amount with its currency symbol.
 */
const formatValue = (value: number): string => formatCurrencyWhole(value, props.currency)
</script>

<template>
  <DashboardChartCard :title="t('dashboard.revenue.title')" :note="t('dashboard.revenue.note')" data-testid="dashboard-revenue">
    <template #aside>
      <UiStack direction="row" align="center" gap="sm">
        <UiBadge v-if="change" :tone="changeTone" small data-testid="dashboard-revenue-change">{{ change }}</UiBadge>
        <UiMetric :value="total" :size="UiSize.Sm" :loading="waiting" data-testid="dashboard-revenue-total" />
      </UiStack>
    </template>
    <UiChart
      :type="UiChartType.Bar"
      :labels="labels"
      :series="series"
      :target="income?.dailyAverage"
      :target-label="t('dashboard.revenue.average')"
      :value-formatter="formatValue"
      :loading="loading || waiting"
      :height="UiChartHeight.Short"
      :variant="UiChartVariant.Minimal"
    />
  </DashboardChartCard>
</template>
