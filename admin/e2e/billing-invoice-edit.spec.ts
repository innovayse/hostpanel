/**
 * Invoice edit flow: in manage mode an operator removes a line and edits another on the Options
 * tab, saves, and the stub "database" holds the edits, the screen confirms the save and shows the
 * reloaded lines.
 */
import { HttpMethod } from '../src/types/http'
import { expect, test } from '@playwright/test'
import { unpaidInvoiceDetail } from './fixtures/invoices'
import type { InvoiceDetail, InvoiceItemUpdate } from '../src/types/invoice'
import { stubApi } from './support/stubApi'

test('saving the edited lines sends the edits and the screen shows the stored result', async ({ page }) => {
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
          stored.items = stored.items
            .filter((line) => !items.some((entry) => entry.id === line.id && entry.isDeleted))
            .map((line) => {
              const entry = items.find((candidate) => candidate.id === line.id)
              return entry === undefined
                ? line
                : { ...line, description: entry.description, unitPrice: entry.unitPrice, amount: entry.unitPrice * entry.quantity }
            })
          return { status: 204 }
        },
      },
    ],
  })

  await page.goto(`/billing/invoices/${stored.id}/edit`)
  await page.getByTestId('billing-invoice-tab-options').click()
  await page.getByTestId('billing-invoice-item-remove-1').click()
  await page.getByTestId('billing-invoice-item-description-0').fill('Managed hosting')
  await page.getByTestId('billing-invoice-items-save').click()

  await expect(page.getByTestId('billing-invoice-items-feedback')).toHaveText('Changes saved successfully.')
  expect(api.callsTo(HttpMethod.Put, `/api/billing/${stored.id}/items`)[0]?.body).toEqual({
    items: [
      { id: 1, description: 'Managed hosting', unitPrice: 120, quantity: 1, isDeleted: false },
      { id: 2, description: '', unitPrice: 0, quantity: 1, isDeleted: true },
    ],
  })
  await expect(page.getByTestId('billing-invoice-item-description-0')).toHaveValue('Managed hosting')
  await expect(page.getByTestId('billing-invoice-item-description-1')).toHaveCount(0)
})
