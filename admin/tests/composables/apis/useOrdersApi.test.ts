/**
 * Tests for the orders API composable: paths, verbs and query strings through the real `useApi`
 * transport with `fetch` stubbed.
 */

import { afterEach, describe, expect, it, vi } from 'vitest'
import { useOrdersApi } from '@/composables/apis/useOrdersApi'
import { OrderStatus } from '@/types/order'
import { json, stubFetch, callOf } from '../../helpers/fetch'

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('useOrdersApi', () => {
  it('listOrders sends paging and the status only when set', async () => {
    const mock = stubFetch(json({ items: [], totalCount: 0 }))
    const api = useOrdersApi()

    await api.listOrders({ page: 1, pageSize: 20, status: OrderStatus.Pending })
    await api.listOrders({ page: 1, pageSize: 20, status: null })
    expect(callOf(mock, 0).url).toBe('/api/orders?page=1&pageSize=20&status=Pending')
    expect(callOf(mock, 1).url).toBe('/api/orders?page=1&pageSize=20')
  })

  it('getOrder reads the detail', async () => {
    const mock = stubFetch(json({ id: 8 }))

    expect(await useOrdersApi().getOrder(8)).toEqual({ id: 8 })
    expect(callOf(mock).url).toBe('/api/orders/8')
  })

  it('retryOrderLine POSTs to the line retry route', async () => {
    const mock = stubFetch(json({ failedLines: [] }))

    expect(await useOrdersApi().retryOrderLine(8, 2)).toEqual({ failedLines: [] })
    expect(callOf(mock)).toMatchObject({ url: '/api/orders/8/items/2/retry', method: 'POST' })
  })

  it('closeOrderLineRefund POSTs to the line close-refund route', async () => {
    const mock = stubFetch(new Response(null, { status: 204 }))

    await useOrdersApi().closeOrderLineRefund(8, 2)
    expect(callOf(mock)).toMatchObject({ url: '/api/orders/8/items/2/close-refund', method: 'POST' })
  })

  it('accept and cancel POST, delete DELETEs', async () => {
    const mock = stubFetch(new Response(null, { status: 204 }))
    const api = useOrdersApi()

    await api.acceptOrder(8)
    await api.cancelOrder(8)
    await api.deleteOrder(8)
    expect(callOf(mock, 0)).toMatchObject({ url: '/api/orders/8/accept', method: 'POST' })
    expect(callOf(mock, 1)).toMatchObject({ url: '/api/orders/8/cancel', method: 'POST' })
    expect(callOf(mock, 2)).toMatchObject({ url: '/api/orders/8', method: 'DELETE' })
  })
})
