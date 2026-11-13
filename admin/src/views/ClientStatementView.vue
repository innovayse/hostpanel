<script setup lang="ts">
/**
 * Client statement: the entries, debits, credits and running balance of one client account.
 */
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import ReportField from '@/components/reports/ReportField.vue'
import ReportActions from '@/components/reports/ReportActions.vue'
import ReportFilterBar from '@/components/reports/ReportFilterBar.vue'
import ReportPage from '@/components/reports/ReportPage.vue'
import ReportTable from '@/components/reports/ReportTable.vue'
import UiClientSelect from '@/components/ui/UiClientSelect.vue'
import UiDateRangePicker from '@/components/ui/UiDateRangePicker.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { useReport } from '@/composables/useReport'
import { useReportClients } from '@/composables/useReportClients'
import { formatCurrencyCode } from '@/lib/money'
import { reportPath } from '@/lib/reportsQuery'
import { statementLineKey } from '@/lib/reportsRows'
import { ReportStatementLineType } from '@/types/reportclientstatement'
import type { ReportClientStatement, ReportStatementLine } from '@/types/reportclientstatement'
import { ReportCellFormat } from '@/types/reportcolumn'
import type { ReportColumn } from '@/types/reportcolumn'
import { UiAlign } from '@/utils/ui/table'

const { t } = useI18n()

const { clients } = useReportClients()

/** The client chosen in the picker; empty until chosen. */
const selectedClient = ref<string | number | null>('')

/** The date range chosen, or none for the whole account. */
const dateRange = ref<[string, string] | null>(null)

const { data, load } = useReport<ReportClientStatement>(
  () => reportPath('/reports/client-statement', {
    clientId: selectedClient.value,
    from: dateRange.value?.[0],
    to: dateRange.value?.[1],
  }),
  false,
)

/** Loads the statement once a client is chosen. */
const generate = async (): Promise<void> => {
  if (selectedClient.value) {
    await load()
  }
}

/** Opens the print dialog. */
const print = (): void => {
  window.print()
}

/** Statement columns. */
const columns = computed<ReportColumn[]>(() => [
  { key: 'type', label: t('shared.type'), format: ReportCellFormat.Status },
  { key: 'date', label: t('shared.date') },
  { key: 'description', label: t('shared.description') },
  { key: 'currency', label: t('shared.currency') },
  { key: 'debit', label: t('reports.clientStatement.debit'), align: UiAlign.Right, format: ReportCellFormat.Money, currencyKey: 'currency' },
  { key: 'credit', label: t('reports.directDebit.credit'), align: UiAlign.Right, format: ReportCellFormat.Money, currencyKey: 'currency' },
  { key: 'balance', label: t('shared.balance'), align: UiAlign.Right, format: ReportCellFormat.Money, currencyKey: 'currency' },
])

/**
 * Text of a statement line's description cell.
 *
 * @param line - The line.
 * @returns The transaction description as sent, or the sentence for an invoice or a history payment.
 */
const describeLine = (line: ReportStatementLine): string => {
  if (line.type === ReportStatementLineType.PaidHistory) {
    return t('reports.clientStatement.paidHistory', { id: line.invoiceId })
  }
  if (line.invoiceId === null) {
    return line.description ?? ''
  }
  return t('reports.clientStatement.invoicePayment', { id: line.invoiceId })
}
</script>

<template>
  <ReportPage
    :title="t('reports.clientStatement.clientAccountRegisterBalance')"
    :description="t('reports.clientStatement.thisReportProvidesAStatement')"
    :retry="generate"
  >
    <template #actions>
      <ReportActions printable @print="print" />
    </template>

    <template #filters>
      <ReportFilterBar @generate="generate">
        <template #fields>
          <ReportField :label="t('reports.clientStatement.selectClient')" width="grow">
            <UiClientSelect v-model="selectedClient" :clients="clients" :placeholder="t('shared.selectAClient')" />
          </ReportField>
          <ReportField :label="t('shared.dateRange')" width="grow">
            <UiDateRangePicker v-model="dateRange" :placeholder="t('shared.selectDateRange')" />
          </ReportField>
        </template>
      </ReportFilterBar>
    </template>

    <template v-if="data">
      <UiStack gap="xs" data-testid="report-previous-balance">
        <UiText v-for="balance in data.balances" :key="balance.currency" tone="secondary" size="md" class="text-right">
          {{ t('reports.clientStatement.previousBalance') }} {{ formatCurrencyCode(balance.previousBalance, balance.currency) }}
        </UiText>
      </UiStack>
      <ReportTable
        :columns="columns"
        :rows="data.lines"
        :row-key="statementLineKey"
        :empty-text="t('reports.clientStatement.noEntriesForThisPeriod')"
      >
        <template #cell-description="{ row }">
          {{ describeLine(row) }}
        </template>
      </ReportTable>
      <UiStack gap="xs" data-testid="report-ending-balance">
        <UiText v-for="balance in data.balances" :key="balance.currency" tone="secondary" size="md" class="text-right">
          {{ t('reports.clientStatement.endingBalance') }} {{ formatCurrencyCode(balance.endingBalance, balance.currency) }}
        </UiText>
      </UiStack>
    </template>
  </ReportPage>
</template>
