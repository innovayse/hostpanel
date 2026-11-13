/**
 * System settings tabs: the selected tab lives in the address (deep link, back and forward, unknown value),
 * every tab has its own Save bar, and the arrow keys move between the tabs.
 */
import { HttpMethod } from '../src/types/http'
import { expect, test } from '@playwright/test'
import { fieldOf, settingsPanel } from './support/locators'
import { json, stubApi } from './support/stubApi'
import type { Page } from '@playwright/test'
import type { Setting } from '../src/types/setting'

/** Screen path the tab query is appended to. */
const SCREEN = '/settings/general'

/** One identity row (General), one branding row (Appearance) and one raw row (Advanced). */
const settings: Setting[] = [
  { id: 1, key: 'portal.site.name', value: 'Acme' },
  { id: 11, key: 'portal.logo', value: '', description: 'Logo' },
  { id: 90, key: 'general.timezone', value: 'UTC', description: 'Default time zone' },
]

/**
 * Signs in and serves the settings list.
 *
 * @param page - The page under test.
 */
const openWithSettings = async (page: Page): Promise<void> => {
  await stubApi(page, { signedIn: true, routes: [json(HttpMethod.Get, '/api/admin/settings', settings)] })
}

test('a deep link opens its tab', async ({ page }) => {
  await openWithSettings(page)

  await page.goto(`${SCREEN}?tab=advanced`)

  await expect(page.getByTestId('settings-tab-advanced')).toHaveAttribute('aria-selected', 'true')
  await expect(page.getByTestId('setting-row-90')).toBeVisible()
  await expect(page.getByTestId('site-identity-name')).toBeHidden()
})

test('an unknown tab opens General', async ({ page }) => {
  await openWithSettings(page)

  await page.goto(`${SCREEN}?tab=nope`)

  await expect(page.getByTestId('settings-tab-general')).toHaveAttribute('aria-selected', 'true')
  await expect(fieldOf(page, 'site-identity-name')).toHaveValue('Acme')
})

test('the back and forward buttons restore the tab', async ({ page }) => {
  await openWithSettings(page)
  await page.goto(SCREEN)

  await page.getByTestId('settings-tab-appearance').click()
  await expect(page).toHaveURL(/tab=appearance/)
  await page.getByTestId('settings-tab-advanced').click()
  await expect(page).toHaveURL(/tab=advanced/)

  await page.goBack()
  await expect(page.getByTestId('settings-tab-appearance')).toHaveAttribute('aria-selected', 'true')
  await expect(page.getByTestId('branding-url-logo')).toBeVisible()

  await page.goBack()
  await expect(page.getByTestId('settings-tab-general')).toHaveAttribute('aria-selected', 'true')
  await expect(page).not.toHaveURL(/tab=/)

  await page.goForward()
  await expect(page.getByTestId('settings-tab-appearance')).toHaveAttribute('aria-selected', 'true')
})

test('an edit in one tab does not enable the bar of another tab, and survives a tab switch', async ({ page }) => {
  await openWithSettings(page)
  await page.goto(SCREEN)

  await fieldOf(page, 'site-identity-name').fill('Acme Cloud')
  await expect(settingsPanel(page, 'general').getByTestId('settings-form-save')).toBeEnabled()

  await page.getByTestId('settings-tab-advanced').click()
  await expect(settingsPanel(page, 'advanced').getByTestId('settings-form-status')).toHaveText('No unsaved changes')
  await expect(settingsPanel(page, 'advanced').getByTestId('settings-form-save')).toBeDisabled()

  await page.getByTestId('settings-tab-general').click()
  await expect(fieldOf(page, 'site-identity-name')).toHaveValue('Acme Cloud')
  await expect(settingsPanel(page, 'general').getByTestId('settings-form-status')).toHaveText('1 unsaved change')
})

test('the arrow keys move the selection and the focus between the tabs', async ({ page }) => {
  await openWithSettings(page)
  await page.goto(SCREEN)

  await page.getByTestId('settings-tab-general').focus()
  await page.keyboard.press('ArrowRight')
  await expect(page.getByTestId('settings-tab-appearance')).toHaveAttribute('aria-selected', 'true')
  await expect(page.getByTestId('settings-tab-appearance')).toBeFocused()
  await expect(page).toHaveURL(/tab=appearance/)

  await page.keyboard.press('ArrowLeft')
  await expect(page.getByTestId('settings-tab-general')).toBeFocused()
  await expect(page.getByTestId('settings-tab-general')).toHaveAttribute('aria-selected', 'true')
})
