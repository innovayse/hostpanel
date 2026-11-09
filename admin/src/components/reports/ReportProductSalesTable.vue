<script setup lang="ts">
/**
 * Table of units sold and value per product, grouped by product group with a sub total per group
 * and a grand total. Shared by the income by product and monthly orders reports.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import ReportGroupRow from '@/components/reports/ReportGroupRow.vue'
import ReportTable from '@/components/reports/ReportTable.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import { formatReportAmount } from '@/lib/money'
import { ReportCellFormat } from '@/types/reportcolumn'
import type { ReportColumn } from '@/types/reportcolumn'
import type { ReportSalesGroup } from '@/types/reportproductsales'

const { t } = useI18n()

/** Props for ReportProductSalesTable. */
const props = withDefaults(defineProps<{
  /** Product groups with their products. */
  groups: ReportSalesGroup[]
  /** Units sold overall. */
  totalUnitsSold: number
  /** Value overall, printed by `formatMoney`. */
  totalValue: number
  /** Formats a value cell; US dollars when omitted. */
  formatMoney?: (amount: number) => string
}>(), {
  /**
   * Default money formatter: US dollars.
   *
   * @param amount - Value to format.
   * @returns The formatted amount.
   */
  formatMoney: (amount: number) => formatReportAmount(amount),
})

/** Table columns. */
const columns = computed<ReportColumn[]>(() => [
  { key: 'productName', label: t('shared.productName') },
  { key: 'unitsSold', label: t('reports.monthlyOrders.unitsSold'), numeric: true },
  { key: 'value', label: t('shared.value'), format: ReportCellFormat.Money },
])

/** The grand total row, by column key. */
const totals = computed<Record<string, number>>(() => ({ unitsSold: props.totalUnitsSold, value: props.totalValue }))
</script>

<template>
  <ReportTable
    :columns="columns"
    :empty="groups.length === 0"
    :totals="totals"
    :format-money="formatMoney"
    testid="report-sales-table"
  >
    <template #body>
      <template v-for="group in groups" :key="group.groupName">
        <ReportGroupRow :label="group.groupName" :colspan="columns.length" />
        <UiTableRow v-for="product in group.products" :key="product.productId" hoverable>
          <UiTableCell>{{ product.productName }}</UiTableCell>
          <UiTableCell numeric>{{ product.unitsSold }}</UiTableCell>
          <UiTableCell numeric>{{ formatMoney(product.value) }}</UiTableCell>
        </UiTableRow>
        <UiTableRow>
          <UiTableCell muted>{{ t('shared.subTotal2') }}</UiTableCell>
          <UiTableCell numeric muted>{{ group.groupUnitsSold }}</UiTableCell>
          <UiTableCell numeric muted>{{ formatMoney(group.groupValue) }}</UiTableCell>
        </UiTableRow>
      </template>
    </template>
  </ReportTable>
</template>
