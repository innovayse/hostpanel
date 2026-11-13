<script setup lang="ts">
/**
 * Admin dashboard: a greeting with the period toggle, four KPI tiles (the monthly revenue one with its change
 * since last month), the revenue chart with its target line beside the "needs a person" list, and the server
 * capacity beside the latest invoices.
 *
 * Reads `stores/dashboard` (a refusal of the statistics shows the store's sentence in an error state; a refusal
 * of a panel list leaves that panel empty and shows one alert). Every block keeps its place and shows its own
 * placeholders while its data loads.
 */
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import DashboardAttentionList from '@/components/dashboard/DashboardAttentionList.vue'
import DashboardRecentInvoices from '@/components/dashboard/DashboardRecentInvoices.vue'
import DashboardRevenueCard from '@/components/dashboard/DashboardRevenueCard.vue'
import DashboardServerLoad from '@/components/dashboard/DashboardServerLoad.vue'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiSplit from '@/components/ui/UiSplit.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiSegmented from '@/components/ui/UiSegmented.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatTile from '@/components/ui/UiStatTile.vue'
import { dayPartOf, formatChange, DashboardPeriod, monthlyIncomeChange } from '@/lib/dashboardOverview'
import { formatCurrencyWhole } from '@/lib/money'
import { useDashboardStore } from '@/stores/dashboard'
import type { DashboardFigure } from '@/types/dashboardstats'
import { UiSplitRatio } from '@/utils/ui/layout'
import type { UiTabItem } from '@/utils/ui/tabs'
import { UiTone } from '@/utils/ui/tone'

/** One KPI tile of the dashboard. */
interface StatTileConfig {
  /** Field of `DashboardStats` the tile shows. */
  key: DashboardFigure
  /** Locale key of the tile caption. */
  labelKey: string
  /** Turns the raw number, with the currency the revenue figures are in, into the text shown on the tile. */
  format: (value: number, currency: string) => string
}

/** Dashboard store: the headline numbers and the panel lists. */
const store = useDashboardStore()

const { t } = useI18n()

/** The period the toggle chose. */
const period = ref<string>(DashboardPeriod.Days30)

/** The options of the period toggle. */
const periodItems = computed<UiTabItem[]>(() => [
  { value: DashboardPeriod.Days30, label: t('dashboard.period.days30'), testid: 'dashboard-period-30' },
  { value: DashboardPeriod.Days90, label: t('dashboard.period.days90'), testid: 'dashboard-period-90' },
])

/** The greeting for the part of the day it is now. */
const greeting = computed<string>(() => t(`dashboard.greeting.${dayPartOf(new Date().getHours())}`))

/**
 * Formats a count as plain text.
 *
 * @param value - The count.
 * @returns The count as a string.
 */
const formatCount = (value: number): string => value.toString()

/** The tiles, in display order. */
const statConfig: StatTileConfig[] = [
  { key: 'monthlyRevenue', labelKey: 'dashboard.stats.monthlyRevenue', format: formatCurrencyWhole },
  { key: 'activeServices', labelKey: 'dashboard.stats.activeServices', format: formatCount },
  { key: 'overdueInvoices', labelKey: 'dashboard.stats.overdueInvoices', format: formatCount },
  { key: 'openTickets', labelKey: 'dashboard.stats.openTickets', format: formatCount },
]

/** Change of this month's income against the last one in percent; null while unknown. */
const monthlyChange = computed<number | null>(() => monthlyIncomeChange(store.overview.income, new Date().getMonth()))

/** The monthly change formatted with its sign, so it reads without colour; undefined while unknown. */
const monthlyDelta = computed<string | undefined>(() => (monthlyChange.value === null ? undefined : formatChange(monthlyChange.value)))

/** Colour of the monthly change: red for a drop, green otherwise. */
const monthlyDeltaTone = computed<UiTone>(() => ((monthlyChange.value ?? 0) < 0 ? UiTone.Danger : UiTone.Success))

/**
 * The text of a tile; empty while the statistics are not here yet (the tile then shows its placeholders).
 *
 * @param stat - The tile.
 * @returns The formatted figure.
 */
const valueOf = (stat: StatTileConfig): string => (store.stats === null ? '' : stat.format(store.stats[stat.key], store.stats.currency))

onMounted(() => {
  void store.fetchStats()
  void store.fetchOverview()
})
</script>

<template>
  <UiStack gap="none">
    <UiPageHeader :title="greeting" :subtitle="t('dashboard.subtitle')" data-testid="dashboard-header">
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

    <UiStack v-else gap="none" class="gap-5.5">
      <UiGrid :columns="4" data-testid="dashboard-stats">
        <UiStatTile
          v-for="stat in statConfig"
          :key="stat.key"
          :label="t(stat.labelKey)"
          :value="valueOf(stat)"
          :loading="store.stats === null"
          :delta="stat.key === 'monthlyRevenue' ? monthlyDelta : undefined"
          :delta-tone="monthlyDeltaTone"
          :data-testid="`dashboard-stat-${stat.key}`"
        />
      </UiGrid>

      <UiAlert v-if="store.stats && store.overviewError" variant="warning" data-testid="dashboard-overview-error">
        {{ store.overviewError }}
      </UiAlert>

      <UiSplit :ratio="UiSplitRatio.WideMain" gap="md" data-testid="dashboard-main">
        <DashboardRevenueCard
          :income="store.overview.income"
          :currency="store.stats?.currency ?? ''"
          :period="period"
          :loading="store.overviewLoading && store.overview.income.length === 0"
        />
        <DashboardAttentionList
          :overdue-invoices="store.overview.overdueInvoices ?? 0"
          :pending-orders="store.overview.pendingOrders ?? 0"
          :expiring-domains="store.overview.expiringDomains ?? 0"
          :urgent-tickets="store.overview.urgentTickets.length"
          :loading="store.overviewLoading && store.overview.overdueInvoices === null"
        />
      </UiSplit>

      <UiSplit :ratio="UiSplitRatio.WideAside" gap="md" data-testid="dashboard-panels">
        <DashboardServerLoad :servers="store.overview.servers" :loading="store.overviewLoading" />
        <DashboardRecentInvoices :invoices="store.overview.recentInvoices" :loading="store.overviewLoading" />
      </UiSplit>
    </UiStack>
  </UiStack>
</template>
