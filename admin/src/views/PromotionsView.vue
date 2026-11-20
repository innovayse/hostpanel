<script setup lang="ts">
/**
 * Promotions report: every promotion code recorded on a service, with how many services carry it and what the
 * active ones renew for each month, in the base currency. The title, the refresh action, the table columns and the
 * note are the ones of its schema, which the skeleton draws too.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import ReportPage from '@/components/reports/ReportPage.vue'
import { useReport } from '@/composables/useReport'
import { baseCurrencyFormatter } from '@/lib/money'
import { ReportCellFormat } from '@/types/reportcolumn'
import type { ReportPromotionRow, ReportPromotions } from '@/types/reportpromotion'
import type { ReportTableData } from '@/types/reporttabledata'
import schema from '@/views/PromotionsView.schema'

const { t } = useI18n()

const { data, load } = useReport<ReportPromotions>(() => '/reports/promotions')

/** Promotion codes with their usage; empty until loaded. */
const rows = computed<ReportPromotionRow[]>(() => data.value?.rows ?? [])

/** Base currency the amounts are converted to; empty until loaded. */
const currency = computed<string>(() => data.value?.currency ?? '')

/** Formats an amount in the base currency the API converted the report to. */
const formatMoney = baseCurrencyFormatter(() => currency.value)

/** The table of the codes: the counts as integers, the revenue as money with its explanation. */
const table = computed<ReportTableData<ReportPromotionRow>>(() => ({
  rows: rows.value,
  rowKey: 'code',
  extras: {
    services: { format: ReportCellFormat.Integer },
    activeServices: { format: ReportCellFormat.Integer },
    monthlyRevenue: { format: ReportCellFormat.Money, hint: t('reports.promotions.monthlyRevenueHint') },
  },
  formatMoney,
}))
</script>

<template>
  <ReportPage :retry="load" :schema="schema" :table="table" :base-currency="currency" />
</template>
