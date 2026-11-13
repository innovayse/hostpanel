<script setup lang="ts">
/**
 * Revenue card of the dashboard: income per month as bars for the months the chosen period reaches into,
 * with a dashed line at the average of the months so far this year as the target to beat. The title row
 * stays while the figures load.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import UiCard from '@/components/ui/UiCard.vue'
import UiChart from '@/components/ui/UiChart.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { averageIncome, incomeInPeriod } from '@/lib/dashboardOverview'
import { formatCurrencyWhole } from '@/lib/money'
import type { ReportMonthIncome } from '@/types/reportmonthincome'
import { UiCardRadius } from '@/utils/ui/card'
import { UiChartHeight, UiChartType, UiChartVariant } from '@/utils/ui/chart'
import type { UiChartSeries } from '@/utils/ui/chart'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'

/** Props for DashboardRevenueCard. */
const props = defineProps<{
  /** Income per month of the current year, January first. */
  income: ReportMonthIncome[]
  /** ISO 4217 code of the base currency the amounts are in. */
  currency: string
  /** The period the toggle chose; it decides how many months are drawn. */
  period: string
  /** Whether the figures are still loading. */
  loading: boolean
}>()

const { t } = useI18n()

/** The months drawn: the ones the period reaches into. */
const shown = computed<ReportMonthIncome[]>(() => incomeInPeriod(props.income, new Date().getMonth(), props.period))

/** Month captions of the x axis. */
const labels = computed<string[]>(() => shown.value.map((month) => month.month))

/** The one series of the chart. */
const series = computed<UiChartSeries[]>(() => [{ label: t('dashboard.revenue.income'), data: shown.value.map((month) => month.amount) }])

/** The target line: the average of the months up to the current one. */
const target = computed<number | undefined>(() => averageIncome(props.income, new Date().getMonth()))

/**
 * Formats a chart number in the base currency.
 *
 * @param value - Amount to format.
 * @returns The amount with its currency symbol.
 */
const formatValue = (value: number): string => formatCurrencyWhole(value, props.currency)
</script>

<template>
  <UiCard padding="none" :radius="UiCardRadius.Panel" class="p-5.5" data-testid="dashboard-revenue">
    <UiStack gap="md">
      <UiStack direction="row" align="center" justify="between">
        <UiHeading :level="2" variant="card">{{ t('dashboard.revenue.title') }}</UiHeading>
        <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Xs">{{ t('dashboard.revenue.note') }}</UiText>
      </UiStack>
      <UiChart
        :type="UiChartType.Bar"
        :labels="labels"
        :series="series"
        :target="target"
        :target-label="t('dashboard.revenue.average')"
        :value-formatter="formatValue"
        :loading="loading"
        :height="UiChartHeight.Short"
        :variant="UiChartVariant.Minimal"
      />
    </UiStack>
  </UiCard>
</template>
