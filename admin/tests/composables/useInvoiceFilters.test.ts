/**
 * Tests for the invoice filters every invoice list shares: the address is read back as the typed status and the period
 * the API takes, a value that is not a status or a date is ignored, and each applied filter is described for its chip.
 */
import { describe, expect, it } from 'vitest'
import { defineComponent, h } from 'vue'
import { flushPromises, mount } from '@vue/test-utils'
import { createMemoryHistory, createRouter } from 'vue-router'
import { InvoiceFilterKey, useInvoiceFilters } from '@/composables/useInvoiceFilters'
import type { InvoiceFilterSet } from '@/composables/useInvoiceFilters'
import { useListQuery } from '@/composables/useListQuery'
import { InvoiceStatus } from '@/types/invoice'
import { createTestI18n } from '../helpers/i18n'

/**
 * Mounts a screen that uses the composables, at the given address.
 *
 * @param url - The address the router starts at.
 * @returns A reader of the invoice filters the screen holds.
 */
const setup = async (url: string) => {
  let state: InvoiceFilterSet | null = null
  const Screen = defineComponent({
    setup() {
      const list = useListQuery(Object.values(InvoiceFilterKey))
      state = useInvoiceFilters(list)
      return () => h('div')
    },
  })
  const router = createRouter({ history: createMemoryHistory(), routes: [{ path: '/invoices', component: Screen }] })
  await router.push(url)
  await router.isReady()
  mount({ render: () => h('div', [h(Screen)]) }, { global: { plugins: [router, createTestI18n()] } })
  await flushPromises()
  return () => state as unknown as InvoiceFilterSet
}

describe('useInvoiceFilters', () => {
  it('reads the status and the period from the address as the API takes them', async () => {
    const filters = await setup('/invoices?status=Overdue&from=2026-10-01&to=2026-10-31')

    expect(filters().filters.value).toEqual({
      status: InvoiceStatus.Overdue,
      from: '2026-10-01',
      to: '2026-10-31',
    })
  })

  it('leaves out a filter that is not set or holds a value that is not a status or a date', async () => {
    const filters = await setup('/invoices?status=Gone&from=yesterday&to=')

    expect(filters().filters.value).toEqual({ status: undefined, from: undefined, to: undefined })
  })

  it('describes each applied filter for its chip', async () => {
    const filters = await setup('/invoices?status=Paid&from=2026-10-01&to=2026-10-31')

    expect(filters().chipSources()).toEqual([
      { key: 'status', label: 'Status', value: 'Paid' },
      { key: 'from', label: 'Date from', value: '01/10/2026' },
      { key: 'to', label: 'Date to', value: '31/10/2026' },
    ])
  })

  it('names the statuses that are not in the short list too', async () => {
    const filters = await setup('/invoices?status=PaymentPending')

    expect(filters().chipSources()[0]?.value).toBe('Payment Pending')
  })

  it('describes a filter that is not set without a value', async () => {
    const filters = await setup('/invoices')

    expect(filters().chipSources().every((source) => source.value === undefined)).toBe(true)
  })
})
