<script setup lang="ts">
/**
 * OSS report (One Stop Shop): the invoiced value and VAT collected per EU member state, VAT rate and invoice
 * currency for a quarter, net of the refunds made in it. Each amount is shown in its own currency;
 * the footer sums the money only when every row shares one currency. The title, the filter caption and the
 * table columns are the ones of its schema, which the skeleton draws too; the line naming the period is the schema's summary.
 */
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import ReportPage from '@/components/reports/ReportPage.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiText from '@/components/ui/UiText.vue'
import { useReport } from '@/composables/useReport'
import { formatDate } from '@/lib/format'
import { reportApiPath } from '@/lib/apiPaths'
import { formatCurrencyCode } from '@/lib/money'
import { reportCountryName } from '@/lib/reportsCountry'
import { buildQuarterOptions, parseQuarterValue, quarterValue } from '@/lib/quarterOptions'
import { reportPath } from '@/lib/reportsQuery'
import { ossRowKey } from '@/lib/reportsRows'
import { ReportCellFormat } from '@/types/reportcolumn'
import type { ReportColumnExtra } from '@/types/reportcolumn'
import type { ReportOss, ReportOssRow } from '@/types/reportoss'
import type { ReportTableData } from '@/types/reporttabledata'
import { ReportFieldWidth } from '@/utils/ui/reportField'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'
import schema from '@/views/OssReportView.schema'

const { t, locale } = useI18n()

/** Number of calendar years, ending with the current one, whose quarters are on offer. */
const YEARS_ON_OFFER = 4

/** Width of the quarter filter. */
const FILTER_WIDTHS = { quarter: ReportFieldWidth.Lg }

/** How the cells of the schema columns are written: the rate as a percent, every amount in its row's currency. */
const COLUMN_EXTRAS: Readonly<Record<string, ReportColumnExtra>> = {
  vatRate: { format: ReportCellFormat.Percent },
  totalExclVat: { format: ReportCellFormat.Money, currencyKey: 'currency' },
  totalVatCollected: { format: ReportCellFormat.Money, currencyKey: 'currency' },
}

/** The moment the screen opened; the offered quarters count back from it. */
const now = new Date()

/** The quarter being reported as `<year>-<quarter>`; the current one at first. */
const quarter = ref(quarterValue(now.getFullYear(), Math.floor(now.getMonth() / 3) + 1))

/** Quarters of the last few years, newest first, grouped by year, the months named in the reader's language. */
const quarterOptions = computed(() => buildQuarterOptions(now, YEARS_ON_OFFER, locale.value))

const { data, load } = useReport<ReportOss>(() => {
  const { year, quarter: number } = parseQuarterValue(quarter.value)
  return reportPath(reportApiPath('oss'), { year, quarter: number })
})

/** The rows with the country named in the reader's language. */
const rows = computed(() =>
  (data.value?.rows ?? []).map(row => ({ ...row, countryName: reportCountryName(row.countryCode, locale.value) })),
)

/** The one currency every row is in, or null when the rows mix currencies (or there are none). */
const sharedCurrency = computed<string | null>(() => {
  const currencies = new Set(rows.value.map(row => row.currency))
  return currencies.size === 1 ? [...currencies][0] ?? null : null
})

/** Footer sums: the invoice count always, the money only when it is all in one currency. */
const totals = computed<Record<string, number>>(() => {
  const counted = { numberOfInvoices: rows.value.reduce((sum, row) => sum + row.numberOfInvoices, 0) }
  if (sharedCurrency.value === null) {
    return counted
  }
  return {
    ...counted,
    totalExclVat: rows.value.reduce((sum, row) => sum + row.totalExclVat, 0),
    totalVatCollected: rows.value.reduce((sum, row) => sum + row.totalVatCollected, 0),
  }
})

/**
 * Formats a footer amount in the currency every row shares.
 *
 * @param amount - The sum.
 * @returns The formatted sum.
 */
const formatTotal = (amount: number): string => formatCurrencyCode(amount, sharedCurrency.value ?? '')

/** The table of the member states, with the footer sums. */
const table = computed<ReportTableData<ReportOssRow & { countryName: string }>>(() => ({
  rows: rows.value,
  rowKey: ossRowKey,
  extras: COLUMN_EXTRAS,
  totals: totals.value,
  totalsLabel: t('reports.oss.totals'),
  formatMoney: formatTotal,
}))
</script>

<template>
  <ReportPage
    :retry="load"
    :schema="schema"
    :widths="FILTER_WIDTHS"
    :generate-label="t('shared.generate')"
    :table="table"
    @generate="load"
  >
    <template #field-quarter>
      <UiSelect v-model="quarter" :options="quarterOptions" :placeholder="t('reports.oss.selectQuarter')" data-testid="report-quarter" />
    </template>

    <template #summary>
      <UiText v-if="data" :tone="UiTextTone.Secondary" :size="UiTextSize.Md" data-testid="report-period-label">
        {{ t('reports.oss.period', { from: formatDate(data.periodStart), to: formatDate(data.periodEnd) }) }}
      </UiText>
    </template>

    <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Sm">
      {{ t('reports.oss.ifACountryDoesNot') }}
    </UiText>
  </ReportPage>
</template>
