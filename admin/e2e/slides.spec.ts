/**
 * Slides flow: the list shows each slide's title, deleting asks first and removes the slide, and
 * leaving the slide form with unsaved edits asks whether to stay or leave.
 */
import { HttpMethod } from '../src/types/http'
import { expect, test } from '@playwright/test'
import { welcomeSlide } from './fixtures/slides'
import type { Slide } from '../src/types/slide'
import { fieldOf } from './support/locators'
import { json, stubApi } from './support/stubApi'

test('deleting a slide asks first and then removes it from the list', async ({ page }) => {
  const stored: Slide[] = [{ ...welcomeSlide }]

  const api = await stubApi(page, {
    signedIn: true,
    routes: [
      { method: HttpMethod.Get, path: '/api/admin/slides', respond: () => ({ body: stored }) },
      {
        method: HttpMethod.Delete,
        path: `/api/admin/slides/${welcomeSlide.id}`,
        respond: () => {
          stored.splice(0, stored.length)
          return { status: 204 }
        },
      },
    ],
  })

  await page.goto('/settings/slides')
  await expect(page.getByTestId(`slide-row-${welcomeSlide.id}`)).toContainText('Welcome')

  await page.getByTestId(`slide-row-${welcomeSlide.id}`).getByTestId('row-actions-trigger').click()
  await page.getByTestId(`slide-delete-${welcomeSlide.id}`).click()
  expect(api.callsTo(HttpMethod.Delete, `/api/admin/slides/${welcomeSlide.id}`)).toHaveLength(0)
  await page.getByTestId('dialog-confirm').click()

  await expect(page.getByTestId(`slide-row-${welcomeSlide.id}`)).toHaveCount(0)
  expect(api.callsTo(HttpMethod.Delete, `/api/admin/slides/${welcomeSlide.id}`)).toHaveLength(1)
})

test('leaving the slide form with unsaved edits asks first, and staying keeps the form', async ({ page }) => {
  await stubApi(page, {
    signedIn: true,
    routes: [json(HttpMethod.Get, '/api/products', []), json(HttpMethod.Get, '/api/products/groups', []), json(HttpMethod.Get, '/api/admin/slides', [])],
  })

  await page.goto('/settings/slides/create')
  await fieldOf(page, 'slide-form-title').fill('Draft title')

  await page.getByTestId('settings-form-cancel').click()

  // The route change is parked behind the question; the form is still there.
  await expect(page.getByTestId('dialog-cancel')).toBeVisible()
  await page.getByTestId('dialog-cancel').click()
  await expect(fieldOf(page, 'slide-form-title')).toHaveValue('Draft title')
  expect(new URL(page.url()).pathname).toBe('/settings/slides/create')

  // Asking again and confirming lets the navigation through.
  await page.getByTestId('settings-form-cancel').click()
  await page.getByTestId('dialog-confirm').click()
  await page.waitForURL('**/settings/slides')
})

test('a clean slide form leaves without asking', async ({ page }) => {
  await stubApi(page, {
    signedIn: true,
    routes: [json(HttpMethod.Get, '/api/products', []), json(HttpMethod.Get, '/api/products/groups', []), json(HttpMethod.Get, '/api/admin/slides', [])],
  })

  await page.goto('/settings/slides/create')
  await page.getByTestId('settings-form-cancel').click()

  await page.waitForURL('**/settings/slides')
  await expect(page.getByTestId('dialog-confirm')).toHaveCount(0)
})
