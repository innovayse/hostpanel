<script setup lang="ts">
/**
 * Growth card of the dashboard: new orders and new clients per day of the chosen period as two lines, with the
 * totals of the period in the title row. A day with nothing new is a zero point, never a gap. The title row
 * stays while the figures load.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import DashboardChartCard from '@/components/dashboard/DashboardChartCard.vue'
import UiChart from '@/components/ui/UiChart.vue'
import UiMetric from '@/components/ui/UiMetric.vue'
import UiStack from '@/components/ui/UiStack.vue'
import { formatDayCaption } from '@/lib/dashboardOverview'
import type { DashboardDailyCount } from '@/types/dashboardcharts'
import { UiChartHeight, UiChartType } from '@/utils/ui/chart'
import type { UiChartSeries } from '@/utils/ui/chart'
import { UiSize } from '@/utils/ui/size'

/** Props for DashboardGrowthCard. */
const props = defineProps<{
  /** Orders placed per day, null until the first load answers. */
  newOrders: DashboardDailyCount[] | null
  /** Client accounts created per day, null until the first load answers. */
  newClients: DashboardDailyCount[] | null
  /** Whether the figures are still loading. */
  loading: boolean
}>()

const { t, locale } = useI18n()

/** Whether there is nothing to draw yet. */
const waiting = computed<boolean>(() => props.newOrders === null || props.newClients === null)

/** Day captions of the x axis; both series cover the same days. */
const labels = computed<string[]>(() => (props.newOrders ?? []).map((day) => formatDayCaption(day.date, locale.value)))

/** One line per series. */
const series = computed<UiChartSeries[]>(() => [
  { label: t('dashboard.growth.orders'), data: (props.newOrders ?? []).map((day) => day.count) },
  { label: t('dashboard.growth.clients'), data: (props.newClients ?? []).map((day) => day.count) },
])

/**
 * Adds up a count series.
 *
 * @param days - The series.
 * @returns The sum of its counts.
 */
const sumOf = (days: DashboardDailyCount[] | null): number => (days ?? []).reduce((sum, day) => sum + day.count, 0)

/** Total new orders of the period. */
const ordersTotal = computed<number>(() => sumOf(props.newOrders))

/** Total new clients of the period. */
const clientsTotal = computed<number>(() => sumOf(props.newClients))
</script>

<template>
  <DashboardChartCard :title="t('dashboard.growth.title')" :note="t('dashboard.growth.note')" data-testid="dashboard-growth">
    <template #aside>
      <UiStack direction="row" align="center" gap="md">
        <UiMetric :value="ordersTotal" :label="t('dashboard.growth.orders')" :size="UiSize.Sm" :loading="waiting" data-testid="dashboard-growth-orders" />
        <UiMetric :value="clientsTotal" :label="t('dashboard.growth.clients')" :size="UiSize.Sm" :loading="waiting" data-testid="dashboard-growth-clients" />
      </UiStack>
    </template>
    <UiChart
      :type="UiChartType.Line"
      :labels="labels"
      :series="series"
      :loading="loading || waiting"
      :height="UiChartHeight.Short"
    />
  </DashboardChartCard>
</template>
