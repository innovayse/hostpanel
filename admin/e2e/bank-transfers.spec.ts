/**
 * Pending bank transfers flow: the screen lists the transfers that wait, confirming asks first and then posts and drops the
 * row, rejecting needs a note and posts it, the empty state shows when nothing waits, and a refused load shows the API sentence.
 */
import { HttpMethod } from '../src/types/http'
import { expect, test } from '@playwright/test'
import { transfersPage, transfersRefusal, transfersRefusalSentence, waitingTransfers } from './fixtures/bankTransfers'
import { json, stubApi } from './support/stubApi'

test('the screen lists the waiting transfers', async ({ page }) => {
  await stubApi(page, {
    signedIn: true,
    routes: [json(HttpMethod.Get, '/api/billing/bank-transfers/pending', transfersPage(waitingTransfers))],
  })

  await page.goto('/billing/bank-transfers')

  await expect(page.getByTestId('billing-bank-transfers-table')).toContainText('Ada Lovelace')
  await expect(page.getByTestId('billing-bank-transfers-table')).toContainText('INV-6')
  await expect(page.getByTestId('billing-bank-transfers-table')).toContainText('$100.00')
  await expect(page.getByTestId('billing-bank-transfer-invoice-5')).toHaveAttribute('href', '/billing/5')
  await expect(page.getByTestId('billing-bank-transfers-count')).toContainText('2 Records Found')
})

test('confirming asks first, posts, and the transfer leaves the list', async ({ page }) => {
  const stored = [...waitingTransfers]
  const api = await stubApi(page, {
    signedIn: true,
    routes: [
      { method: HttpMethod.Get, path: '/api/billing/bank-transfers/pending', respond: () => ({ body: transfersPage(stored) }) },
      {
        method: HttpMethod.Post,
        path: '/api/billing/5/bank-transfer/confirm',
        respond: () => {
          stored.splice(0, 1)
          return { status: 204 }
        },
      },
    ],
  })

  await page.goto('/billing/bank-transfers')
  // Two or more row actions sit behind the row's kebab menu, so it is opened first.
  await page.getByTestId('billing-bank-transfer-actions-5').click()
  await page.getByTestId('billing-bank-transfer-confirm-5').click()
  // Nothing is sent until the dialog is confirmed.
  expect(api.callsTo(HttpMethod.Post, '/api/billing/5/bank-transfer/confirm')).toHaveLength(0)
  await page.getByTestId('billing-bank-transfers-confirm-dialog').getByTestId('dialog-confirm').click()

  await expect(page.getByTestId('billing-bank-transfers-feedback')).toContainText('Invoice #5 is paid.')
  await expect(page.getByTestId('billing-bank-transfer-confirm-5')).toHaveCount(0)
  await expect(page.getByTestId('billing-bank-transfer-invoice-6')).toBeVisible()
})

test('rejecting needs a note and posts it', async ({ page }) => {
  const stored = [...waitingTransfers]
  const api = await stubApi(page, {
    signedIn: true,
    routes: [
      { method: HttpMethod.Get, path: '/api/billing/bank-transfers/pending', respond: () => ({ body: transfersPage(stored) }) },
      {
        method: HttpMethod.Post,
        path: '/api/billing/6/bank-transfer/reject',
        respond: () => {
          stored.splice(1, 1)
          return { status: 204 }
        },
      },
    ],
  })

  await page.goto('/billing/bank-transfers')
  // Two or more row actions sit behind the row's kebab menu, so it is opened first.
  await page.getByTestId('billing-bank-transfer-actions-6').click()
  await page.getByTestId('billing-bank-transfer-reject-6').click()
  await page.getByTestId('billing-bank-transfers-reject-dialog').getByTestId('dialog-confirm').click()
  await expect(page.getByTestId('billing-bank-transfers-note-error')).toBeVisible()
  expect(api.callsTo(HttpMethod.Post, '/api/billing/6/bank-transfer/reject')).toHaveLength(0)

  await page.getByTestId('billing-bank-transfers-note').fill('Nothing arrived')
  await page.getByTestId('billing-bank-transfers-reject-dialog').getByTestId('dialog-confirm').click()

  await expect(page.getByTestId('billing-bank-transfers-feedback')).toContainText('Invoice #6 is open again')
  expect(api.callsTo(HttpMethod.Post, '/api/billing/6/bank-transfer/reject')[0]?.body).toEqual({ note: 'Nothing arrived' })
  await expect(page.getByTestId('billing-bank-transfer-reject-6')).toHaveCount(0)
})

test('the screen shows the empty state when nothing waits', async ({ page }) => {
  await stubApi(page, {
    signedIn: true,
    routes: [json(HttpMethod.Get, '/api/billing/bank-transfers/pending', transfersPage([]))],
  })

  await page.goto('/billing/bank-transfers')

  await expect(page.getByTestId('billing-bank-transfers-table')).toContainText('No bank transfers are waiting.')
  await expect(page.getByTestId('billing-bank-transfers-count')).toContainText('0 Records Found')
})

test('a refused load shows the API sentence and no empty state or count', async ({ page }) => {
  await stubApi(page, {
    signedIn: true,
    routes: [json(HttpMethod.Get, '/api/billing/bank-transfers/pending', transfersRefusal, 403)],
  })

  await page.goto('/billing/bank-transfers')

  await expect(page.getByTestId('billing-bank-transfers-error')).toContainText(transfersRefusalSentence)
  await expect(page.getByTestId('billing-bank-transfers-table')).not.toContainText('No bank transfers are waiting.')
  await expect(page.getByTestId('billing-bank-transfers-count')).toHaveCount(0)
})
