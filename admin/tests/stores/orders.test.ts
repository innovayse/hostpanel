/**
 * Tests for the orders store: loads fill state and put the API's sentence into `error`, the
 * lifecycle commands report the outcome as a value and put a refusal into `actionError`, and a
 * successful command refreshes what the page shows. The requests go through the real `useApi`
 * transport.
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { translate } from '@/lib/translate'
import { useOrdersStore } from '@/stores/orders'
import { OrderStatus } from '@/types/order'
import { json, problem, stubFetch, stubOffline, callOf } from '../helpers/fetch'

describe('ordersStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('fetchAll sends the status filter and fills the list', async () => {
    const mock = stubFetch(json({ items: [{ id: 1 }], totalCount: 1 }))
    const store = useOrdersStore()
    store.statusFilter = OrderStatus.Pending

    expect(await store.fetchAll()).toBe(true)
    expect(callOf(mock).url).toBe('/api/orders?page=1&pageSize=20&status=Pending')
    expect(store.orders).toEqual([{ id: 1 }])
    expect(store.totalCount).toBe(1)
    expect(store.loading).toBe(false)
  })

  it('fetchAll reports the API sentence, and the no-answer sentence offline', async () => {
    const store = useOrdersStore()
    stubFetch(problem('Not allowed.', 403))
    expect(await store.fetchAll()).toBe(false)
    expect(store.error).toBe('Not allowed.')

    stubOffline()
    expect(await store.fetchAll()).toBe(false)
    expect(store.error).toBe(translate('errors.noAnswer'))
  })

  it('fetchOne stores the detail and reports a refusal', async () => {
    const store = useOrdersStore()
    stubFetch(json({ id: 8 }))
    expect(await store.fetchOne(8)).toBe(true)
    expect(store.currentOrder).toEqual({ id: 8 })

    stubFetch(problem('No such order.', 404))
    expect(await store.fetchOne(9)).toBe(false)
    expect(store.error).toBe('No such order.')
  })

  it('acceptOrder and cancelOrder refresh the order detail on success', async () => {
    const store = useOrdersStore()
    const mock = stubFetch(json({ failedLines: [] }), json({ id: 8, status: 'Active' }))

    expect(await store.acceptOrder(8)).toBe(true)
    expect(store.acceptFailures).toEqual([])
    expect(callOf(mock, 0)).toMatchObject({ url: '/api/orders/8/accept', method: 'POST' })
    expect(callOf(mock, 1).url).toBe('/api/orders/8')
    expect(store.currentOrder).toEqual({ id: 8, status: 'Active' })

    const cancel = stubFetch(json({}), json({ id: 8, status: 'Cancelled' }))
    expect(await store.cancelOrder(8)).toBe(true)
    expect(callOf(cancel, 0)).toMatchObject({ url: '/api/orders/8/cancel', method: 'POST' })
    expect(store.currentOrder).toEqual({ id: 8, status: 'Cancelled' })
  })

  it('acceptOrder keeps the lines it could not fulfil and names them', async () => {
    const store = useOrdersStore()
    const failedLines = [
      { itemId: 1, kind: 'Domain', productName: 'Domain', domain: 'taken.example', firstPaymentAmount: 10, errorCode: null, reason: 'Taken.' },
      { itemId: 2, kind: 'Service', productName: 'Starter Hosting', domain: null, firstPaymentAmount: 5, errorCode: 'PRODUCT_NOT_FOUND', reason: 'Gone.' },
    ]
    stubFetch(json({ failedLines }), json({ id: 8, status: 'Active' }))

    expect(await store.acceptOrder(8)).toBe(true)
    expect(store.acceptFailures).toEqual(failedLines)
    expect(store.acceptFailureNames).toBe('taken.example, Starter Hosting')
  })

  it('retryOrderLine keeps the lines still failed and refreshes the detail', async () => {
    const store = useOrdersStore()
    const failedLines = [
      { itemId: 2, kind: 'Domain', productName: 'Domain', domain: 'taken.example', firstPaymentAmount: 10, errorCode: null, reason: 'Taken.' },
    ]
    const mock = stubFetch(json({ failedLines }), json({ id: 8, status: 'Incomplete' }))

    expect(await store.retryOrderLine(8, 2)).toBe(true)
    expect(callOf(mock, 0)).toMatchObject({ url: '/api/orders/8/items/2/retry', method: 'POST' })
    expect(store.acceptFailures).toEqual(failedLines)
    expect(store.currentOrder).toEqual({ id: 8, status: 'Incomplete' })
  })

  it('a refused retry sets actionError and still reads the order again', async () => {
    const store = useOrdersStore()
    const mock = stubFetch(problem('This order line cannot be retried now.', 409), json({ id: 8, status: 'Active' }))

    expect(await store.retryOrderLine(8, 2)).toBe(false)
    expect(store.actionError).toBe('This order line cannot be retried now.')
    expect(mock).toHaveBeenCalledTimes(2)
    expect(store.currentOrder).toEqual({ id: 8, status: 'Active' })
  })

  it('a refused command returns false, sets actionError and does not refresh', async () => {
    const store = useOrdersStore()
    const mock = stubFetch(problem('Order is already accepted.', 409))

    expect(await store.acceptOrder(8)).toBe(false)
    expect(store.actionError).toBe('Order is already accepted.')
    expect(store.error).toBeNull()
    expect(mock).toHaveBeenCalledTimes(1)
  })

  it('deleteOrder refreshes the list on success and clears actionError', async () => {
    const store = useOrdersStore()
    stubFetch(problem('Locked.', 409))
    expect(await store.deleteOrder(8)).toBe(false)
    expect(store.actionError).toBe('Locked.')

    const mock = stubFetch(new Response(null, { status: 204 }), json({ items: [], totalCount: 0 }))
    expect(await store.deleteOrder(8)).toBe(true)
    expect(callOf(mock, 0)).toMatchObject({ url: '/api/orders/8', method: 'DELETE' })
    expect(callOf(mock, 1).url).toBe('/api/orders?page=1&pageSize=20')
    expect(store.actionError).toBeNull()
  })
})
