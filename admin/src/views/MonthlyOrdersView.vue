<script setup lang="ts">
/**
 * Monthly orders report: units sold and their value per product for a month, grouped by product
 * group, in the base currency the API converted them to.
 */
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import ReportPage from '@/components/reports/ReportPage.vue'
import ReportProductSalesTable from '@/components/reports/ReportProductSalesTable.vue'
import { useReport } from '@/composables/useReport'
import { baseCurrencyFormatter } from '@/lib/money'
import { currentMonthNumber, currentYear, monthName } from '@/lib/reportsPeriod'
import { reportPath } from '@/lib/reportsQuery'
import type { ReportMonthlyOrders } from '@/types/reportproductsales'
import schema from '@/views/MonthlyOrdersView.schema'

const { t } = useI18n()

/** Year being reported; the current one at first. */
const year = ref(currentYear())

/** Month being reported, 1 to 12; the current one at first. */
const month = ref(currentMonthNumber())

const { data, load } = useReport<ReportMonthlyOrders>(() =>
  reportPath('/reports/monthly-orders', { year: year.value, month: month.value }),
)

/** Title naming the month once a report is loaded; the schema's own title until then. */
const title = computed<string | undefined>(() =>
  data.value ? t('reports.titles.salesByProductFor', { month: monthName(data.value.month), year: data.value.year }) : undefined,
)

/** Base currency the figures are converted to; empty until loaded. */
const currency = computed<string>(() => data.value?.currency ?? '')

/** Formats an amount in the base currency the API converted the report to. */
const formatMoney = baseCurrencyFormatter(() => currency.value)
</script>

<template>
  <ReportPage
    v-model:year="year"
    v-model:month="month"
    :retry="load"
    :schema="schema"
    :title="title"
    :years-back="2"
    :years-ahead="2"
    :base-currency="currency"
    @generate="load"
  >
    <ReportProductSalesTable
      :groups="data?.groups ?? []"
      :total-units-sold="data?.totalUnitsSold ?? 0"
      :total-value="data?.totalValue ?? 0"
      :format-money="formatMoney"
    />
  </ReportPage>
</template>
