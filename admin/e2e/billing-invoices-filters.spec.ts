/**
 * Invoice lists flow: on the staff invoice list and on the invoices tab of a client's profile, the search and the filters
 * of the filter dialog (client, status and invoice date on the staff list; status and invoice date on the tab) are sent to
 * the API and kept in the address, each applied filter is a chip that is removed from its own button, and a link with
 * filters opens the filtered list.
 */
import { HttpMethod } from '../src/types/http'
import { expect, test } from '@playwright/test'
import type { Page } from '@playwright/test'
import { adaDetail } from './fixtures/clients'
import { invoicesPage, paidInvoice, unpaidInvoice } from './fixtures/invoices'
import type { Invoice } from '../src/types/invoice'
import type { PaginatedList } from '../src/types/paginatedlist'
import { fieldOf } from './support/locators'
import { json, stubApi } from './support/stubApi'
import type { StubApi, StubRoute } from './support/stubApi'

/** The invoices both stubbed lists hold. */
const allInvoices: Invoice[] = [unpaidInvoice, paidInvoice]

/**
 * Stubs an invoice list endpoint so it narrows by the same query parameters the real API takes.
 *
 * @param path - The list's exact path.
 * @returns The stubbed route.
 */
const invoiceList = (path: string): StubRoute => ({
  method: HttpMethod.Get,
  path,
  respond: (call) => {
    const query = new URLSearchParams(call.search)
    const term = query.get('search')?.toLowerCase() ?? ''
    const items = allInvoices.filter(
      (invoice) =>
        String(invoice.id).includes(term)
        && (query.get('status') === null || invoice.status === query.get('status'))
        && (query.get('clientId') === null || String(invoice.clientId) === query.get('clientId')),
    )
    const body: PaginatedList<Invoice> = { ...invoicesPage, items, totalCount: items.length }
    return { body }
  },
})

/**
 * Stubs the staff invoice list and the client picker.
 *
 * @param page - The page under test.
 * @returns The stubbed API, to read the requests it saw.
 */
const stubStaffList = (page: Page): Promise<StubApi> =>
  stubApi(page, {
    signedIn: true,
    routes: [
      invoiceList('/api/billing'),
      json(HttpMethod.Get, '/api/clients', { items: [], totalCount: 0, pageNumber: 1, pageSize: 100, totalPages: 0 }),
    ],
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

test('searching sends the search to the API and shows only the matching invoice', async ({ page }) => {
  const api = await stubStaffList(page)

  await page.goto('/billing/invoices')
  await expect(page.getByTestId(`billing-invoice-select-${unpaidInvoice.id}`)).toBeVisible()
  await expect(page.getByTestId(`billing-invoice-select-${paidInvoice.id}`)).toBeVisible()

  // The search box writes to the address after a pause and the list reloads from it.
  await fieldOf(page, 'billing-invoices-search').fill(String(paidInvoice.id))
  await expect(page).toHaveURL(/search=602/)

  await expect(page.getByTestId(`billing-invoice-select-${paidInvoice.id}`)).toBeVisible()
  await expect(page.getByTestId(`billing-invoice-select-${unpaidInvoice.id}`)).toHaveCount(0)
  await expect.poll(() => api.callsTo(HttpMethod.Get, '/api/billing').at(-1)?.search).toContain('search=602')
})

test('the status filter is sent to the API, shown as a chip and removed from its chip', async ({ page }) => {
  const api = await stubStaffList(page)

  await page.goto('/billing/invoices')
  await expect(page.getByTestId(`billing-invoice-select-${unpaidInvoice.id}`)).toBeVisible()

  await page.getByTestId('billing-invoices-filter').click()
  await chooseOption(page, 'billing-invoices-filter-status', 'Paid')
  await page.getByTestId('filter-apply').click()

  await expect(page).toHaveURL(/status=Paid/)
  await expect(page.getByTestId('filter-chip-status')).toContainText('Paid')
  await expect(page.getByTestId(`billing-invoice-select-${paidInvoice.id}`)).toBeVisible()
  await expect(page.getByTestId(`billing-invoice-select-${unpaidInvoice.id}`)).toHaveCount(0)
  expect(api.callsTo(HttpMethod.Get, '/api/billing').at(-1)?.search).toContain('status=Paid')

  await page.getByTestId('filter-chip-remove-status').click()

  await expect(page.getByTestId('filter-chip-status')).toHaveCount(0)
  expect(api.callsTo(HttpMethod.Get, '/api/billing').at(-1)?.search).not.toContain('status=')
})

test('a list link with a client and a period opens the filtered list', async ({ page }) => {
  const api = await stubStaffList(page)

  await page.goto('/billing/invoices?clientId=8&from=2026-10-01&to=2026-10-31')

  await expect(page.getByTestId('filter-chip-clientId')).toContainText('8')
  await expect(page.getByTestId('filter-chip-from')).toBeVisible()
  await expect(page.getByTestId('filter-chip-to')).toBeVisible()
  await expect(page.getByTestId(`billing-invoice-select-${paidInvoice.id}`)).toBeVisible()
  await expect(page.getByTestId(`billing-invoice-select-${unpaidInvoice.id}`)).toHaveCount(0)
  const sent = api.callsTo(HttpMethod.Get, '/api/billing')[0]?.search ?? ''
  expect(sent).toContain('clientId=8')
  expect(sent).toContain('from=2026-10-01')
  expect(sent).toContain('to=2026-10-31')
})

test('the invoices tab of a client sends its filters to the API and shows them as chips', async ({ page }) => {
  const api = await stubApi(page, {
    signedIn: true,
    routes: [json(HttpMethod.Get, '/api/clients/7', adaDetail), invoiceList('/api/billing/client/7')],
  })

  await page.goto('/clients/7/invoices')
  await expect(page.getByTestId(`client-invoices-select-${unpaidInvoice.id}`)).toBeVisible()

  await page.getByTestId('client-invoices-filter').click()
  await chooseOption(page, 'client-invoices-filter-status', 'Paid')
  await page.getByTestId('filter-apply').click()

  await expect(page).toHaveURL(/status=Paid/)
  await expect(page.getByTestId('filter-chip-status')).toContainText('Paid')
  await expect(page.getByTestId(`client-invoices-select-${paidInvoice.id}`)).toBeVisible()
  await expect(page.getByTestId(`client-invoices-select-${unpaidInvoice.id}`)).toHaveCount(0)
  expect(api.callsTo(HttpMethod.Get, '/api/billing/client/7').at(-1)?.search).toContain('status=Paid')
})
