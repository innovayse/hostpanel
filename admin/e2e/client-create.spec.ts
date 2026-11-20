/**
 * Client creation flow: a refusal from the API shows its sentence and field messages and keeps the
 * form, and a corrected submit creates the client and returns to the list.
 */
import { HttpMethod } from '../src/types/http'
import { expect, test } from '@playwright/test'
import { clientsPage } from './fixtures/clients'
import { fieldOf } from './support/locators'
import { json, stubApi } from './support/stubApi'

test('a refused create shows the API sentence, and the next submit creates the client', async ({ page }) => {
  const refusal = 'A client with this email address already exists.'
  let attempts = 0

  const api = await stubApi(page, {
    signedIn: true,
    routes: [
      json(HttpMethod.Get, '/api/admin/currencies', []),
      json(HttpMethod.Get, '/api/reference/countries', []),
      json(HttpMethod.Get, '/api/clients', clientsPage),
      {
        method: HttpMethod.Post,
        path: '/api/clients/admin',
        respond: () => {
          attempts += 1
          if (attempts === 1) {
            return {
              status: 400,
              body: {
                type: 'VALIDATION_ERROR',
                title: 'Validation failed',
                status: 400,
                detail: refusal,
                errors: { email: [refusal] },
              },
            }
          }
          return { status: 204 }
        },
      },
    ],
  })

  await page.goto('/clients/add')

  await fieldOf(page, 'add-client-email').fill('new@example.test')
  await fieldOf(page, 'add-client-password').fill('long-enough-password')
  await fieldOf(page, 'add-client-first-name').fill('Alan')
  await fieldOf(page, 'add-client-last-name').fill('Turing')
  await page.getByTestId('add-client-submit').click()

  await expect(page.getByTestId('add-client-error')).toContainText(refusal)
  expect(new URL(page.url()).pathname).toBe('/clients/add')

  await page.getByTestId('add-client-submit').click()

  await page.waitForURL('**/clients')
  expect(api.callsTo(HttpMethod.Post, '/api/clients/admin')).toHaveLength(2)
  expect(api.callsTo(HttpMethod.Post, '/api/clients/admin')[1]?.body).toMatchObject({
    createNewUser: true,
    email: 'new@example.test',
    firstName: 'Alan',
    lastName: 'Turing',
  })
})

test('a refused empty form shows the API field message under the field', async ({ page }) => {
  const message = 'Email is required.'
  await stubApi(page, {
    signedIn: true,
    routes: [
      json(HttpMethod.Get, '/api/admin/currencies', []),
      json(HttpMethod.Get, '/api/reference/countries', []),
      {
        method: HttpMethod.Post,
        path: '/api/clients/admin',
        respond: () => ({
          status: 400,
          body: {
            type: 'VALIDATION_ERROR',
            title: 'Validation failed',
            status: 400,
            detail: message,
            errors: { email: [message] },
          },
        }),
      },
    ],
  })

  await page.goto('/clients/add')
  await page.getByTestId('add-client-submit').click()

  await expect(page.getByTestId('ui-form-field-email').getByRole('alert')).toBeVisible()
  expect(new URL(page.url()).pathname).toBe('/clients/add')
})
