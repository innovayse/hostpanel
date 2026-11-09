/**
 * Orders, services and domains lists: each renders the page the API returned with a link to the
 * owning client, a pending order can be accepted, and a refused load shows the API's sentence.
 */
import { HttpMethod } from '../src/types/http'
import { expect, test } from '@playwright/test'
import { domainsPage, hostingProductName, hostingService, ordersPage, pendingOrder, registeredDomain, servicesPage } from './fixtures/orders'
import type { OrderListItem } from '../src/types/order'
import { OrderStatus } from '../src/types/order'
import { json, stubApi } from './support/stubApi'

test('accepting a pending order posts the command and reloads the list', async ({ page }) => {
  // The order as the "database" holds it; the accept call activates it.
  const stored: OrderListItem = { ...pendingOrder }

  const api = await stubApi(page, {
    signedIn: true,
    routes: [
      { method: HttpMethod.Get, path: '/api/orders', respond: () => ({ body: { ...ordersPage, items: [stored] } }) },
      {
        method: HttpMethod.Post,
        path: `/api/orders/${pendingOrder.id}/accept`,
        respond: () => {
          stored.status = OrderStatus.Active
          return { body: { failedLines: [] } }
        },
      },
      { method: HttpMethod.Get, path: `/api/orders/${pendingOrder.id}`, respond: () => ({ body: { ...stored, invoiceStatus: null, invoiceTotal: null, ipAddress: null, items: [] } }) },
    ],
  })

  await page.goto('/orders')
  await expect(page.getByTestId(`orders-row-${pendingOrder.id}`)).toContainText(pendingOrder.orderNumber)
  await expect(page.getByTestId(`orders-client-${pendingOrder.id}`)).toHaveAttribute('href', `/clients/${pendingOrder.clientId}`)
  await expect(page.getByTestId(`orders-client-${pendingOrder.id}`)).toHaveText(pendingOrder.clientName)

  // A pending order has several row actions, so its menu is opened first.
  await page.getByTestId(`orders-actions-${pendingOrder.id}`).click()
  await page.getByTestId(`orders-accept-${pendingOrder.id}`).click()

  // An active order no longer offers accept or cancel: only the detail link is left, without a menu.
  await expect(page.getByTestId(`orders-actions-${pendingOrder.id}`)).toHaveCount(0)
  await expect(page.getByTestId(`orders-accept-${pendingOrder.id}`)).toHaveCount(0)
  expect(api.callsTo(HttpMethod.Post, `/api/orders/${pendingOrder.id}/accept`)).toHaveLength(1)
})

test('a refused accept shows the API sentence and keeps the order pending', async ({ page }) => {
  const sentence = 'The order cannot be accepted while the invoice is unpaid.'
  await stubApi(page, {
    signedIn: true,
    routes: [
      json(HttpMethod.Get, '/api/orders', ordersPage),
      json(HttpMethod.Post, `/api/orders/${pendingOrder.id}/accept`, { type: 'CONFLICT', title: 'Conflict', status: 409, detail: sentence }, 409),
    ],
  })

  await page.goto('/orders')
  // A pending order has several row actions, so its menu is opened first.
  await page.getByTestId(`orders-actions-${pendingOrder.id}`).click()
  await page.getByTestId(`orders-accept-${pendingOrder.id}`).click()

  await expect(page.getByTestId('orders-action-feedback')).toContainText(sentence)
  await page.getByTestId(`orders-actions-${pendingOrder.id}`).click()
  await expect(page.getByTestId(`orders-accept-${pendingOrder.id}`)).toBeVisible()
})

test('the services list links each service to its client', async ({ page }) => {
  await stubApi(page, { signedIn: true, routes: [json(HttpMethod.Get, '/api/services', servicesPage)] })

  await page.goto('/services')

  await expect(page.getByTestId(`services-row-${hostingService.id}`)).toContainText(hostingProductName)
  await expect(page.getByTestId(`services-client-${hostingService.id}`)).toHaveAttribute('href', `/clients/${hostingService.clientId}`)
  await expect(page.getByTestId(`services-client-${hostingService.id}`)).toHaveText(hostingService.clientName)
})

test('the domains list opens a domain under its client', async ({ page }) => {
  await stubApi(page, { signedIn: true, routes: [json(HttpMethod.Get, '/api/domains', domainsPage)] })

  await page.goto('/domains')

  await expect(page.getByTestId(`domains-row-${registeredDomain.id}`)).toContainText(registeredDomain.name)
  await expect(page.getByTestId(`domains-name-${registeredDomain.id}`)).toHaveAttribute(
    'href',
    `/clients/${registeredDomain.clientId}/domains/${registeredDomain.id}`,
  )
  await expect(page.getByTestId(`domains-client-${registeredDomain.id}`)).toHaveText(registeredDomain.clientName)
})

test('an empty services list shows the empty state', async ({ page }) => {
  await stubApi(page, {
    signedIn: true,
    routes: [json(HttpMethod.Get, '/api/services', { items: [], pageNumber: 1, pageSize: 20, totalCount: 0, totalPages: 0 })],
  })

  await page.goto('/services')

  await expect(page.getByTestId('services-empty')).toBeVisible()
})
