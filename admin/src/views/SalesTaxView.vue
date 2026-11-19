<script setup lang="ts">
/**
 * Sales tax report: the invoices paid in a period with the tax they carried. The totals come one
 * per invoice currency (the summary lines of the schema), never added across currencies; the invoice rows are paged and
 * each amount is shown in its invoice's currency. The title, the filter caption and the table columns are the ones of
 * its schema, which the skeleton draws too.
 */
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import ReportPage from '@/components/reports/ReportPage.vue'
import UiDateRangePicker from '@/components/ui/UiDateRangePicker.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { useReport } from '@/composables/useReport'
import { REPORT_PAGE_SIZE } from '@/lib/limits'
import { reportApiPath } from '@/lib/apiPaths'
import { formatCurrencyCode } from '@/lib/money'
import { reportPath } from '@/lib/reportsQuery'
import { DateRangePreset, presetIsoRange } from '@/utils/ui/datePicker'
import { ReportCellFormat } from '@/types/reportcolumn'
import type { ReportColumnExtra } from '@/types/reportcolumn'
import type { ReportSalesTax, ReportSalesTaxRow } from '@/types/reportsalestax'
import type { ReportTableData } from '@/types/reporttabledata'
import { ReportFieldWidth } from '@/utils/ui/reportField'
import schema from '@/views/SalesTaxView.schema'

const { t } = useI18n()

/** Width of the date range filter. */
const FILTER_WIDTHS = { dateRange: ReportFieldWidth.Grow }

/** How the cells of the schema columns are written: the dates as dates, every amount in its invoice's currency. */
const COLUMN_EXTRAS: Readonly<Record<string, ReportColumnExtra>> = {
  invoiceDate: { format: ReportCellFormat.Date },
  datePaid: { format: ReportCellFormat.Date },
  subTotal: { format: ReportCellFormat.Money, currencyKey: 'currency' },
  tax: { format: ReportCellFormat.Money, currencyKey: 'currency' },
  credit: { format: ReportCellFormat.Money, currencyKey: 'currency' },
  total: { format: ReportCellFormat.Money, currencyKey: 'currency' },
}

/** The payment days to report; the current year at first. */
const dateRange = ref<[string, string] | null>(presetIsoRange(DateRangePreset.ThisYear))

/** Page of invoice rows shown, starting at 1. */
const page = ref(1)

const { data, load } = useReport<ReportSalesTax>(() =>
  reportPath(reportApiPath('sales-tax'), {
    from: dateRange.value?.[0],
    to: dateRange.value?.[1],
    page: page.value,
    pageSize: REPORT_PAGE_SIZE,
  }),
)

/** The invoices of the page shown, with the pager over every page. */
const table = computed<ReportTableData<ReportSalesTaxRow>>(() => ({
  rows: data.value?.rows.items ?? [],
  rowKey: 'id',
  extras: COLUMN_EXTRAS,
  pagination: { page: page.value, total: data.value?.rows.totalCount ?? 0, pageSize: REPORT_PAGE_SIZE, testid: 'report-sales-tax-pagination' },
}))

/** Runs the report for the chosen period from its first page. */
const generate = async (): Promise<void> => {
  page.value = 1
  await load()
}

/**
 * Shows another page of invoice rows.
 *
 * @param next - The page to show.
 */
const goToPage = async (next: number): Promise<void> => {
  page.value = next
  await load()
}
</script>

<template>
  <ReportPage
    :retry="load"
    :schema="schema"
    :widths="FILTER_WIDTHS"
    :generate-label="t('shared.generateReport')"
    :table="table"
    @generate="generate"
    @page="goToPage"
  >
    <template #field-dateRange>
      <UiDateRangePicker v-model="dateRange" :placeholder="t('shared.selectDateRange')" />
    </template>

    <template #summary>
      <UiStack gap="xs" data-testid="report-sales-tax-summary">
        <UiText v-for="total in data?.totals ?? []" :key="total.currency" tone="secondary" size="md">
          {{
            t('reports.salesTax.currencySummary', {
              currency: total.currency,
              count: total.totalInvoices,
              invoiced: formatCurrencyCode(total.totalInvoiced, total.currency),
              tax: formatCurrencyCode(total.taxLiability, total.currency),
            })
          }}
        </UiText>
      </UiStack>
    </template>

    <UiText tone="muted" size="sm">
      {{ t('reports.salesTax.thisReportExcludesInvoicesThat') }}
    </UiText>
  </ReportPage>
</template>
