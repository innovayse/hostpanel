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

  it('fetchAll sends the page and the status and fills the list', async () => {
    const mock = stubFetch(json({ items: [{ id: 1 }], totalCount: 1 }))
    const store = useOrdersStore()

    expect(await store.fetchAll(2, OrderStatus.Pending)).toBe(true)
    expect(callOf(mock).url).toBe('/api/orders?page=2&pageSize=20&status=Pending')
    expect(store.orders).toEqual([{ id: 1 }])
    expect(store.totalCount).toBe(1)
    expect(store.loading).toBe(false)
  })

  it('fetchAll reports the API sentence, and the no-answer sentence offline', async () => {
    const store = useOrdersStore()
    stubFetch(problem('Not allowed.', 403))
    expect(await store.fetchAll(1, null)).toBe(false)
    expect(store.error).toBe('Not allowed.')

    stubOffline()
    expect(await store.fetchAll(1, null)).toBe(false)
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
    expect(store.unfulfilledLines).toEqual([])
    expect(callOf(mock, 0)).toMatchObject({ url: '/api/orders/8/accept', method: 'POST' })
    expect(callOf(mock, 1).url).toBe('/api/orders/8')
    expect(store.currentOrder).toEqual({ id: 8, status: 'Active' })

    const cancel = stubFetch(json({}), json({ id: 8, status: 'Cancelled' }))
    expect(await store.cancelOrder(8)).toBe(true)
    expect(callOf(cancel, 0)).toMatchObject({ url: '/api/orders/8/cancel', method: 'POST' })
    expect(store.currentOrder).toEqual({ id: 8, status: 'Cancelled' })
  })

  it('approveReview and rejectReview POST to the review routes and refresh the order detail', async () => {
    const store = useOrdersStore()
    const approve = stubFetch(new Response(null, { status: 204 }), json({ id: 8, status: 'Pending' }))

    expect(await store.approveReview(8)).toBe(true)
    expect(callOf(approve, 0)).toMatchObject({ url: '/api/orders/8/approve-review', method: 'POST' })
    expect(store.currentOrder).toEqual({ id: 8, status: 'Pending' })

    const reject = stubFetch(new Response(null, { status: 204 }), json({ id: 8, status: 'Fraud' }))
    expect(await store.rejectReview(8, 'Stolen card')).toBe(true)
    expect(callOf(reject, 0)).toMatchObject({ url: '/api/orders/8/reject-review', method: 'POST', body: { reason: 'Stolen card' } })
    expect(store.currentOrder).toEqual({ id: 8, status: 'Fraud' })
  })

  it('a refused review sets actionError and still reads the order again', async () => {
    const store = useOrdersStore()
    const mock = stubFetch(problem('The order is not held for review.', 409), json({ id: 8, status: 'Active' }))

    expect(await store.approveReview(8)).toBe(false)
    expect(store.actionError).toBe('The order is not held for review.')
    expect(mock).toHaveBeenCalledTimes(2)
    expect(store.currentOrder).toEqual({ id: 8, status: 'Active' })
  })

  it('acceptOrder keeps the lines it could not fulfil and names them', async () => {
    const store = useOrdersStore()
    const failedLines = [
      { itemId: 1, kind: 'Domain', productName: 'Domain', domain: 'taken.example', firstPaymentAmount: 10, errorCode: null, reason: 'Taken.' },
      { itemId: 2, kind: 'Service', productName: 'Starter Hosting', domain: null, firstPaymentAmount: 5, errorCode: 'PRODUCT_NOT_FOUND', reason: 'Gone.' },
    ]
    stubFetch(json({ failedLines }), json({ id: 8, status: 'Active' }))

    expect(await store.acceptOrder(8)).toBe(true)
    expect(store.unfulfilledLines).toEqual(failedLines)
    expect(store.unfulfilledLineNames).toBe('taken.example, Starter Hosting')
  })

  it('retryOrderLine keeps the lines still failed and refreshes the detail', async () => {
    const store = useOrdersStore()
    const failedLines = [
      { itemId: 2, kind: 'Domain', productName: 'Domain', domain: 'taken.example', firstPaymentAmount: 10, errorCode: null, reason: 'Taken.' },
    ]
    const mock = stubFetch(json({ failedLines }), json({ id: 8, status: 'Incomplete' }))

    expect(await store.retryOrderLine(8, 2)).toBe(true)
    expect(callOf(mock, 0)).toMatchObject({ url: '/api/orders/8/items/2/retry', method: 'POST' })
    expect(store.unfulfilledLines).toEqual(failedLines)
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

  it('retryOrderLine marks only the running line and clears it afterwards', async () => {
    const store = useOrdersStore()
    let release: (response: Response) => void = () => undefined
    const gate = new Promise<Response>((resolve) => {
      release = resolve
    })
    vi.stubGlobal('fetch', vi.fn((url: string) => (url.endsWith('/retry') ? gate : Promise.resolve(json({ id: 8 })))))

    const pending = store.retryOrderLine(8, 2)
    expect(store.retryingItemId).toBe(2)
    release(json({ failedLines: [] }))
    await pending

    expect(store.retryingItemId).toBeNull()
  })

  it('a refused retry keeps the sentence on its own line and clears it on the next try', async () => {
    const store = useOrdersStore()
    stubFetch(problem('This order line cannot be retried now.', 409), json({ id: 8 }))

    expect(await store.retryOrderLine(8, 2)).toBe(false)
    expect(store.lineErrors).toEqual({ 2: 'This order line cannot be retried now.' })
    expect(store.retryingItemId).toBeNull()

    stubFetch(json({ failedLines: [] }), json({ id: 8 }))
    expect(await store.retryOrderLine(8, 2)).toBe(true)
    expect(store.lineErrors).toEqual({})
  })

  it('closeLineRefund closes the line and reads the order again', async () => {
    const store = useOrdersStore()
    const mock = stubFetch(new Response(null, { status: 204 }), json({ id: 8, status: 'Active' }))

    expect(await store.closeLineRefund(8, 2)).toBe(true)
    expect(callOf(mock, 0)).toMatchObject({ url: '/api/orders/8/items/2/close-refund', method: 'POST' })
    expect(store.currentOrder).toEqual({ id: 8, status: 'Active' })
    expect(store.actionError).toBeNull()
  })

  it('a refused closeLineRefund sets actionError and still reads the order again', async () => {
    const store = useOrdersStore()
    const mock = stubFetch(problem('This order line cannot be refunded now.', 409), json({ id: 8, status: 'Incomplete' }))

    expect(await store.closeLineRefund(8, 2)).toBe(false)
    expect(store.actionError).toBe('This order line cannot be refunded now.')
    expect(mock).toHaveBeenCalledTimes(2)
    expect(store.currentOrder).toEqual({ id: 8, status: 'Incomplete' })
  })

  it('a refused command returns false, sets actionError and does not refresh', async () => {
    const store = useOrdersStore()
    const mock = stubFetch(problem('Order is already accepted.', 409))

    expect(await store.acceptOrder(8)).toBe(false)
    expect(store.actionError).toBe('Order is already accepted.')
    expect(store.error).toBeNull()
    expect(mock).toHaveBeenCalledTimes(1)
  })

  it('deleteOrder clears actionError on success and leaves the list to the screen', async () => {
    const store = useOrdersStore()
    stubFetch(problem('Locked.', 409))
    expect(await store.deleteOrder(8)).toBe(false)
    expect(store.actionError).toBe('Locked.')

    const mock = stubFetch(new Response(null, { status: 204 }))
    expect(await store.deleteOrder(8)).toBe(true)
    expect(callOf(mock, 0)).toMatchObject({ url: '/api/orders/8', method: 'DELETE' })
    expect(mock).toHaveBeenCalledTimes(1)
    expect(store.actionError).toBeNull()
  })
})
