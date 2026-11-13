<script setup lang="ts">
/**
 * VAT MOSS report: the invoiced value and VAT collected per EU member state, VAT rate and invoice
 * currency for a quarter, net of the refunds made in it. Each amount is shown in its own currency;
 * the footer sums the money only when every row shares one currency.
 */
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import ReportField from '@/components/reports/ReportField.vue'
import ReportFilterBar from '@/components/reports/ReportFilterBar.vue'
import ReportPage from '@/components/reports/ReportPage.vue'
import ReportTable from '@/components/reports/ReportTable.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiText from '@/components/ui/UiText.vue'
import { useReport } from '@/composables/useReport'
import { formatDate } from '@/lib/format'
import { formatCurrencyCode } from '@/lib/money'
import { reportCountryName } from '@/lib/reportsCountry'
import { reportPath } from '@/lib/reportsQuery'
import { vatRowKey } from '@/lib/reportsRows'
import { ReportCellFormat } from '@/types/reportcolumn'
import type { ReportColumn } from '@/types/reportcolumn'
import type { ReportVat } from '@/types/reportvat'
import { UiAlign } from '@/utils/ui/table'

const { t, locale } = useI18n()

/** Number of calendar years, ending with the current one, whose quarters are on offer. */
const YEARS_ON_OFFER = 4

/** The moment the screen opened; the offered quarters count back from it. */
const now = new Date()

/** The quarter being reported as `<year>-<quarter>`; the current one at first. */
const quarter = ref(`${now.getFullYear()}-${Math.floor(now.getMonth() / 3) + 1}`)

/** Quarters of the last few years, newest first. */
const quarterOptions = computed(() => {
  const options: { value: string; label: string }[] = []
  for (let year = now.getFullYear(); year > now.getFullYear() - YEARS_ON_OFFER; year--) {
    for (let number = 4; number >= 1; number--) {
      options.push({
        value: `${year}-${number}`,
        label: t('reports.vatMoss.quarterOption', { year, quarter: number, months: t(`reports.vatMoss.quarter${number}`) }),
      })
    }
  }
  return options
})

const { data, load } = useReport<ReportVat>(() => {
  const [year = '', number = ''] = quarter.value.split('-')
  return reportPath('/reports/vat-moss', { year, quarter: number })
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

/** Table columns; every amount is written in its row's currency. */
const columns = computed<ReportColumn[]>(() => [
  { key: 'countryName', label: t('reports.vatMoss.countryName') },
  { key: 'countryCode', label: t('reports.vatMoss.countryCode') },
  { key: 'vatRate', label: t('reports.vatMoss.vatRate'), align: UiAlign.Right, format: ReportCellFormat.Percent },
  { key: 'numberOfInvoices', label: t('reports.vatMoss.numberOfInvoices'), align: UiAlign.Right },
  { key: 'totalExclVat', label: t('reports.vatMoss.totalValueInvoicedExclVat'), align: UiAlign.Right, format: ReportCellFormat.Money, currencyKey: 'currency' },
  { key: 'totalVatCollected', label: t('reports.vatMoss.totalVatCollected'), align: UiAlign.Right, format: ReportCellFormat.Money, currencyKey: 'currency' },
  { key: 'currency', label: t('shared.currency') },
])

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
</script>

<template>
  <ReportPage :retry="load" :title="t('reports.vatMoss.vatMossSettlementData')" :description="t('reports.vatMoss.thisReportProvidesTheInformation')">
    <template #filters>
      <ReportFilterBar :generate-label="t('shared.generateReport')" @generate="load">
        <template #fields>
          <ReportField :label="t('reports.vatMoss.selectQuarter')" width="lg">
            <UiSelect v-model="quarter" :options="quarterOptions" :placeholder="t('reports.vatMoss.selectQuarter')" data-testid="report-quarter" />
          </ReportField>
        </template>
      </ReportFilterBar>
    </template>

    <template v-if="data">
      <UiText tone="secondary" size="md" data-testid="report-period-label">
        {{ t('reports.vatMoss.period', { from: formatDate(data.periodStart), to: formatDate(data.periodEnd) }) }}
      </UiText>
      <ReportTable
        :columns="columns"
        :rows="rows"
        :row-key="vatRowKey"
        :totals="totals"
        :totals-label="t('reports.vatMoss.totals')"
        :format-money="formatTotal"
        :empty-text="t('shared.noDataFoundForThis2')"
      />
      <UiText tone="muted" size="sm">
        {{ t('reports.vatMoss.ifACountryDoesNot') }}
      </UiText>
    </template>
  </ReportPage>
</template>
