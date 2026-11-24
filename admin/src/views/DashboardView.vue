<script setup lang="ts">
/**
 * Admin dashboard: a greeting with the period toggle, four KPI tiles (the revenue one covers the chosen period and
 * shows its change against the period before), the daily revenue chart with its target line beside the "needs a
 * person" list, the new orders and clients, invoice status and open ticket charts, and the server capacity beside
 * the latest invoices.
 *
 * Reads `stores/dashboard`. The period toggle reloads the chart series, and with them the charts and the revenue
 * tile. A refusal of the statistics shows the store's sentence in an error state; a refusal of the chart series or
 * of a panel list leaves that part empty and shows an alert. Every block keeps its place and shows its own
 * placeholders while its data loads.
 */
import { computed, onMounted } from 'vue'
import { useI18n } from 'vue-i18n'
import DashboardAttentionList from '@/components/dashboard/DashboardAttentionList.vue'
import DashboardGrowthCard from '@/components/dashboard/DashboardGrowthCard.vue'
import DashboardInvoiceStatusCard from '@/components/dashboard/DashboardInvoiceStatusCard.vue'
import DashboardRecentInvoices from '@/components/dashboard/DashboardRecentInvoices.vue'
import DashboardRevenueCard from '@/components/dashboard/DashboardRevenueCard.vue'
import DashboardServerLoad from '@/components/dashboard/DashboardServerLoad.vue'
import DashboardTicketPriorityCard from '@/components/dashboard/DashboardTicketPriorityCard.vue'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiSplit from '@/components/ui/UiSplit.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiSegmented from '@/components/ui/UiSegmented.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatRow from '@/components/ui/UiStatRow.vue'
import { dayPartOf, formatChange } from '@/lib/dashboardOverview'
import { formatCurrencyWhole } from '@/lib/money'
import { schemaHeader } from '@/lib/schemaHeader'
import { useDashboardStore } from '@/stores/dashboard'
import { DashboardPeriod } from '@/types/dashboardcharts'
import type { SkeletonStat } from '@/utils/ui/skeletonSchema'
import type { UiStatFigure } from '@/utils/ui/statRow'
import { gridColumnsOf } from '@/utils/ui/skeletonLayout'
import type { UiTabItem } from '@/utils/ui/tabs'
import { UiGap } from '@/utils/ui/layout'
import { UiTone } from '@/utils/ui/tone'
import schema, { DASHBOARD_CHARTS_ROW, DASHBOARD_MAIN_ROW, DASHBOARD_PANELS_ROW, PERIOD_REVENUE_STAT_KEY } from '@/views/DashboardView.schema'

/** Dashboard store: the headline numbers, the chart series and the panel lists. */
const store = useDashboardStore()

const { t } = useI18n()

/** The options of the period toggle. */
const periodItems = computed<UiTabItem[]>(() => [
  { value: DashboardPeriod.Days30, label: t('dashboard.period.days30'), testid: 'dashboard-period-30' },
  { value: DashboardPeriod.Days90, label: t('dashboard.period.days90'), testid: 'dashboard-period-90' },
])

/** The period the toggle shows; choosing another one reloads the chart series. */
const period = computed<string>({
  /**
   * Reads the chosen period.
   *
   * @returns The period the store holds.
   */
  get: () => store.period,
  /**
   * Loads the series of the period the toggle chose; a value that is not a period is ignored.
   *
   * @param value - The option value the toggle emitted.
   */
  set: (value) => {
    const next = Object.values(DashboardPeriod).find((candidate) => candidate === value)
    if (next !== undefined) {
      void store.fetchCharts(next)
    }
  },
})

/** The greeting for the part of the day it is now. */
const greeting = computed<string>(() => t(`dashboard.greeting.${dayPartOf(new Date().getHours())}`))

/** Change of the period's income against the period before in percent; null while unknown. */
const incomeChange = computed<number | null>(() => store.charts?.income.changePercent ?? null)

/** The income change formatted with its sign, so it reads without colour; undefined while unknown. */
const incomeDelta = computed<string | undefined>(() => (incomeChange.value === null ? undefined : formatChange(incomeChange.value)))

/** Colour of the income change: red for a drop, green otherwise. */
const incomeDeltaTone = computed<UiTone>(() => ((incomeChange.value ?? 0) < 0 ? UiTone.Danger : UiTone.Success))

