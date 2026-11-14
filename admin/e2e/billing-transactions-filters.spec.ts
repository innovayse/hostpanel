/**
 * Transaction lists flow: on the billing ledger and on the transactions tab of a client's profile, the search and the
 * filters of the filter dialog (client, direction, payment method, period and amount on the ledger; the same without the
 * client on the tab) are sent to the API and kept in the address, the totals shown are the ones the API answers for those
 * filters, each applied filter is a chip that is removed from its own button, and a link with filters opens the filtered
 * list.
 */
import { HttpMethod } from '../src/types/http'
import { expect, test } from '@playwright/test'
import type { Page } from '@playwright/test'
import { adaDetail, clientsPage } from './fixtures/clients'
import { newTransaction, refundTransaction } from './fixtures/transactions'
import type { Transaction, TransactionsResult } from '../src/types/transaction'
import { fieldOf } from './support/locators'
import { json, stubApi } from './support/stubApi'
import type { StubApi, StubRoute } from './support/stubApi'

/** Every entry the stubbed ledgers hold. */
const allTransactions: Transaction[] = [newTransaction, refundTransaction]

/**
 * Stubs a ledger endpoint so it narrows by the same query parameters the real API takes, and totals the rows it keeps.
 *
 * @param path - The ledger's exact path.
 * @returns The stubbed route.
 */
const ledger = (path: string): StubRoute => ({
  method: HttpMethod.Get,
  path,
  respond: (call) => {
    const query = new URLSearchParams(call.search)
    const term = query.get('search')?.toLowerCase() ?? ''
    const items = allTransactions.filter(
      (entry) =>
        entry.description.toLowerCase().includes(term)
        && (query.get('direction') === null || (query.get('direction') === 'In' ? entry.amountIn > 0 : entry.amountOut > 0))
        && (query.get('clientId') === null || String(entry.clientId) === query.get('clientId'))
        && (query.get('amount') === null || [entry.amountIn, entry.amountOut].includes(Number(query.get('amount')))),
    )
    const totalIn = items.reduce((sum, entry) => sum + entry.amountIn, 0)
    const totalOut = items.reduce((sum, entry) => sum + entry.amountOut, 0)
    const body: TransactionsResult = {
      transactions: { items, totalCount: items.length, pageNumber: 1, pageSize: 25, totalPages: 1 },
      totalIn,
      totalOut,
      totalFees: 0,
      balance: totalIn - totalOut,
      currency: 'USD',
    }
    return { body }
  },
})

/**
 * Stubs the billing ledger and the client picker.
 *
 * @param page - The page under test.
 * @returns The stubbed API, to read the requests it saw.
 */
const stubLedger = (page: Page): Promise<StubApi> =>
  stubApi(page, {
    signedIn: true,
    routes: [ledger('/api/transactions'), json(HttpMethod.Get, '/api/clients', clientsPage)],
  })

/**
 * Chooses an option in one of the filter dialog's selects.
 *
 * @param page - The page under test.
 * @param testid - The `data-testid` of the select.
 * @param label - The exact label of the option to choose.
 * @returns Resolves once the option is clicked.
 */
const chooseOption = async (page: Page, testid: string, label: string): Promise<void> => {
  await page.getByTestId(testid).getByTestId('ui-select-trigger').click()
  await page.getByTestId('ui-select-option').filter({ hasText: new RegExp(`^${label}$`) }).click()
}

test('searching sends the search to the API and shows only the matching entry', async ({ page }) => {
  const api = await stubLedger(page)

  await page.goto('/billing/transactions')
  await expect(page.getByTestId('billing-transactions-table')).toContainText(newTransaction.description)
  await expect(page.getByTestId('billing-transactions-table')).toContainText(refundTransaction.description)

  // The search box writes to the address after a pause and the ledger reloads from it.
  await fieldOf(page, 'billing-transactions-search').fill('refund')
  await expect(page).toHaveURL(/search=refund/)

  await expect(page.getByTestId('billing-transactions-table')).not.toContainText(newTransaction.description)
  await expect(page.getByTestId('billing-transactions-table')).toContainText(refundTransaction.description)
  expect(api.callsTo(HttpMethod.Get, '/api/transactions').at(-1)?.search).toContain('search=refund')
})

