/**
 * Storefront server locations flow: the list shows the stored location, creating one posts it with
 * an upper-case country code and shows the new row, and deleting asks first and then removes the row.
 */
import { HttpMethod } from '../src/types/http'
import { expect, test } from '@playwright/test'
import type { Page } from '@playwright/test'
import { ApiPath } from '../src/lib/apiPaths'
import { frankfurtLocation } from './fixtures/storefront'
import type { ServerLocation } from '../src/types/serverlocation'
import { fieldOf } from './support/locators'
import { collectionPath, stubCrud } from './support/stubCrud'

/** Path of the collection endpoint. */
const COLLECTION = collectionPath(ApiPath.AdminLocations)

/**
 * Stubs the locations endpoints over an in-memory list, so a write shows up in the next read.
 *
 * @param page - The page under test.
 * @param stored - The list the "database" holds; mutated by the writes.
 * @returns The recorder handle.
 */
const stubLocations = (page: Page, stored: ServerLocation[]) => stubCrud(page, { root: ApiPath.AdminLocations, stored, newId: 82 })

test('the list shows the stored location with its country and city', async ({ page }) => {
  await stubLocations(page, [{ ...frankfurtLocation }])

  await page.goto('/storefront/locations')

  await expect(page.getByTestId(`location-row-${frankfurtLocation.id}`)).toContainText('Frankfurt')
  await expect(page.getByTestId(`location-row-${frankfurtLocation.id}`)).toContainText('DE')
  await expect(page.getByTestId(`location-row-${frankfurtLocation.id}`)).toContainText('Frankfurt am Main')
})

test('creating a location posts it and lists the new row', async ({ page }) => {
  const api = await stubLocations(page, [{ ...frankfurtLocation }])

  await page.goto('/storefront/locations')
  await page.getByTestId('locations-add').click()
  await page.waitForURL('**/storefront/locations/create')
  await fieldOf(page, 'location-form-country').fill('am')
  await fieldOf(page, 'location-form-name').fill('Yerevan')
  await fieldOf(page, 'location-form-city').fill('Yerevan city')
  await page.getByTestId('settings-form-save').click()

  await page.waitForURL('**/storefront/locations')
  await expect(page.getByTestId('location-row-82')).toContainText('Yerevan')
  expect(api.callsTo(HttpMethod.Post, COLLECTION)[0]?.body).toMatchObject({ countryCode: 'AM', isActive: true, translations: { en: { name: 'Yerevan', city: 'Yerevan city' } } })
})

test('deleting a location asks first and then removes the row', async ({ page }) => {
  const api = await stubLocations(page, [{ ...frankfurtLocation }])

  await page.goto('/storefront/locations')
  await page.getByTestId(`location-row-${frankfurtLocation.id}`).getByTestId('row-actions-trigger').click()
  await page.getByTestId(`location-delete-${frankfurtLocation.id}`).click()
  expect(api.callsTo(HttpMethod.Delete, `${COLLECTION}/${frankfurtLocation.id}`)).toHaveLength(0)
  await page.getByTestId('dialog-confirm').click()

  await expect(page.getByTestId(`location-row-${frankfurtLocation.id}`)).toHaveCount(0)
  expect(api.callsTo(HttpMethod.Delete, `${COLLECTION}/${frankfurtLocation.id}`)).toHaveLength(1)
})
