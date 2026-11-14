/**
 * Orders list flow: the search on the order number and the status filter of the filter dialog are sent to the API and
 * kept in the address, the applied status is a chip that is removed from its own button, and a link with a search and a
 * status opens the narrowed list.
 */
import { HttpMethod } from '../src/types/http'
import { expect, test } from '@playwright/test'
import type { Page } from '@playwright/test'
import { ordersPage, pendingOrder } from './fixtures/orders'
import { OrderStatus } from '../src/types/order'
import type { OrderListItem } from '../src/types/order'
import type { PaginatedList } from '../src/types/paginatedlist'
import { fieldOf } from './support/locators'
import { stubApi } from './support/stubApi'
import type { StubApi } from './support/stubApi'

/** A cancelled order with another number: the one the search and the status filter must keep. */
const cancelledOrder: OrderListItem = {
  ...pendingOrder,
  id: 32,
  orderNumber: 'ORD-0032',
  status: OrderStatus.Cancelled,
}

/** Every order the stubbed list holds. */
const allOrders: OrderListItem[] = [...ordersPage.items, cancelledOrder]

/**
 * Stubs the order list so it narrows by the same query parameters the real API takes.
 *
 * @param page - The page under test.
 * @returns The stubbed API, to read the requests it saw.
 */
const stubOrderList = (page: Page): Promise<StubApi> =>
  stubApi(page, {
    signedIn: true,
    routes: [
      {
        method: HttpMethod.Get,
        path: '/api/orders',
        respond: (call) => {
          const query = new URLSearchParams(call.search)
          const term = query.get('search')?.toLowerCase() ?? ''
          const items = allOrders.filter(
            (order) =>
              order.orderNumber.toLowerCase().includes(term) && (query.get('status') === null || order.status === query.get('status')),
          )
          const body: PaginatedList<OrderListItem> = { items, totalCount: items.length, pageNumber: 1, pageSize: 20, totalPages: 1 }
          return { body }
        },
      },
    ],
  })

test('searching by order number sends the search and shows only the matching order', async ({ page }) => {
  const api = await stubOrderList(page)

  await page.goto('/orders')
  await expect(page.getByTestId(`orders-row-${pendingOrder.id}`)).toBeVisible()
  await expect(page.getByTestId(`orders-row-${cancelledOrder.id}`)).toBeVisible()

  // The search box writes to the address after a pause and the list reloads from it.
  await fieldOf(page, 'orders-search').fill('0032')
  await expect(page).toHaveURL(/search=0032/)

  await expect(page.getByTestId(`orders-row-${cancelledOrder.id}`)).toBeVisible()
  await expect(page.getByTestId(`orders-row-${pendingOrder.id}`)).toHaveCount(0)
  expect(api.callsTo(HttpMethod.Get, '/api/orders').at(-1)?.search).toContain('search=0032')
})

test('the status filter is sent to the API, shown as a chip and removed from its chip', async ({ page }) => {
  const api = await stubOrderList(page)

  await page.goto('/orders')
  await expect(page.getByTestId(`orders-row-${pendingOrder.id}`)).toBeVisible()

  await page.getByTestId('filter-trigger').click()
  await page.getByTestId('orders-filter-status').getByTestId('ui-select-trigger').click()
  await page.getByTestId('ui-select-option').filter({ hasText: /^Cancelled$/ }).click()
  await page.getByTestId('filter-apply').click()

  await expect(page).toHaveURL(/status=Cancelled/)
  await expect(page.getByTestId('filter-chip-status')).toContainText('Cancelled')
  await expect(page.getByTestId(`orders-row-${cancelledOrder.id}`)).toBeVisible()
  await expect(page.getByTestId(`orders-row-${pendingOrder.id}`)).toHaveCount(0)
  expect(api.callsTo(HttpMethod.Get, '/api/orders').at(-1)?.search).toContain('status=Cancelled')

  await page.getByTestId('filter-chip-remove-status').click()

  await expect(page.getByTestId('filter-chip-status')).toHaveCount(0)
  await expect(page.getByTestId(`orders-row-${pendingOrder.id}`)).toBeVisible()
  expect(api.callsTo(HttpMethod.Get, '/api/orders').at(-1)?.search).not.toContain('status=')
})

test('a list link with a search and a status opens the narrowed list', async ({ page }) => {
  const api = await stubOrderList(page)

  await page.goto('/orders?search=ORD-00&status=Cancelled')

  await expect(page.getByTestId('filter-chip-status')).toContainText('Cancelled')
  await expect(page.getByTestId(`orders-row-${cancelledOrder.id}`)).toBeVisible()
  await expect(page.getByTestId(`orders-row-${pendingOrder.id}`)).toHaveCount(0)
  const first = api.callsTo(HttpMethod.Get, '/api/orders')[0]?.search ?? ''
  expect(first).toContain('search=ORD-00')
  expect(first).toContain('status=Cancelled')
})
