/**
 * Settings index flow: the index groups every section as a card, a card opens its section, and the
 * general settings page shows the unsaved-changes bar once a field is edited. Runs on a phone width
 * as well, where the cards stack in one column.
 */
import { HttpMethod } from '../src/types/http'
import { expect, test } from '@playwright/test'
import { fieldOf } from './support/locators'
import { json, stubApi } from './support/stubApi'
import type { Setting } from '../src/types/setting'

/** A free-form setting the general page lists. */
const timezone: Setting = { id: 90, key: 'general.timezone', value: 'UTC', description: 'Default time zone' }

/** The site name row, which the identity card edits. */
const siteName: Setting = { id: 1, key: 'portal.site.name', value: 'Acme' }

test('a card of the index opens its section', async ({ page }) => {
  await stubApi(page, {
    signedIn: true,
    routes: [json(HttpMethod.Get, '/api/admin/settings', [timezone])],
  })

  await page.goto('/settings')
  await expect(page.getByTestId('settings-index-view')).toBeVisible()
  await page.getByTestId('settings-card-general').click()

  await expect(page).toHaveURL(/\/settings\/general$/)
  await expect(page.getByTestId('system-settings-view')).toBeVisible()
})

test('the index fits a phone without sideways scrolling', async ({ page }) => {
  await page.setViewportSize({ width: 360, height: 740 })
  await stubApi(page, { signedIn: true, routes: [] })

  await page.goto('/settings')
  await expect(page.getByTestId('settings-card-currencies')).toBeVisible()

  const overflows = await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth)
  expect(overflows).toBe(false)
})

test('editing a field counts as an unsaved change in the bar', async ({ page }) => {
  await stubApi(page, {
    signedIn: true,
    routes: [json(HttpMethod.Get, '/api/admin/settings', [siteName])],
  })

  await page.goto('/settings/general')
  await expect(page.getByTestId('settings-form-status')).toHaveText('No unsaved changes')
  await expect(page.getByTestId('settings-form-save')).toBeDisabled()

  await fieldOf(page, 'site-identity-name').fill('Acme Cloud')

  await expect(page.getByTestId('settings-form-status')).toHaveText('1 unsaved change')
  await expect(page.getByTestId('settings-form-save')).toBeEnabled()
})
