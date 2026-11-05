/**
 * Upcoming renewals flow: the screen lists the services, domains and claimed lines the API returns, asks for
 * another window, raises a client invoice by hand, and releases a claimed line only after the confirmation.
 */
import { HttpMethod } from '../src/types/http'
import { expect, test } from '@playwright/test'
import { claimedLines, upcomingDomainsPage, upcomingServicesPage } from './fixtures/renewals'
import { json, stubApi } from './support/stubApi'

/** The three list routes every flow starts from. */
const listRoutes = [
  json(HttpMethod.Get, '/api/services/renewals/upcoming', upcomingServicesPage),
  json(HttpMethod.Get, '/api/domains/renewals/upcoming', upcomingDomainsPage),
  json(HttpMethod.Get, '/api/billing/renewal-claims', claimedLines),
]

test('the renewals screen lists services, domains and claimed lines', async ({ page }) => {
  await stubApi(page, { signedIn: true, routes: listRoutes })

  await page.goto('/billing/renewals')

  await expect(page.getByTestId('renewals-services-table')).toContainText('Starter')
  await expect(page.getByTestId('renewals-domains-table')).toContainText('example.test')
  await expect(page.getByTestId('renewals-claims-table')).toContainText('example.test renewal')
})

test('another window is requested from its tab', async ({ page }) => {
  const api = await stubApi(page, { signedIn: true, routes: listRoutes })
  await page.goto('/billing/renewals')

  await page.getByTestId('renewals-window-Next90Days').click()

  await expect.poll(() => api.callsTo(HttpMethod.Get, '/api/services/renewals/upcoming').some(c => c.search.includes('window=Next90Days'))).toBe(true)
})

test('a renewal invoice is raised by hand and the count is shown', async ({ page }) => {
  const api = await stubApi(page, {
    signedIn: true,
    routes: [...listRoutes, json(HttpMethod.Post, '/api/billing/client/9/renewal-invoices', 1)],
  })
  await page.goto('/billing/renewals')

  await page.getByTestId('renewals-service-generate-4').click()

  await expect(page.getByTestId('renewals-notice')).toContainText('Renewal invoices raised: 1.')
  expect(api.callsTo(HttpMethod.Post, '/api/billing/client/9/renewal-invoices')).toHaveLength(1)
})

test('a claimed line is released only after the confirmation', async ({ page }) => {
  const api = await stubApi(page, {
    signedIn: true,
    routes: [...listRoutes, { method: HttpMethod.Post, path: '/api/billing/5/items/11/release-renewal-claim', respond: () => ({ status: 204 }) }],
  })
  await page.goto('/billing/renewals')

  await page.getByTestId('renewals-claim-release-11').click()
  expect(api.callsTo(HttpMethod.Post, '/api/billing/5/items/11/release-renewal-claim')).toHaveLength(0)
  await page.getByRole('dialog').getByRole('button', { name: 'Release claim' }).click()

  await expect(page.getByTestId('renewals-notice')).toContainText('The claim was released.')
  expect(api.callsTo(HttpMethod.Post, '/api/billing/5/items/11/release-renewal-claim')).toHaveLength(1)
})
