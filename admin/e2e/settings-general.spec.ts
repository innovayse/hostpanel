/**
 * System settings flow: editing a raw setting reveals its Save button and PUTs the new value, and
 * a refusal shows the API's validation sentence while the draft stays on screen.
 */
import { HttpMethod } from '../src/types/http'
import { expect, test } from '@playwright/test'
import { fieldOf } from './support/locators'
import { json, stubApi } from './support/stubApi'
import type { Setting } from '../src/types/setting'

/** A free-form setting the raw table edits. */
const timezone: Setting = { id: 90, key: 'general.timezone', value: 'UTC', description: 'Default time zone' }

test('editing a setting saves the new value', async ({ page }) => {
  // The settings as the "database" holds them; the PUT mutates the row so the re-read returns it.
  const stored: Setting[] = [{ ...timezone }]

  const api = await stubApi(page, {
    signedIn: true,
    routes: [
      { method: HttpMethod.Get, path: '/api/admin/settings', respond: () => ({ body: stored }) },
      {
        method: HttpMethod.Put,
        path: `/api/admin/settings/${timezone.id}`,
        respond: (call) => {
          const row = stored[0]
          if (row) {
            row.value = (call.body as { value: string }).value
          }
          return { status: 204 }
        },
      },
    ],
  })

  await page.goto('/settings/general')
  await expect(page.getByTestId(`setting-save-${timezone.id}`)).toHaveCount(0)

  await fieldOf(page, `setting-value-${timezone.id}`).fill('Asia/Yerevan')
  await page.getByTestId(`setting-save-${timezone.id}`).click()

  await expect(page.getByTestId(`setting-save-${timezone.id}`)).toHaveCount(0)
  await expect(fieldOf(page, `setting-value-${timezone.id}`)).toHaveValue('Asia/Yerevan')
  expect(api.callsTo(HttpMethod.Put, `/api/admin/settings/${timezone.id}`)[0]?.body).toEqual({ value: 'Asia/Yerevan' })
})

test('a refused save shows the API sentence and keeps the draft', async ({ page }) => {
  const sentence = 'Asia/Nowhere is not a known time zone.'
  await stubApi(page, {
    signedIn: true,
    routes: [
      json(HttpMethod.Get, '/api/admin/settings', [timezone]),
      json(
        HttpMethod.Put,
        `/api/admin/settings/${timezone.id}`,
        { type: 'VALIDATION_ERROR', title: 'Validation failed', status: 400, detail: sentence },
        400,
      ),
    ],
  })

  await page.goto('/settings/general')
  await fieldOf(page, `setting-value-${timezone.id}`).fill('Asia/Nowhere')
  await page.getByTestId(`setting-save-${timezone.id}`).click()

  await expect(page.getByTestId(`setting-outcome-${timezone.id}`)).toContainText(sentence)
  await expect(fieldOf(page, `setting-value-${timezone.id}`)).toHaveValue('Asia/Nowhere')
})
