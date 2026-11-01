/**
 * Mount tests for TransactionChart: it hands the kit chart thirty daily labels and the net
 * revenue of each day (money in, less money out, less fees).
 */
import { describe, expect, it } from 'vitest'
import TransactionChart from '@/components/billing/TransactionChart.vue'
import UiChart from '@/components/ui/UiChart.vue'
import { transactionFixture } from '../../helpers/billingFixtures'
import { mountWithApp } from '../../helpers/mount'

describe('TransactionChart', () => {
  it('plots thirty days with today holding the net of its transactions', () => {
    const today = new Date().toISOString()
    const wrapper = mountWithApp(TransactionChart, {
      props: { transactions: [transactionFixture({ date: today, amountIn: 120, amountOut: 0, fees: 3.5 })] },
      stubs: { UiChart: true },
    })

    const chart = wrapper.findComponent(UiChart)
    expect(chart.props('labels')).toHaveLength(30)
    const [series] = chart.props('series') as { data: number[] }[]
    expect(series?.data).toHaveLength(30)
    expect(series?.data.at(-1)).toBe(116.5)
    expect(series?.data[0]).toBe(0)
  })

  it('ignores transactions with an unreadable date', () => {
    const wrapper = mountWithApp(TransactionChart, {
      props: { transactions: [transactionFixture({ date: 'not a date' })] },
      stubs: { UiChart: true },
    })

    const [series] = wrapper.findComponent(UiChart).props('series') as { data: number[] }[]
    expect(series?.data.every((value) => value === 0)).toBe(true)
  })

  it('writes the day captions in the interface language, not the browser language', () => {
    const wrapper = mountWithApp(TransactionChart, { props: { transactions: [] }, stubs: { UiChart: true } })

    const labels = wrapper.findComponent(UiChart).props('labels') as string[]
    expect(labels.every((label) => /^[A-Za-z]{3} \d{1,2}$/.test(label))).toBe(true)
  })
})
