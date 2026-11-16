/**
 * Products list flow: the search, the group, type and status filters of the filter dialog and the page are sent to the
 * API and kept in the address, an applied filter is a chip that is removed from its own button, and a list link with a
 * filter opens the filtered list.
 */
import { HttpMethod } from '../src/types/http'
import { expect, test } from '@playwright/test'
import type { Page } from '@playwright/test'
import { usd } from './fixtures/currencies'
import type { PaginatedList } from '../src/types/paginatedlist'
import type { Product } from '../src/types/product'
import { ProductStatus, ProductType } from '../src/types/productenums'
import { fieldOf } from './support/locators'
import { json, stubApi } from './support/stubApi'
import type { StubApi } from './support/stubApi'

/** The list endpoint the screen reads. */
const LIST_PATH = '/api/admin/products'

/** The product group the group filter offers. */
const group = { id: 5, name: 'Shared', description: null, isActive: true, productCount: 2 }

/**
 * Builds a list row.
 *
 * @param id - The product id.
 * @param name - The product name.
 * @param type - The product type.
 * @param status - Whether the product is active.
 * @returns The row.
 */
const productRow = (id: number, name: string, type: ProductType, status: ProductStatus): Product => ({
  id,
  groupId: group.id,
  name,
  description: null,
  website: null,
  slug: null,
  packageName: null,
  type,
  status,
  pricing: { monthly: 4.5, annual: null, quarterly: null, semiAnnual: null, biennial: null },
  prices: [{ currencyCode: 'USD', monthly: 4.5, annual: null, quarterly: null, semiAnnual: null, biennial: null }],
  serverGroupId: null,
  isFeatured: false,
  sortOrder: 0,
  sku: null,
  metadata: null,
  gracePeriodDays: 7,
  isDigitalService: false,
  maxActiveServices: null,
  onePerClient: false,
})

/** An active shared hosting plan. */
const starter = productRow(1, 'Starter', ProductType.SharedHosting, ProductStatus.Active)

/** A hidden VPS, the one the type and status filters must keep. */
const hiddenVps = productRow(2, 'Hidden VPS', ProductType.Vps, ProductStatus.Inactive)

/** Every product the stubbed list holds. */
const allProducts: Product[] = [starter, hiddenVps]

/**
 * Stubs the product list so it narrows by the same query parameters the real API takes.
 *
 * @param page - The page under test.
 * @returns The stubbed API, to read the requests it saw.
 */
const stubProductList = (page: Page): Promise<StubApi> =>
  stubApi(page, {
    signedIn: true,
    routes: [
      {
        method: HttpMethod.Get,
        path: LIST_PATH,
        respond: (call) => {
          const query = new URLSearchParams(call.search)
          const term = query.get('search')?.toLowerCase() ?? ''
          const type = query.get('type')
          const status = query.get('status')
          const items = allProducts.filter(
            (product) =>
              product.name.toLowerCase().includes(term)
              && (type === null || product.type === type)
              && (status === null || product.status === status),
          )
          const body: PaginatedList<Product> = { items, totalCount: items.length, pageNumber: 1, pageSize: 20, totalPages: 1 }
          return { body }
        },
      },
      json(HttpMethod.Get, '/api/products/groups', [group]),
      json(HttpMethod.Get, '/api/admin/currencies', [usd]),
      json(HttpMethod.Get, '/api/admin/server-groups', []),
      json(HttpMethod.Get, '/api/product-features', []),
    ],
  })

test('searching by name sends the search and shows only the matching product', async ({ page }) => {
  const api = await stubProductList(page)

  await page.goto('/settings/products')
  await expect(page.getByTestId('product-row-1')).toBeVisible()
  await expect(page.getByTestId('product-row-2')).toBeVisible()

  // The search box writes to the address after a pause and the list reloads from it.
  await fieldOf(page, 'products-search').fill('hidden')
  await expect(page).toHaveURL(/search=hidden/)

  await expect(page.getByTestId('product-row-2')).toBeVisible()
  await expect(page.getByTestId('product-row-1')).toHaveCount(0)
  expect(api.callsTo(HttpMethod.Get, LIST_PATH).at(-1)?.search).toContain('search=hidden')
})

test('the group, type and status filters are sent to the API, shown as chips and removed from their chips', async ({ page }) => {
  const api = await stubProductList(page)

  await page.goto('/settings/products')
  await expect(page.getByTestId('product-row-1')).toBeVisible()

  await page.getByTestId('filter-trigger').click()
  await page.getByTestId('products-filter-group').getByTestId('ui-select-trigger').click()
  await page.getByTestId('ui-select-option').filter({ hasText: /^Shared$/ }).click()
  await page.getByTestId('products-filter-type').getByTestId('ui-select-trigger').click()
  await page.getByTestId('ui-select-option').filter({ hasText: /^VPS$/ }).click()
  await page.getByTestId('products-filter-status').getByTestId('ui-select-trigger').click()
  await page.getByTestId('ui-select-option').filter({ hasText: /^Inactive$/ }).click()
  await page.getByTestId('filter-apply').click()

  await expect(page).toHaveURL(/group=5/)
  await expect(page).toHaveURL(/type=Vps/)
  await expect(page).toHaveURL(/status=Inactive/)
  await expect(page.getByTestId('filter-chip-group')).toContainText('Shared')
  await expect(page.getByTestId('filter-chip-type')).toContainText('VPS')
  await expect(page.getByTestId('filter-chip-status')).toContainText('Inactive')
  await expect(page.getByTestId('product-row-1')).toHaveCount(0)
  await expect(page.getByTestId('product-row-2')).toBeVisible()
  const sent = api.callsTo(HttpMethod.Get, LIST_PATH).at(-1)?.search
  expect(sent).toContain('groupId=5')
  expect(sent).toContain('type=Vps')
  expect(sent).toContain('status=Inactive')

  await page.getByTestId('filter-chip-remove-type').click()
  await page.getByTestId('filter-chip-remove-status').click()

  await expect(page.getByTestId('filter-chip-type')).toHaveCount(0)
  await expect(page.getByTestId('product-row-1')).toBeVisible()
  expect(api.callsTo(HttpMethod.Get, LIST_PATH).at(-1)?.search).not.toContain('status=')
})

test('a list link with a filter opens the filtered list', async ({ page }) => {
  const api = await stubProductList(page)

  await page.goto('/settings/products?status=Active')

  await expect(page.getByTestId('filter-chip-status')).toContainText('Active')
  await expect(page.getByTestId('product-row-1')).toBeVisible()
  await expect(page.getByTestId('product-row-2')).toHaveCount(0)
  expect(api.callsTo(HttpMethod.Get, LIST_PATH)[0]?.search).toContain('status=Active')
})

test('a filter nothing matches says so and clears from the empty state', async ({ page }) => {
  await stubProductList(page)

  await page.goto('/settings/products?type=Ssl')

  await expect(page.getByTestId('products-empty')).toBeVisible()
  await page.getByTestId('products-clear-empty').click()

  await expect(page.getByTestId('product-row-1')).toBeVisible()
})
