/**
 * Storefront partners flow: the list shows the stored partner, creating one posts it and shows the
 * new row, and deleting asks first and then removes the row.
 */
import { HttpMethod } from '../src/types/http'
import { expect, test } from '@playwright/test'
import type { Page } from '@playwright/test'
import { ApiPath } from '../src/lib/apiPaths'
import { acmePartner } from './fixtures/storefront'
import type { Partner } from '../src/types/partner'
import { fieldOf } from './support/locators'
import { collectionPath, stubCrud } from './support/stubCrud'

/** Path of the collection endpoint. */
const COLLECTION = collectionPath(ApiPath.AdminPartners)

/**
 * Stubs the partners endpoints over an in-memory list, so a write shows up in the next read.
 *
 * @param page - The page under test.
 * @param stored - The list the "database" holds; mutated by the writes.
 * @returns The recorder handle.
 */
const stubPartners = (page: Page, stored: Partner[]) => stubCrud(page, { root: ApiPath.AdminPartners, stored, newId: 52 })

test('the list shows the stored partner with its website', async ({ page }) => {
  await stubPartners(page, [{ ...acmePartner }])

  await page.goto('/storefront/partners')

  await expect(page.getByTestId(`partner-row-${acmePartner.id}`)).toContainText('Acme Partner')
  await expect(page.getByTestId(`partner-row-${acmePartner.id}`)).toContainText('https://partner.example.com')
})

test('creating a partner posts it and lists the new row', async ({ page }) => {
  const api = await stubPartners(page, [{ ...acmePartner }])

  await page.goto('/storefront/partners')
  await page.getByTestId('partners-add').click()
  await page.waitForURL('**/storefront/partners/create')
  await fieldOf(page, 'partner-form-name').fill('Second Partner')
  await fieldOf(page, 'partner-form-url').fill('https://second.example.com')
  await page.getByTestId('settings-form-save').click()

  await page.waitForURL('**/storefront/partners')
  await expect(page.getByTestId('partner-row-52')).toContainText('Second Partner')
  expect(api.callsTo(HttpMethod.Post, COLLECTION)[0]?.body).toMatchObject({ url: 'https://second.example.com', isActive: true, translations: { en: { name: 'Second Partner' } } })
})

test('deleting a partner asks first and then removes the row', async ({ page }) => {
  const api = await stubPartners(page, [{ ...acmePartner }])

  await page.goto('/storefront/partners')
  await page.getByTestId(`partner-row-${acmePartner.id}`).getByTestId('row-actions-trigger').click()
  await page.getByTestId(`partner-delete-${acmePartner.id}`).click()
  expect(api.callsTo(HttpMethod.Delete, `${COLLECTION}/${acmePartner.id}`)).toHaveLength(0)
  await page.getByTestId('dialog-confirm').click()

  await expect(page.getByTestId(`partner-row-${acmePartner.id}`)).toHaveCount(0)
  expect(api.callsTo(HttpMethod.Delete, `${COLLECTION}/${acmePartner.id}`)).toHaveLength(1)
})
