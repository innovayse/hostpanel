<script setup lang="ts">
/**
 * Product suspensions report: every suspended service with the reason recorded for it. The title, the refresh
 * action and the table columns are the ones of its schema, which the skeleton draws too.
 */
import { computed } from 'vue'
import ReportPage from '@/components/reports/ReportPage.vue'
import { useReport } from '@/composables/useReport'
import { ReportCellFormat } from '@/types/reportcolumn'
import type { ReportSuspensionRow } from '@/types/reportsuspension'
import type { ReportTableData } from '@/types/reporttabledata'
import schema from '@/views/ProductSuspensionsView.schema'

const { data, load } = useReport<ReportSuspensionRow[]>(() => '/reports/product-suspensions')

/** The suspended services, the id and the next due date formatted. */
const table = computed<ReportTableData<ReportSuspensionRow>>(() => ({
  rows: data.value ?? [],
  rowKey: 'serviceId',
  extras: {
    serviceId: { format: ReportCellFormat.Id },
    nextDueDate: { format: ReportCellFormat.Date },
  },
}))
</script>

<template>
  <ReportPage :retry="load" :schema="schema" :table="table" />
</template>
