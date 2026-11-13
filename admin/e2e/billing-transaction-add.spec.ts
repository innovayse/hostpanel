/**
 * Add-transaction flow: the operator picks a client, enters an amount and a description, submits,
 * and lands on the transactions ledger, which shows the new entry; a refusal shows the API's
 * sentence and keeps the form.
 */
import { HttpMethod } from '../src/types/http'
import { expect, test } from '@playwright/test'
import { clientsPage } from './fixtures/clients'
import { ledgerAfterAdd, ledgerEmpty, newTransaction } from './fixtures/transactions'
import { fieldOf } from './support/locators'
import { json, stubApi } from './support/stubApi'

test('adding a transaction posts it and returns to the ledger', async ({ page }) => {
  // The ledger as the "database" holds it; the POST makes the new entry appear.
  let created = false

  const api = await stubApi(page, {
    signedIn: true,
    routes: [
      json(HttpMethod.Get, '/api/clients', clientsPage),
      { method: HttpMethod.Get, path: '/api/transactions', respond: () => ({ body: created ? ledgerAfterAdd : ledgerEmpty }) },
      {
        method: HttpMethod.Post,
        path: '/api/transactions',
        respond: () => {
          created = true
          return { status: 204 }
        },
      },
    ],
  })

  await page.goto('/billing/add-transaction')
  await expect(page.getByTestId('billing-transaction-form')).toBeVisible()

  await page.getByTestId('billing-transaction-client').getByTestId('ui-select-trigger').click()
  await page.getByTestId('ui-select-option').filter({ hasText: `${newTransaction.clientName} - #${newTransaction.clientId}` }).click()
  await fieldOf(page, 'billing-transaction-amount-in').fill(String(newTransaction.amountIn))
  await fieldOf(page, 'billing-transaction-amount-in').press('Tab')
  await fieldOf(page, 'billing-transaction-description').fill(newTransaction.description)
  await page.getByTestId('billing-transaction-submit').click()

  await page.waitForURL('**/billing/transactions')
  await expect(page.getByTestId('billing-transactions-table')).toContainText(newTransaction.description)
  expect(api.callsTo(HttpMethod.Post, '/api/transactions')[0]?.body).toMatchObject({
    clientId: newTransaction.clientId,
    amountIn: newTransaction.amountIn,
    description: newTransaction.description,
  })
})

test('a refused transaction shows the API sentence and stays on the form', async ({ page }) => {
  const sentence = 'The transaction amount must be greater than zero.'
  await stubApi(page, {
    signedIn: true,
    routes: [
      json(HttpMethod.Get, '/api/clients', clientsPage),
      json(HttpMethod.Post, '/api/transactions', { type: 'VALIDATION_ERROR', title: 'Validation failed', status: 400, detail: sentence }, 400),
    ],
  })

  await page.goto('/billing/add-transaction')
  await page.getByTestId('billing-transaction-client').getByTestId('ui-select-trigger').click()
  await page.getByTestId('ui-select-option').filter({ hasText: `${newTransaction.clientName} - #${newTransaction.clientId}` }).click()
  await page.getByTestId('billing-transaction-submit').click()

  await expect(page.getByTestId('billing-transaction-error')).toContainText(sentence)
  expect(new URL(page.url()).pathname).toBe('/billing/add-transaction')
})
