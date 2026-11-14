<script setup lang="ts">
/**
 * Open tickets card of the dashboard: one bar per priority, the highest first. Open tickets are a state, not an
 * event, so this card does not follow the period toggle. The title row stays while the figures load.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import DashboardChartCard from '@/components/dashboard/DashboardChartCard.vue'
import UiChart from '@/components/ui/UiChart.vue'
import type { DashboardTicketPriorityCount } from '@/types/dashboardcharts'
import { UiChartHeight, UiChartType } from '@/utils/ui/chart'
import type { UiChartSeries } from '@/utils/ui/chart'

/** Props for DashboardTicketPriorityCard. */
const props = defineProps<{
  /** Open tickets per priority, null until the first load answers. */
  priorities: DashboardTicketPriorityCount[] | null
  /** Whether the figures are still loading. */
  loading: boolean
}>()

const { t } = useI18n()

/** Whether there is nothing to draw yet. */
const waiting = computed<boolean>(() => props.priorities === null)

/** Priority names of the x axis. */
const labels = computed<string[]>(() => (props.priorities ?? []).map((row) => t(`ticketPriority.${row.priority}`)))

/** The one series of the chart: the open tickets per priority. */
const series = computed<UiChartSeries[]>(() => [{ label: t('dashboard.tickets.open'), data: (props.priorities ?? []).map((row) => row.count) }])
</script>

<template>
  <DashboardChartCard :title="t('dashboard.tickets.title')" :note="t('dashboard.tickets.note')" data-testid="dashboard-tickets">
    <UiChart
      :type="UiChartType.Bar"
      :labels="labels"
      :series="series"
      :loading="loading || waiting"
      :height="UiChartHeight.Sm"
    />
  </DashboardChartCard>
</template>
