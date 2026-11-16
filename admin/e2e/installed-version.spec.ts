/**
 * Installed version: the sidebar footer and the Settings page both show "Hostpanel <version>" from
 * one request to the staff version endpoint, the version does not depend on the Admin-only release
 * status, and a refused load shows no version and no error. The panel opens only for an Admin, so a
 * non-admin staff user is covered by the layout and store unit tests instead.
 */
import { HttpMethod } from '../src/types/http'
import { expect, test } from '@playwright/test'
import { json, stubApi } from './support/stubApi'
import type { Setting } from '../src/types/setting'

/** A setting so the Settings page has a row to draw. */
const timezone: Setting = { id: 90, key: 'general.timezone', value: 'UTC', description: 'Default time zone' }

/** Body of the staff version endpoint of a running 1.4.0 build. */
const installed = { installedVersion: '1.4.0' }

/** Problem body the API sends when the caller may not read the version. */
const forbidden = { type: 'FORBIDDEN', title: 'Forbidden', status: 403, detail: 'No access.' }

test('the sidebar and the Settings page show the installed version from one request', async ({ page }) => {
  const api = await stubApi(page, {
    signedIn: true,
    routes: [
      json(HttpMethod.Get, '/api/admin/version', installed),
      json(HttpMethod.Get, '/api/admin/release-status', {
        installedVersion: '1.4.0',
        latestVersion: null,
        latestPublishedAt: null,
        releaseNotesUrl: null,
        upgradeCommand: null,
        isUpgradeAvailable: false,
      }),
      json(HttpMethod.Get, '/api/admin/settings', [timezone]),
    ],
  })

  await page.goto('/settings/general')

  await expect(page.getByTestId('sidebar-version')).toHaveText('Hostpanel 1.4.0')
  await expect(page.getByTestId('settings-version')).toHaveText('Hostpanel 1.4.0')
  expect(api.callsTo(HttpMethod.Get, '/api/admin/version')).toHaveLength(1)
})

test('the version shows even when the release status is refused', async ({ page }) => {
  const api = await stubApi(page, {
    signedIn: true,
    routes: [
      json(HttpMethod.Get, '/api/admin/version', installed),
      json(HttpMethod.Get, '/api/admin/release-status', forbidden, 403),
      json(HttpMethod.Get, '/api/admin/settings', [timezone]),
    ],
  })

  await page.goto('/settings/general')

  await expect(page.getByTestId('sidebar-version')).toHaveText('Hostpanel 1.4.0')
  await expect(page.getByTestId('settings-version')).toHaveText('Hostpanel 1.4.0')
  expect(api.callsTo(HttpMethod.Get, '/api/admin/version')).toHaveLength(1)
})

test('a refused load shows no version and no error', async ({ page }) => {
  await stubApi(page, {
    signedIn: true,
    routes: [
      json(HttpMethod.Get, '/api/admin/version', forbidden, 403),
      json(HttpMethod.Get, '/api/admin/settings', [timezone]),
    ],
  })

  await page.goto('/settings/general?tab=advanced')

  await expect(page.getByTestId(`setting-value-${timezone.id}`)).toBeVisible()
  await expect(page.getByTestId('sidebar-version')).toHaveCount(0)
  await expect(page.getByTestId('settings-version')).toHaveCount(0)
})
