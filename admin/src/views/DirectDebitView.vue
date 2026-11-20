<script setup lang="ts">
/**
 * Direct debit report: every unpaid invoice assigned to the direct debit payment method, with the
 * client bank details. The table is drawn while the report loads (its own skeleton rows), so the screen
 * never shows an empty page before the first answer.
 */
import { computed } from 'vue'
import ReportPage from '@/components/reports/ReportPage.vue'
import { useReport } from '@/composables/useReport'
import { reportApiPath } from '@/lib/apiPaths'
import { ReportCellFormat } from '@/types/reportcolumn'
import type { ReportDirectDebit, ReportDirectDebitRow } from '@/types/reportdirectdebit'
import type { ReportTableData } from '@/types/reporttabledata'
import schema from '@/views/DirectDebitView.schema'

const { data, load } = useReport<ReportDirectDebit>(() => reportApiPath('direct-debit'))

/** The unpaid invoices, with the date and money columns formatted; none until the report is read. */
const table = computed<ReportTableData<ReportDirectDebitRow>>(() => ({
  rows: data.value?.rows ?? [],
  rowKey: 'invoiceId',
  extras: {
    invoiceDate: { format: ReportCellFormat.Date },
    dueDate: { format: ReportCellFormat.Date },
    subtotal: { format: ReportCellFormat.Money },
    tax: { format: ReportCellFormat.Money },
    credit: { format: ReportCellFormat.Money },
    total: { format: ReportCellFormat.Money },
  },
}))
</script>

<template>
  <ReportPage :retry="load" :schema="schema" :table="table" />
</template>
