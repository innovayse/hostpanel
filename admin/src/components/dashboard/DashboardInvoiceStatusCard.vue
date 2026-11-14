<script setup lang="ts">
/**
 * Invoice status card of the dashboard: the invoices raised in the chosen period as one bar per status (paid,
 * unpaid, overdue, cancelled), with the count and the amount of each listed under the chart. The title row
 * stays while the figures load.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import DashboardChartCard from '@/components/dashboard/DashboardChartCard.vue'
import UiChart from '@/components/ui/UiChart.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { formatCurrencyWhole } from '@/lib/money'
import type { DashboardInvoiceStatusCount } from '@/types/dashboardcharts'
import { UiChartHeight, UiChartType } from '@/utils/ui/chart'
import type { UiChartSeries } from '@/utils/ui/chart'
import { UiTextSize, UiTextTag, UiTextTone } from '@/utils/ui/text'

/** Props for DashboardInvoiceStatusCard. */
const props = defineProps<{
  /** Invoices raised in the period per status, null until the first load answers. */
  statuses: DashboardInvoiceStatusCount[] | null
  /** ISO 4217 code of the base currency the amounts are in. */
  currency: string
  /** Whether the figures are still loading. */
  loading: boolean
}>()

const { t } = useI18n()

/** Whether there is nothing to draw yet. */
const waiting = computed<boolean>(() => props.statuses === null)

/** The rows, one per status. */
const rows = computed<DashboardInvoiceStatusCount[]>(() => props.statuses ?? [])

/** Status names of the x axis. */
const labels = computed<string[]>(() => rows.value.map((row) => t(`dashboard.invoiceStatus.${row.status}`)))

/** The one series of the chart: the count per status. */
const series = computed<UiChartSeries[]>(() => [{ label: t('dashboard.invoiceStatus.count'), data: rows.value.map((row) => row.count) }])
</script>

<template>
  <DashboardChartCard :title="t('dashboard.invoiceStatus.title')" :note="t('dashboard.invoiceStatus.note')" data-testid="dashboard-invoice-status">
    <UiChart
      :type="UiChartType.Bar"
      :labels="labels"
      :series="series"
      :loading="loading || waiting"
      :height="UiChartHeight.Sm"
    />
    <UiStack gap="sm">
      <UiStack
        v-for="row in rows"
        :key="row.status"
        direction="row"
        align="center"
        justify="between"
        :data-testid="`dashboard-invoice-status-${row.status}`"
      >
        <UiText :as="UiTextTag.Span" :size="UiTextSize.Sm">{{ t(`dashboard.invoiceStatus.${row.status}`) }}</UiText>
        <UiText :as="UiTextTag.Span" :tone="UiTextTone.Muted" :size="UiTextSize.Sm">
          {{ row.count }} · {{ formatCurrencyWhole(row.amount, currency) }}
        </UiText>
      </UiStack>
    </UiStack>
  </DashboardChartCard>
</template>