/** The period's income as the revenue tile shows it; empty while the series are not here yet (the tile then shows its placeholder). */
const incomeValue = computed<string>(() => (store.charts === null ? '' : formatCurrencyWhole(store.charts.income.total, store.charts.currency)))

/**
 * What a tile of the schema shows: the revenue tile names the chosen period and carries the change of the income, and each
 * tile waits for its own data (the revenue for the chart series, the others for the statistics).
 *
 * @param stat - The tile of the schema.
 * @returns The tile's figure, caption, change and loading state.
 */
const figureOf = (stat: SkeletonStat): UiStatFigure => {
  if (stat.key === PERIOD_REVENUE_STAT_KEY) {
    return {
      value: incomeValue.value,
      label: t(`dashboard.stats.revenue.${store.period}`),
      delta: incomeDelta.value,
      deltaTone: incomeDeltaTone.value,
      loading: store.charts === null,
    }
  }
  const figure = store.stats === null ? undefined : Object.entries(store.stats).find(([name]) => name === stat.key)
  return { value: figure === undefined ? '' : String(figure[1]), loading: store.stats === null }
}

/** What every tile of the schema shows, by tile key. */
const figures = computed<Record<string, UiStatFigure>>(() => Object.fromEntries(schema.stats.map((stat) => [stat.key, figureOf(stat)])))

onMounted(() => {
  void store.fetchStats()
  void store.fetchCharts()
  void store.fetchOverview()
})
</script>

<template>
  <UiStack :gap="UiGap.None">
    <UiPageHeader v-bind="schemaHeader(schema, t, { title: greeting })" data-testid="dashboard-header">
      <template #actions>
        <UiSegmented v-model="period" :items="periodItems" :aria-label="t('dashboard.period.label')" data-testid="dashboard-period" />
      </template>
    </UiPageHeader>

    <UiErrorState
      v-if="store.error"
      :message="store.error"
      :loading="store.loading"
      testid="dashboard-error"
      @retry="store.fetchStats()"
    />

    <UiStack v-else :gap="UiGap.None" class="gap-5.5">
      <UiStatRow :stats="schema.stats" :figures="figures" testid-prefix="dashboard-stat" data-testid="dashboard-stats" />

      <UiAlert v-if="store.chartsError" :variant="UiTone.Warning" data-testid="dashboard-charts-error">
        {{ store.chartsError }}
      </UiAlert>

      <UiAlert v-if="store.stats && store.overviewError" :variant="UiTone.Warning" data-testid="dashboard-overview-error">
        {{ store.overviewError }}
      </UiAlert>

      <UiSplit :ratio="DASHBOARD_MAIN_ROW.ratio" :gap="UiGap.Md" data-testid="dashboard-main">
        <DashboardRevenueCard
          :income="store.charts?.income ?? null"
          :currency="store.charts?.currency ?? ''"
          :loading="store.chartsLoading"
        />
        <DashboardAttentionList
          :overdue-invoices="store.overview.overdueInvoices ?? 0"
          :pending-orders="store.overview.pendingOrders ?? 0"
          :expiring-domains="store.overview.expiringDomains ?? 0"
          :urgent-tickets="store.overview.urgentTickets.length"
          :loading="store.overviewLoading && store.overview.overdueInvoices === null"
        />
      </UiSplit>

      <UiGrid :columns="gridColumnsOf(DASHBOARD_CHARTS_ROW.columns.length)" :gap="UiGap.Md" data-testid="dashboard-charts">
        <DashboardGrowthCard
          :new-orders="store.charts?.newOrders ?? null"
          :new-clients="store.charts?.newClients ?? null"
          :loading="store.chartsLoading"
        />
        <DashboardInvoiceStatusCard
          :statuses="store.charts?.invoicesByStatus ?? null"
          :currency="store.charts?.currency ?? ''"
          :loading="store.chartsLoading"
        />
        <DashboardTicketPriorityCard :priorities="store.charts?.openTickets ?? null" :loading="store.chartsLoading" />
      </UiGrid>

      <UiSplit :ratio="DASHBOARD_PANELS_ROW.ratio" :gap="UiGap.Md" data-testid="dashboard-panels">
        <DashboardServerLoad :servers="store.overview.servers" :loading="store.overviewLoading" />
        <DashboardRecentInvoices :invoices="store.overview.recentInvoices" :loading="store.overviewLoading" />
      </UiSplit>
    </UiStack>
  </UiStack>
</template>
