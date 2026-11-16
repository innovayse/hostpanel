/**
 * Product billing cycles: the operator prices a new product for the cycles added after monthly and
 * annual, the create request carries those amounts under their own cycle and null for the cycles
 * left blank, and the products list shows a column for every cycle.
 */
import { HttpMethod } from '../src/types/http'
import { expect, test } from '@playwright/test'
import { usd } from './fixtures/currencies'
import type { Product } from '../src/types/product'
import { ProductStatus, ProductType } from '../src/types/productenums'
import { fieldOf } from './support/locators'
import { json, stubApi } from './support/stubApi'

/** An empty product group list entry the create dialog can pick. */
const group = { id: 5, name: 'Shared', description: null, isActive: true, productCount: 0 }

/** A product the list shows, so the table with its cycle columns is drawn. */
const listed: Product = {
  id: 1,
  groupId: group.id,
  name: 'Listed plan',
  description: null,
  website: null,
  slug: null,
  packageName: null,
  type: ProductType.SharedHosting,
  status: ProductStatus.Active,
  pricing: { monthly: 4.5, annual: null, quarterly: null, semiAnnual: null, biennial: null },
  prices: [],
  serverGroupId: null,
  isFeatured: false,
  sortOrder: 0,
  sku: null,
  metadata: null,
  gracePeriodDays: 7,
  isDigitalService: false,
  maxActiveServices: null,
  onePerClient: false,
}

test('the operator prices a product for a quarterly and a biennial cycle', async ({ page }) => {
  const api = await stubApi(page, {
    signedIn: true,
    routes: [
      json(HttpMethod.Get, '/api/admin/products', { items: [listed], totalCount: 1, pageNumber: 1, pageSize: 20, totalPages: 1 }),
      json(HttpMethod.Get, '/api/products/groups', [group]),
      json(HttpMethod.Get, '/api/admin/currencies', [usd]),
      json(HttpMethod.Get, '/api/admin/server-groups', []),
      json(HttpMethod.Get, '/api/product-features', []),
      json(HttpMethod.Post, '/api/products', 12, 201),
    ],
  })

  await page.goto('/settings/products')

  const header = page.locator('thead')
  for (const label of ['Monthly', 'Annual', 'Quarterly', 'Semi-annual', 'Biennial']) {
    await expect(header).toContainText(label)
  }

  await page.getByTestId('products-create').click()
  await fieldOf(page, 'product-form-name').fill('Cycle plan')
  await fieldOf(page, 'product-form-price-USD-quarterly').fill('8.5')
  await fieldOf(page, 'product-form-price-USD-biennial').fill('60')
  await page.getByTestId('product-form').evaluate((form) => (form as HTMLFormElement).requestSubmit())

  await expect.poll(() => api.callsTo(HttpMethod.Post, '/api/products').length).toBe(1)
  const body = api.callsTo(HttpMethod.Post, '/api/products')[0]?.body as { prices: unknown[] }
  expect(body.prices).toEqual([
    { currencyCode: 'USD', monthly: null, annual: null, quarterly: 8.5, semiAnnual: null, biennial: 60 },
  ])
})
