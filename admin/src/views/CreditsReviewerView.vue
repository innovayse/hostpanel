<script setup lang="ts">
/**
 * Credits reviewer: the credits issued to clients between two dates, optionally for one client and
 * within an amount range. The totals come one per currency; the credits are paged, newest first,
 * each in its own currency.
 */
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import ReportField from '@/components/reports/ReportField.vue'
import ReportFilterBar from '@/components/reports/ReportFilterBar.vue'
import ReportPage from '@/components/reports/ReportPage.vue'
import ReportTable from '@/components/reports/ReportTable.vue'
import UiClientSelect from '@/components/ui/UiClientSelect.vue'
import UiDateRangePicker from '@/components/ui/UiDateRangePicker.vue'
import UiPagination from '@/components/ui/UiPagination.vue'
import UiStepper from '@/components/ui/UiStepper.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { useReport } from '@/composables/useReport'
import { useReportClients } from '@/composables/useReportClients'
import { REPORT_PAGE_SIZE } from '@/lib/limits'
import { formatCurrencyCode } from '@/lib/money'
import { reportPath } from '@/lib/reportsQuery'
import { currentYearRange } from '@/lib/reportsPeriod'
import { ReportCellFormat } from '@/types/reportcolumn'
import type { ReportColumn } from '@/types/reportcolumn'
import type { ReportCredits } from '@/types/reportcredit'
import { UiAlign } from '@/utils/ui/table'

const { t } = useI18n()

const { clients } = useReportClients()

/** The client chosen in the picker; empty for all clients. */
const selectedClient = ref<string | number | null>('')

/** The period to review; the current year at first. */
const dateRange = ref<[string, string] | null>(currentYearRange())

/** Smallest credit to list; 0 means no lower limit. */
const minAmount = ref(0)

/** Largest credit to list; 0 means no upper limit. */
const maxAmount = ref(0)

/** Page of credits shown, starting at 1. */
const page = ref(1)

const { data, load } = useReport<ReportCredits>(() =>
  reportPath('/reports/credits-reviewer', {
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

/** Table columns; every amount is written in its own currency. */
const columns = computed<ReportColumn[]>(() => [
  { key: 'id', label: t('reports.creditsReviewer.creditId') },
  { key: 'clientId', label: t('reports.creditsReviewer.clientId') },
  { key: 'clientName', label: t('shared.clientName') },
  { key: 'date', label: t('shared.date'), align: UiAlign.Center, format: ReportCellFormat.Date },
  { key: 'description', label: t('shared.description') },
  { key: 'currency', label: t('shared.currency') },
  { key: 'amount', label: t('shared.amount'), align: UiAlign.Right, format: ReportCellFormat.Money, currencyKey: 'currency' },
])
</script>

<template>
  <ReportPage :retry="load" :title="t('reports.creditsReviewer.creditsReviewer')" :description="t('reports.creditsReviewer.thisReportAllowsYouTo')">
    <template #filters>
      <ReportFilterBar @generate="generate">
        <template #fields>
          <ReportField :label="t('shared.client')" width="grow">
            <UiClientSelect v-model="selectedClient" :clients="clients" :placeholder="t('reports.creditsReviewer.allClients')" />
          </ReportField>
          <ReportField :label="t('shared.dateRange')" width="grow">
            <UiDateRangePicker v-model="dateRange" :placeholder="t('shared.selectDateRange')" />
          </ReportField>
          <ReportField :label="t('reports.creditsReviewer.minAmount')">
            <UiStepper v-model="minAmount" :step="1" :min="0" :placeholder="t('shared.any')" />
          </ReportField>
          <ReportField :label="t('reports.creditsReviewer.maxAmount')">
            <UiStepper v-model="maxAmount" :step="1" :min="0" :placeholder="t('shared.any')" />
          </ReportField>
        </template>
      </ReportFilterBar>
    </template>

    <template v-if="data">
      <UiStack gap="xs" data-testid="report-credits-summary">
        <UiText v-for="total in data.totals" :key="total.currency" tone="secondary" size="md">
          {{
            t('reports.creditsReviewer.currencySummary', {
              currency: total.currency,
              count: total.totalCount,
              amount: formatCurrencyCode(total.totalAmount, total.currency),
            })
          }}
        </UiText>
      </UiStack>
      <ReportTable
        :columns="columns"
        :rows="data.rows.items"
        row-key="id"
        :empty-text="t('reports.creditsReviewer.noCreditsFoundForThis')"
      />
      <UiPagination
        :page="page"
        :total="data.rows.totalCount"
        :page-size="REPORT_PAGE_SIZE"
        testid="report-credits-pagination"
        @update:page="goToPage"
      />
    </template>
  </ReportPage>
</template>
