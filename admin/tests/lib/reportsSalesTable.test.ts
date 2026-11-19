/**
 * Tests of the product sales table the income by product and monthly orders reports share: both schemas hold the very
 * same table, with the columns the sales table component draws.
 */
import { describe, expect, it } from 'vitest'
import { PRODUCT_SALES_TABLE } from '@/lib/reportsSalesTable'
import incomeByProduct from '@/views/IncomeByProductView.schema'
import monthlyOrders from '@/views/MonthlyOrdersView.schema'

describe('PRODUCT_SALES_TABLE', () => {
  it('names the product, the units sold and the value', () => {
    expect(PRODUCT_SALES_TABLE.columns.map(column => column.key)).toEqual(['productName', 'unitsSold', 'value'])
  })

  it('is the table of both reports that draw it', () => {
    expect(incomeByProduct.table).toBe(PRODUCT_SALES_TABLE)
    expect(monthlyOrders.table).toBe(PRODUCT_SALES_TABLE)
  })
})
