/**
 * Currencies flow: editing a non-base currency opens the form with its stored values, submits
 * the upsert payload, closes the modal and shows the new rate in the table.
 */
import { HttpMethod } from '../src/types/http'
import { expect, test } from '@playwright/test'
import { eur, usd } from './fixtures/currencies'
import type { CurrencyDto } from '../src/types/currency'
import { fieldOf } from './support/locators'
import { stubApi } from './support/stubApi'

test('editing a currency saves the new prefix and rate and shows them in the table', async ({ page }) => {
  // The currencies table as the "database" holds it; the PUT mutates it so the re-fetch after a
  // save returns the edited row.
  const stored: CurrencyDto[] = [{ ...usd }, { ...eur }]

  const api = await stubApi(page, {
    signedIn: true,
    routes: [
      { method: HttpMethod.Get, path: '/api/admin/currencies', respond: () => ({ body: stored }) },
      {
        method: HttpMethod.Put,
        path: `/api/admin/currencies/${eur.code}`,
        respond: (call) => {
          const row = stored.find((currency) => currency.code === eur.code)
          if (row) {
            Object.assign(row, call.body)
          }
          return { status: 204 }
        },
      },
    ],
  })

  await page.goto('/settings/currencies')
  await expect(page.getByTestId(`currency-row-${usd.code}`)).toBeVisible()
  await expect(page.getByTestId(`currency-row-${eur.code}`)).toBeVisible()

  await page.getByTestId(`currency-actions-${eur.code}`).click()
  await page.getByTestId(`currency-edit-${eur.code}`).click()
  await expect(page.getByTestId('currency-form')).toBeVisible()
  await expect(fieldOf(page, 'currency-form-prefix')).toHaveValue(eur.prefix)

  await fieldOf(page, 'currency-form-prefix').fill('EUR')
  await fieldOf(page, 'currency-form-rate').fill('0.95')
  // The number input commits on blur.
  await fieldOf(page, 'currency-form-rate').press('Tab')
  await page.getByTestId('currency-form-save').click()

  await expect(page.getByTestId('currency-form')).toHaveCount(0)
  await expect(page.getByTestId(`currency-row-${eur.code}`)).toContainText('0.95000000')
  await expect(page.getByTestId(`currency-row-${eur.code}`)).toContainText('EUR2.99')

  const [save] = api.callsTo(HttpMethod.Put, `/api/admin/currencies/${eur.code}`)
  expect(save?.body).toEqual({
    numeric: eur.numeric,
    prefix: 'EUR',
    suffix: eur.suffix,
    decimals: eur.decimals,
    rateToBase: 0.95,
    isEnabled: true,
  })
})
