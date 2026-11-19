/**
 * Client detail flow: the summary page loads its rollups, and editing the profile saves the
 * changed field, re-reads the record and confirms the save.
 */
import { HttpMethod } from '../src/types/http'
import { expect, test } from '@playwright/test'
import { adaDetail } from './fixtures/clients'
import { emptyClientSummary } from './fixtures/clientSummary'
import type { ClientDetail } from '../src/types/client'
import { fieldOf } from './support/locators'
import { json, stubApi } from './support/stubApi'

test('the summary page shows the client and its rollups', async ({ page }) => {
  await stubApi(page, {
    signedIn: true,
    routes: [
      json(HttpMethod.Get, '/api/clients/7', adaDetail),
      json(HttpMethod.Get, '/api/clients/7/summary', emptyClientSummary),
      json(HttpMethod.Get, '/api/services', { items: [], pageNumber: 1, pageSize: 5, totalCount: 0, totalPages: 0 }),
      json(HttpMethod.Get, '/api/domains', { items: [], pageNumber: 1, pageSize: 5, totalCount: 0, totalPages: 0 }),
      json(HttpMethod.Get, '/api/billing/quotes/client/7', { items: [], pageNumber: 1, pageSize: 5, totalCount: 0, totalPages: 0 }),
    ],
  })

  await page.goto('/clients/7/summary')

  await expect(page.getByTestId('client-header-name')).toHaveText(`${adaDetail.firstName} ${adaDetail.lastName}`)
  await expect(page.getByTestId('ui-detail-card-info')).toContainText(adaDetail.email)
  await expect(page.getByTestId('ui-detail-card-billing')).toBeVisible()
})

test('editing the profile saves the new value and confirms it', async ({ page }) => {
  // The record as the "database" holds it; the PUT mutates it so the re-read returns the edit.
  const stored: ClientDetail = { ...adaDetail }

  const api = await stubApi(page, {
    signedIn: true,
    routes: [
      { method: HttpMethod.Get, path: '/api/clients/7', respond: () => ({ body: stored }) },
      {
        method: HttpMethod.Put,
        path: '/api/clients/7',
        respond: (call) => {
          Object.assign(stored, call.body)
          return { status: 204 }
        },
      },
      json(HttpMethod.Get, '/api/admin/currencies', []),
      json(HttpMethod.Get, '/api/reference/countries', []),
    ],
  })

  await page.goto('/clients/7/profile')
  await expect(fieldOf(page, 'client-profile-first-name')).toHaveValue(adaDetail.firstName)

  await fieldOf(page, 'client-profile-first-name').fill('Augusta')
  await page.getByTestId('client-profile-save').click()

  await expect(page.getByTestId('client-profile-saved')).toBeVisible()
  await expect(page.getByTestId('client-header-name')).toHaveText(`Augusta ${adaDetail.lastName}`)
  expect(api.callsTo(HttpMethod.Put, '/api/clients/7')[0]?.body).toMatchObject({ firstName: 'Augusta', lastName: adaDetail.lastName })
})
