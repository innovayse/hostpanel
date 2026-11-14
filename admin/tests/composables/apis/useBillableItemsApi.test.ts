/**
 * Tests for the billable items API composable: the admin-wide and the client-scoped endpoints
 * through the real `useApi` transport with `fetch` stubbed.
 */

import { afterEach, describe, expect, it, vi } from 'vitest'
import { useBillableItemsApi } from '@/composables/apis/useBillableItemsApi'
import { ApiError } from '@/composables/useApi'
import { BillableItemInvoiced, BillableItemType, RecurringPeriod } from '@/types/billableitem'
import { json, problem, stubFetch, callOf } from '../../helpers/fetch'

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('useBillableItemsApi', () => {
  it('list drops the filters that are not set and sends the ones that are', async () => {
    const mock = stubFetch(json({ items: [], totalCount: 0 }))
    const api = useBillableItemsApi()

    await api.list(1, 20)
    await api.list(1, 20, { search: '', clientId: undefined, type: undefined, invoiced: undefined })
    await api.list(3, 20, { search: 'hosting', clientId: 7, type: BillableItemType.Recurring, invoiced: BillableItemInvoiced.No })
    expect(callOf(mock, 0).url).toBe('/api/billing/billable-items?page=1&pageSize=20')
    expect(callOf(mock, 1).url).toBe('/api/billing/billable-items?page=1&pageSize=20')
    expect(callOf(mock, 2).url).toBe(
      '/api/billing/billable-items?page=3&pageSize=20&search=hosting&clientId=7&type=Recurring&invoiced=false',
    )
  })

  it('create POSTs the payload and remove DELETEs by id', async () => {
    const mock = stubFetch(new Response(null, { status: 204 }))
    const api = useBillableItemsApi()
    const payload = { clientId: 1, description: 'x', amount: 2, currency: 'USD', type: BillableItemType.OneTime }

    await api.create(payload)
    await api.remove(4)
    expect(callOf(mock, 0)).toMatchObject({ url: '/api/billing/billable-items', method: 'POST', body: payload })
    expect(callOf(mock, 1)).toMatchObject({ url: '/api/billing/billable-items/4', method: 'DELETE' })
  })

  it('listByClient pages the invoiced items and sends the search and the type', async () => {
    const mock = stubFetch(json({ uninvoicedItems: [] }))
    const api = useBillableItemsApi()

    await api.listByClient('7', 2, 20)
    await api.listByClient('7', 1, 20, { search: 'hosting', type: BillableItemType.OneTime })
    expect(callOf(mock, 0).url).toBe('/api/clients/7/billable-items?page=2&pageSize=20')
    expect(callOf(mock, 1).url).toBe('/api/clients/7/billable-items?page=1&pageSize=20&search=hosting&type=OneTime')
  })

  it('invoiceSelected, removeForClient, addForClient and addTimeBilling use the client paths', async () => {
    const mock = stubFetch(new Response(null, { status: 204 }))
    const api = useBillableItemsApi()

    await api.invoiceSelected('7', [1, 2])
    await api.removeForClient('7', 3)
    await api.addForClient('7', {
      description: 'Backup',
      amount: 1,
      currency: 'USD',
      type: BillableItemType.Recurring,
      recurringPeriod: RecurringPeriod.Monthly,
    })
    await api.addTimeBilling('7', [{ hours: 2 }])
    expect(callOf(mock, 0)).toMatchObject({ url: '/api/clients/7/billable-items/invoice-selected', method: 'POST', body: { billableItemIds: [1, 2] } })
    expect(callOf(mock, 1)).toMatchObject({ url: '/api/clients/7/billable-items/3', method: 'DELETE' })
    expect(callOf(mock, 2)).toMatchObject({
      url: '/api/clients/7/billable-items',
      method: 'POST',
      body: { description: 'Backup', amount: 1, currency: 'USD', type: 'Recurring', recurringPeriod: 'monthly' },
    })
    expect(callOf(mock, 3)).toMatchObject({ url: '/api/clients/7/billable-items/time-billing', method: 'POST', body: { entries: [{ hours: 2 }] } })
  })

  it('listServices asks for up to 100 services of the client', async () => {
    const mock = stubFetch(json({ items: [], totalCount: 0 }))

    await useBillableItemsApi().listServices('7')
    expect(callOf(mock).url).toBe('/api/services?clientId=7&page=1&pageSize=100')
  })

  it('rejects with an ApiError carrying the refusal body', async () => {
    stubFetch(problem('Already invoiced.', 409))

    await expect(useBillableItemsApi().remove(4)).rejects.toBeInstanceOf(ApiError)
  })
})
