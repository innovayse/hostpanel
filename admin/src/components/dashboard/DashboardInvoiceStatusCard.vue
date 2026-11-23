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
import { INVOICE_STATUS_OPTIONS } from '@/lib/constants'
import { formatCurrencyWhole } from '@/lib/money'
import { PANEL_CHARTS, PanelChart } from '@/lib/panelCharts'
import { statusLabel } from '@/lib/tones'
import type { DashboardInvoiceStatusCount } from '@/types/dashboardcharts'
import type { UiChartSeries } from '@/utils/ui/chart'
import { UiAlignItems, UiDirection, UiGap, UiJustify } from '@/utils/ui/layout'
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

/** Kind and height of the chart, the same entry the dashboard skeleton draws the panel from. */
const chart = PANEL_CHARTS[PanelChart.InvoiceStatus]

const { t } = useI18n()

/** Whether there is nothing to draw yet. */
const waiting = computed<boolean>(() => props.statuses === null)

/** The rows, one per status. */
const rows = computed<DashboardInvoiceStatusCount[]>(() => props.statuses ?? [])

/** Status names of the x axis, the same words the invoice list filter uses. */
const labels = computed<string[]>(() => rows.value.map((row) => statusLabel(INVOICE_STATUS_OPTIONS, row.status)))

/** The one series of the chart: the count per status. */
const series = computed<UiChartSeries[]>(() => [{ label: t('dashboard.invoiceStatus.count'), data: rows.value.map((row) => row.count) }])
</script>

<template>
  <DashboardChartCard :title="t('dashboard.invoiceStatus.title')" :note="t('dashboard.invoiceStatus.note')" data-testid="dashboard-invoice-status">
    <UiChart
      :kind="chart.kind"
      :labels="labels"
      :series="series"
      :loading="loading || waiting"
      :height="chart.height"
    />
    <UiStack :gap="UiGap.Sm">
      <UiStack
        v-for="row in rows"
        :key="row.status"
        :direction="UiDirection.Row"
        :align="UiAlignItems.Center"
        :justify="UiJustify.Between"
        :data-testid="`dashboard-invoice-status-${row.status}`"
      >
        <UiText :as="UiTextTag.Span" :size="UiTextSize.Sm">{{ statusLabel(INVOICE_STATUS_OPTIONS, row.status) }}</UiText>
        <UiText :as="UiTextTag.Span" :tone="UiTextTone.Muted" :size="UiTextSize.Sm">
          {{ row.count }} · {{ formatCurrencyWhole(row.amount, currency) }}
        </UiText>
      </UiStack>
    </UiStack>
  </DashboardChartCard>
</template>
