/**
 * Settings branding flow: picking a logo uploads it as multipart data, stages the returned URL
 * in the field, and saving writes that URL to the setting.
 */
import { HttpMethod } from '../src/types/http'
import { expect, test } from '@playwright/test'
import { brandingSettings, tinyPng, uploadedLogoUrl } from './fixtures/settings'
import type { Setting } from '../src/types/setting'
import { fieldOf } from './support/locators'
import { json, stubApi } from './support/stubApi'

test('uploading a logo stages its URL and saving persists it', async ({ page }) => {
  // The settings table as the "database" holds it; the PUT below mutates it so the re-fetch
  // after a save sees the stored value, as the real list would.
  const stored: Setting[] = brandingSettings.map((setting) => ({ ...setting }))
  const logoId = stored.find((setting) => setting.key === 'portal.logo')?.id

  const api = await stubApi(page, {
    signedIn: true,
    routes: [
      { method: HttpMethod.Get, path: '/api/admin/settings', respond: () => ({ body: stored }) },
      json(HttpMethod.Post, '/api/admin/settings/branding/logo', { url: uploadedLogoUrl }),
      {
        method: HttpMethod.Put,
        path: new RegExp(`^/api/admin/settings/${logoId}$`),
        respond: (call) => {
          const row = stored.find((setting) => setting.id === logoId)
          if (row) {
            row.value = (call.body as { value: string }).value
          }
          return { status: 204 }
        },
      },
    ],
  })
  // The preview loads the returned URL as an image; the stub serves it, as the API's static files
  // would, so the request never falls through to the preview server's proxy.
  await page.route(uploadedLogoUrl, (route) => route.fulfill({ contentType: 'image/png', body: tinyPng }))

  await page.goto('/settings/general?tab=appearance')

  await page.getByTestId('branding-file-logo').setInputFiles({
    name: 'logo.png',
    mimeType: 'image/png',
    buffer: tinyPng,
  })

  // The upload went out as multipart form data under the field `file`.
  await expect(fieldOf(page, 'branding-url-logo')).toHaveValue(uploadedLogoUrl)
  const [upload] = api.callsTo(HttpMethod.Post, '/api/admin/settings/branding/logo')
  expect(upload?.contentType).toContain('multipart/form-data')
  expect(upload?.postData).toContain('filename="logo.png"')
  await expect(page.getByTestId('branding-preview-logo')).toBeAttached()

  // Nothing is stored until Save: the staged URL is only a draft.
  expect(api.callsTo(HttpMethod.Put, `/api/admin/settings/${logoId}`)).toHaveLength(0)
  await page.getByTestId('branding-save-logo').click()

  await expect(page.getByTestId('branding-save-logo')).toHaveCount(0)
  await expect(fieldOf(page, 'branding-url-logo')).toHaveValue(uploadedLogoUrl)
  const [save] = api.callsTo(HttpMethod.Put, `/api/admin/settings/${logoId}`)
  expect(save?.body).toEqual({ value: uploadedLogoUrl })
})
