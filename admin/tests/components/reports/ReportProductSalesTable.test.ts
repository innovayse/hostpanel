/**
 * Tests for ReportProductSalesTable: the columns of the shared schema table, group headings, product rows, sub
 * totals and the grand total.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import ReportProductSalesTable from '@/components/reports/ReportProductSalesTable.vue'
import type { ReportSalesGroup } from '@/types/reportproductsales'
import { createTestI18n } from '../../helpers/i18n'

/** One group with two products. */
const groups: ReportSalesGroup[] = [
  {
    groupName: 'Hosting',
    groupUnitsSold: 5,
    groupValue: 50,
    products: [
      { productId: 1, productName: 'Basic', unitsSold: 2, value: 20 },
      { productId: 2, productName: 'Plus', unitsSold: 3, value: 30 },
    ],
  },
]

/**
 * Mounts the table.
 *
 * @param props - Props on top of the totals.
 * @returns The wrapper.
 */
const mountTable = (props: Record<string, unknown> = {}) =>
  mount(ReportProductSalesTable, {
    props: { groups, totalUnitsSold: 5, totalValue: 50, ...props },
    global: { plugins: [createTestI18n()] },
  })

describe('ReportProductSalesTable', () => {
  it('shows the group, its products, the sub total and the grand total', () => {
    const text = mountTable().text()

    expect(text).toContain('Hosting')
    expect(text).toContain('Basic')
    expect(text).toContain('$30.00')
    expect(text).toContain('Sub Total')
    expect(text).toContain('Total')
  })

  it('captions the columns with the labels of the schema table', () => {
    const headers = mountTable().findAll('th').map(header => header.text())

    expect(headers).toEqual(['Product Name', 'Units Sold', 'Value'])
  })

  it('formats values with the function it is given', () => {
    expect(mountTable({ formatMoney: (amount: number) => `${amount} AMD` }).text()).toContain('20 AMD')
  })

  it('shows the empty text when there are no groups', () => {
    const wrapper = mountTable({ groups: [] })

    expect(wrapper.text()).toContain('No data for this report.')
    expect(wrapper.find('[data-testid="report-totals"]').exists()).toBe(false)
  })
})
