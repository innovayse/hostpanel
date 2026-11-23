<script setup lang="ts">
/**
 * Credits reviewer: the credits issued to clients between two dates, optionally for one client and
 * within an amount range. The totals come one per currency (the summary lines of the schema); the credits are
 * paged, newest first, each in its own currency.
 */
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import ReportPage from '@/components/reports/ReportPage.vue'
import UiClientSelect from '@/components/ui/UiClientSelect.vue'
import UiDateRangePicker from '@/components/ui/UiDateRangePicker.vue'
import UiStepper from '@/components/ui/UiStepper.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { useReport } from '@/composables/useReport'
import { useReportClients } from '@/composables/useReportClients'
import { REPORT_PAGE_SIZE } from '@/lib/limits'
import { reportApiPath } from '@/lib/apiPaths'
import { formatCurrencyCode } from '@/lib/money'
import { reportPath } from '@/lib/reportsQuery'
import { ReportCellFormat } from '@/types/reportcolumn'
import type { ReportCreditRow, ReportCredits, ReportCreditTotal } from '@/types/reportcredit'
import type { ReportTableData } from '@/types/reporttabledata'
import { DateRangePreset, presetIsoRange } from '@/utils/ui/datePicker'
import { ReportFieldWidth } from '@/utils/ui/reportField'
import { UiGap } from '@/utils/ui/layout'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'
import schema from '@/views/CreditsReviewerView.schema'

const { t } = useI18n()

const { clients } = useReportClients()

/** The client chosen in the picker; empty for all clients. */
const selectedClient = ref<string | number | null>('')

/** The period to review; the current year at first. */
const dateRange = ref<[string, string] | null>(presetIsoRange(DateRangePreset.ThisYear))

/** Smallest credit to list; 0 means no lower limit. */
const minAmount = ref(0)

/** Largest credit to list; 0 means no upper limit. */
const maxAmount = ref(0)

/** Page of credits shown, starting at 1. */
const page = ref(1)

const { data, load } = useReport<ReportCredits>(() =>
  reportPath(reportApiPath('credits-reviewer'), {
    clientId: selectedClient.value,
    from: dateRange.value?.[0],
    to: dateRange.value?.[1],
    minAmount: minAmount.value > 0 ? minAmount.value : undefined,
    maxAmount: maxAmount.value > 0 ? maxAmount.value : undefined,
    page: page.value,
    pageSize: REPORT_PAGE_SIZE,
  }),
)

/** Runs the report for the chosen filters from its first page. */
const generate = async (): Promise<void> => {
  page.value = 1
  await load()
}

/**
 * Shows another page of credits.
 *
 * @param next - The page to show.
 */
const goToPage = async (next: number): Promise<void> => {
  page.value = next
  await load()
}

/** The client and the date range take the free width of the filter row. */
const WIDTHS = { client: ReportFieldWidth.Grow, dateRange: ReportFieldWidth.Grow } as const

/** Totals per currency; none until the report is read. */
const totals = computed<ReportCreditTotal[]>(() => data.value?.totals ?? [])

/** The credits of the page shown, each amount written in its own currency, with the pager over every page. */
const table = computed<ReportTableData<ReportCreditRow>>(() => ({
  rows: data.value?.rows.items ?? [],
  rowKey: 'id',
  extras: {
    date: { format: ReportCellFormat.Date },
    amount: { format: ReportCellFormat.Money, currencyKey: 'currency' },
  },
  pagination: { page: page.value, total: data.value?.rows.totalCount ?? 0, pageSize: REPORT_PAGE_SIZE, testid: 'report-credits-pagination' },
}))
</script>

<template>
  <ReportPage :retry="load" :schema="schema" :widths="WIDTHS" :table="table" @generate="generate" @page="goToPage">
    <template #field-client>
      <UiClientSelect v-model="selectedClient" :clients="clients" :placeholder="t('reports.creditsReviewer.allClients')" />
    </template>
    <template #field-dateRange>
      <UiDateRangePicker v-model="dateRange" :placeholder="t('shared.selectDateRange')" />
    </template>
    <template #field-minAmount>
      <UiStepper v-model="minAmount" :step="1" :min="0" :placeholder="t('shared.any')" />
    </template>
    <template #field-maxAmount>
      <UiStepper v-model="maxAmount" :step="1" :min="0" :placeholder="t('shared.any')" />
    </template>

    <template #summary>
      <UiStack :gap="UiGap.Xs" data-testid="report-credits-summary">
        <UiText v-for="total in totals" :key="total.currency" :tone="UiTextTone.Secondary" :size="UiTextSize.Md">
          {{
            t('reports.creditsReviewer.currencySummary', {
              currency: total.currency,
              count: total.totalCount,
              amount: formatCurrencyCode(total.totalAmount, total.currency),
            })
          }}
        </UiText>
      </UiStack>
    </template>
  </ReportPage>
</template>
