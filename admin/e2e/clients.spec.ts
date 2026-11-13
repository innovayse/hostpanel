/**
 * Clients flow: the list renders the page the API returned, opening a row loads that client
 * into the client layout, and the sidebar's Profile entry shows the record in the edit form.
 */
import { HttpMethod } from '../src/types/http'
import { expect, test } from '@playwright/test'
import { adaDetail, clientsPage } from './fixtures/clients'
import { fieldOf } from './support/locators'
import { json, stubApi } from './support/stubApi'

test('the clients list opens a client and its profile shows the stored record', async ({ page }) => {
  const api = await stubApi(page, {
    signedIn: true,
    routes: [
      json(HttpMethod.Get, '/api/clients', clientsPage),
      json(HttpMethod.Get, '/api/clients/7', adaDetail),
    ],
  })

  await page.goto('/clients')

  const rows = page.getByTestId('client-row')
  await expect(rows).toHaveCount(clientsPage.items.length)
  await expect(rows.first()).toContainText(adaDetail.email)

  await rows.filter({ hasText: adaDetail.email }).click()
  await page.waitForURL('**/clients/7/summary')
  await expect(page.getByTestId('client-header-name')).toHaveText(`${adaDetail.firstName} ${adaDetail.lastName}`)

  await page.getByTestId('client-tab-profile').click()
  await page.waitForURL('**/clients/7/profile')
  await expect(fieldOf(page, 'client-profile-first-name')).toHaveValue(adaDetail.firstName)

  // The list asked for the first page; the layout asked for exactly the client that was clicked.
  expect(api.callsTo(HttpMethod.Get, '/api/clients')[0]?.search).toContain('page=1')
  expect(api.callsTo(HttpMethod.Get, '/api/clients/7').length).toBeGreaterThan(0)
})
