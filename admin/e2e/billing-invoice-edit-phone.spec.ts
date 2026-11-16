/**
 * Invoice lines at phone width: the editable table is drawn as cards, select-all stays reachable above them, the row
 * checkbox is the card's checkbox, the inputs take the full width of their card and an edit still saves.
 */
import { HttpMethod } from '../src/types/http'
import { expect, test } from '@playwright/test'
import { unpaidInvoiceDetail } from './fixtures/invoices'
import type { InvoiceDetail, InvoiceItemUpdate } from '../src/types/invoice'
import { stubApi } from './support/stubApi'

test.use({ viewport: { width: 390, height: 800 } })

test('the invoice lines are editable cards on a phone', async ({ page }) => {
  // The invoice as the "database" holds it; the items call applies the edits to it.
  const stored: InvoiceDetail = {
    ...unpaidInvoiceDetail,
    items: [
      { id: 1, description: 'Hosting', unitPrice: 120, quantity: 1, amount: 120, sku: null },
      { id: 2, description: 'Setup', unitPrice: 30, quantity: 1, amount: 30, sku: null },
    ],
  }

  const api = await stubApi(page, {
    signedIn: true,
    routes: [
      { method: HttpMethod.Get, path: `/api/billing/${stored.id}`, respond: () => ({ body: stored }) },
      {
        method: HttpMethod.Put,
        path: `/api/billing/${stored.id}/items`,
        respond: (call) => {
          const { items } = call.body as { items: InvoiceItemUpdate[] }
          stored.items = stored.items.map((line) => {
            const entry = items.find((candidate) => candidate.id === line.id)
            return entry === undefined ? line : { ...line, description: entry.description }
          })
          return { status: 204 }
        },
      },
    ],
  })

  await page.goto(`/billing/invoices/${stored.id}/edit`)
  await page.getByTestId('billing-invoice-tab-options').click()

  // Select-all is still reachable, and it ticks the checkbox of every card.
  const selectAll = page.getByTestId('billing-invoice-items-select-all')
  await expect(selectAll).toBeVisible()
  await selectAll.click()
  await expect(page.getByTestId('billing-invoice-item-select-0')).toHaveAttribute('aria-checked', 'true')
  await expect(page.getByTestId('billing-invoice-item-select-1')).toHaveAttribute('aria-checked', 'true')

  // The description input takes the width of its card (the card's padding is all that is left).
  const card = page.getByTestId('billing-invoice-item-description-0').locator('xpath=ancestor::tr[1]')
  const cardWidth = (await card.boundingBox())?.width ?? Number.POSITIVE_INFINITY
  const inputWidth = (await page.getByTestId('billing-invoice-item-description-0').boundingBox())?.width ?? 0
  expect(inputWidth).toBeGreaterThanOrEqual(cardWidth - 40)

  // The caption of the column is real text on the card, and nothing widens the page.
  await expect(card).toContainText('Description')
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true)

  await page.getByTestId('billing-invoice-item-description-0').fill('Managed hosting')
  await page.getByTestId('billing-invoice-items-save').click()

  await expect(page.getByTestId('billing-invoice-items-feedback')).toHaveText('Changes saved successfully.')
  expect(api.callsTo(HttpMethod.Put, `/api/billing/${stored.id}/items`)).toHaveLength(1)
  await expect(page.getByTestId('billing-invoice-item-description-0')).toHaveValue('Managed hosting')
})