test('the direction and amount filters are sent to the API, the totals follow them, and a chip removes its filter', async ({ page }) => {
  const api = await stubLedger(page)

  await page.goto('/billing/transactions')
  await expect(page.getByTestId('billing-transactions-total-in')).toContainText('100.00')
  await expect(page.getByTestId('billing-transactions-total-out')).toContainText('40.00')

  await page.getByTestId('billing-transactions-filter').click()
  await chooseOption(page, 'billing-transactions-filter-direction', 'Payments Sent')
  await fieldOf(page, 'billing-transactions-filter-amount').fill('40')
  await page.getByTestId('filter-apply').click()

  await expect(page).toHaveURL(/direction=Out/)
  await expect(page).toHaveURL(/amount=40/)
  await expect(page.getByTestId('filter-chip-direction')).toContainText('Payments Sent')
  await expect(page.getByTestId('filter-chip-amount')).toContainText('40')
  await expect(page.getByTestId('billing-transactions-table')).toContainText(refundTransaction.description)
  await expect(page.getByTestId('billing-transactions-table')).not.toContainText(newTransaction.description)
  // The tiles describe the rows the filters kept, not the whole ledger.
  await expect(page.getByTestId('billing-transactions-total-in')).toContainText('0.00')
  await expect(page.getByTestId('billing-transactions-total-out')).toContainText('40.00')
  const sent = api.callsTo(HttpMethod.Get, '/api/transactions').at(-1)?.search ?? ''
  expect(sent).toContain('direction=Out')
  expect(sent).toContain('amount=40')

  await page.getByTestId('filter-chip-remove-direction').click()

  await expect(page.getByTestId('filter-chip-direction')).toHaveCount(0)
  expect(api.callsTo(HttpMethod.Get, '/api/transactions').at(-1)?.search).not.toContain('direction=')
})

test('a ledger link with filters opens the filtered ledger and names the client on its chip', async ({ page }) => {
  const api = await stubLedger(page)

  await page.goto(`/billing/transactions?clientId=${newTransaction.clientId}&direction=In`)

  await expect(page.getByTestId('filter-chip-direction')).toContainText('Payments Received')
  await expect(page.getByTestId('filter-chip-clientId')).toBeVisible()
  await expect(page.getByTestId('billing-transactions-table')).toContainText(newTransaction.description)
  await expect(page.getByTestId('billing-transactions-table')).not.toContainText(refundTransaction.description)
  const sent = api.callsTo(HttpMethod.Get, '/api/transactions')[0]?.search ?? ''
  expect(sent).toContain(`clientId=${newTransaction.clientId}`)
  expect(sent).toContain('direction=In')
})

test('the transactions tab of a client sends its search and filters to the API and shows them as chips', async ({ page }) => {
  const api = await stubApi(page, {
    signedIn: true,
    routes: [json(HttpMethod.Get, '/api/clients/7', adaDetail), ledger('/api/transactions/client/7')],
  })

  await page.goto('/clients/7/transactions')
  await expect(page.getByTestId('client-transactions-table')).toContainText(newTransaction.description)

  await page.getByTestId('client-transactions-filter').click()
  await chooseOption(page, 'client-transactions-filter-direction', 'Payments Sent')
  await page.getByTestId('filter-apply').click()

  await expect(page).toHaveURL(/direction=Out/)
  await expect(page.getByTestId('filter-chip-direction')).toContainText('Payments Sent')
  await expect(page.getByTestId('client-transactions-table')).toContainText(refundTransaction.description)
  await expect(page.getByTestId('client-transactions-table')).not.toContainText(newTransaction.description)
  expect(api.callsTo(HttpMethod.Get, '/api/transactions/client/7').at(-1)?.search).toContain('direction=Out')
})
