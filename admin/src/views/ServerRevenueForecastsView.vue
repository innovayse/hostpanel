<script setup lang="ts">
/**
 * Server revenue forecasts report: for each server, the active services provisioned on it and what they renew for,
 * monthly and over a year, in the base currency. The title, the refresh action, the table columns and the note are the
 * ones of its schema, which the skeleton draws too.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import ReportPage from '@/components/reports/ReportPage.vue'
import { useReport } from '@/composables/useReport'
import { baseCurrencyFormatter } from '@/lib/money'
import { ReportCellFormat } from '@/types/reportcolumn'
import type { ReportServerRevenue, ReportServerRevenueRow } from '@/types/reportserverrevenue'
import type { ReportTableData } from '@/types/reporttabledata'
import schema from '@/views/ServerRevenueForecastsView.schema'

const { t } = useI18n()

const { data, load } = useReport<ReportServerRevenue>(() => '/reports/server-revenue-forecasts')

/** Servers with their forecast; empty until loaded. */
const rows = computed<ReportServerRevenueRow[]>(() => data.value?.rows ?? [])

/** Base currency the amounts are converted to; empty until loaded. */
const currency = computed<string>(() => data.value?.currency ?? '')

/** Formats an amount in the base currency the API converted the report to. */
const formatMoney = baseCurrencyFormatter(() => currency.value)

/** The table of the servers: the count as an integer, the revenue as money with its explanation. */
const table = computed<ReportTableData<ReportServerRevenueRow>>(() => ({
  rows: rows.value,
  rowKey: 'serverId',
  extras: {
    activeServices: { format: ReportCellFormat.Integer },
    monthlyRevenue: { format: ReportCellFormat.Money, hint: t('reports.serverRevenueForecasts.monthlyRevenueHint') },
    annualRevenue: { format: ReportCellFormat.Money, hint: t('reports.serverRevenueForecasts.annualRevenueHint') },
  },
  formatMoney,
}))
</script>

<template>
  <ReportPage :retry="load" :schema="schema" :table="table" :base-currency="currency" />
</template>
