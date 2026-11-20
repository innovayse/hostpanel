/**
 * The table of units sold and value per product that two reports share.
 *
 * The income by product and monthly orders reports draw the same grouped table (`ReportProductSalesTable`), so their
 * schemas name the same columns, and the same group heading rows (`grouped`). The columns live here once, as pure data: both `views/*.schema.ts` modules point at
 * them and the table component reads the very list its screen got from its schema.
 */
import type { SkeletonTable } from '@/utils/ui/skeletonSchema'
import { UiAlign, UiColumnSize } from '@/utils/ui/table'

/** Columns of the product sales table: the product, the units sold and the value. */
export const PRODUCT_SALES_TABLE: SkeletonTable = {
  columns: [
    { key: 'productName', label: 'shared.productName', size: UiColumnSize.Grow },
    { key: 'unitsSold', label: 'reports.monthlyOrders.unitsSold', align: UiAlign.Right },
    { key: 'value', label: 'shared.value', align: UiAlign.Right },
  ],
  grouped: true,
}
