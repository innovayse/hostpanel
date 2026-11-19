<script setup lang="ts">
/**
 * Client statement: the entries, debits, credits and running balance of one client account. The table is drawn
 * before a statement is asked for too (with a prompt to choose a client), so the screen has the blocks of its skeleton;
 * the opening and closing balance lines are parts of the schema, with a placeholder while the statement loads.
 */
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import ReportPage from '@/components/reports/ReportPage.vue'
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
import type { ReportClientStatement, ReportStatementBalance, ReportStatementLine } from '@/types/reportclientstatement'
import { ReportCellFormat } from '@/types/reportcolumn'
import type { ReportTableData } from '@/types/reporttabledata'
import { ReportFieldWidth } from '@/utils/ui/reportField'
import schema from '@/views/ClientStatementView.schema'

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

/** The two pickers take the free width of the filter row. */
const WIDTHS = { client: ReportFieldWidth.Grow, dateRange: ReportFieldWidth.Grow } as const

/** Opening and closing balance per currency; none until a statement is read. */
const balances = computed<ReportStatementBalance[]>(() => data.value?.balances ?? [])

/** Sentence of the empty report: the prompt to choose a client until a statement is read, then the sentence for a period with no entries. */
const emptyText = computed<string>(() => (data.value === null ? t('shared.selectAClient') : t('reports.clientStatement.noEntriesForThisPeriod')))

/** The statement lines; the cell formats are the schema columns', and the money columns are in each line's own currency. */
const table = computed<ReportTableData<ReportStatementLine>>(() => ({
  rows: data.value?.lines ?? [],
  rowKey: statementLineKey,
  extras: {
    type: { format: ReportCellFormat.Status },
    date: { format: ReportCellFormat.Date },
    debit: { format: ReportCellFormat.Money, currencyKey: 'currency' },
    credit: { format: ReportCellFormat.Money, currencyKey: 'currency' },
    balance: { format: ReportCellFormat.Money, currencyKey: 'currency' },
  },
}))

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
  <ReportPage :schema="schema" :empty-text="emptyText" :retry="generate" :widths="WIDTHS" :table="table" @generate="generate" @print="print">
    <template #field-client>
      <UiClientSelect v-model="selectedClient" :clients="clients" :placeholder="t('shared.selectAClient')" />
    </template>
    <template #field-dateRange>
      <UiDateRangePicker v-model="dateRange" :placeholder="t('shared.selectDateRange')" />
    </template>

    <template #summary>
      <UiStack gap="xs" data-testid="report-previous-balance">
        <UiText v-for="balance in balances" :key="balance.currency" tone="secondary" size="md" class="text-right">
          {{ t('reports.clientStatement.previousBalance') }} {{ formatCurrencyCode(balance.previousBalance, balance.currency) }}
        </UiText>
      </UiStack>
    </template>

    <template #cell-description="{ row }">
      {{ describeLine(row) }}
    </template>

    <template #closing>
      <UiStack gap="xs" data-testid="report-ending-balance">
        <UiText v-for="balance in balances" :key="balance.currency" tone="secondary" size="md" class="text-right">
          {{ t('reports.clientStatement.endingBalance') }} {{ formatCurrencyCode(balance.endingBalance, balance.currency) }}
        </UiText>
      </UiStack>
    </template>
  </ReportPage>
</template>
