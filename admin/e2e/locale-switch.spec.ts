/**
 * Language switch: choosing a language in the top bar re-renders the shell in it, remembers the
 * choice across a reload, and stamps every API request with the chosen language so the server's
 * sentences come back in it.
 */
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { expect, test } from '@playwright/test'
import { stubApi } from './support/stubApi'

/** The Russian title of the notifications page, read from the catalogue. */
const russianTitle = (JSON.parse(readFileSync(join(process.cwd(), 'src/locales/ru/common.json'), 'utf8')) as { notifications: string }).notifications

test('switching to Russian re-renders the shell, survives a reload and stamps the requests', async ({ page }) => {
  await stubApi(page, { signedIn: true })

  await page.goto('/notifications')
  await expect(page.getByTestId('topbar-title')).not.toHaveText(russianTitle)

  await page.getByTestId('topbar-locale').click()
  await page.getByTestId('locale-option-ru').click()
  await expect(page.getByTestId('topbar-title')).toHaveText(russianTitle)

  // The choice is remembered, and the first request after the reload already carries it.
  const stamped = page.waitForRequest(
    (request) => request.url().includes('/api/admin/dashboard/activity') && request.headers()['x-language'] === 'ru',
  )
  await page.reload()
  await stamped
  await expect(page.getByTestId('topbar-title')).toHaveText(russianTitle)
})
