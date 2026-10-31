/**
 * Smoke tests for ProductsView: the products table with the base-currency prices and the "+other
 * currencies" hint, the create and edit dialogs, the specification dialog, and save at network level.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import ProductsView from '@/views/ProductsView.vue'
import type { CurrencyDto } from '@/types/currency'
import type { Product, ProductGroup } from '@/types/product'
import { invalid, json, problem } from '../helpers/fetch'
import { mountScreen } from '../helpers/mount'
import { requestsOf, stubRouteTable } from '../helpers/routes'

/** The base currency. */
const usd: CurrencyDto = {
  code: 'USD', numeric: '840', prefix: '$', suffix: '', decimals: 2, rateToBase: 1, isBase: true, isEnabled: true,
}

/** A second currency a product can be sold in. */
const eur: CurrencyDto = { ...usd, code: 'EUR', prefix: '€', rateToBase: 0.92, isBase: false }

/** The product groups. */
const groups: ProductGroup[] = [{ id: 5, name: 'Shared', description: null, isActive: true, productCount: 2 }]

/** An active product sold in dollars and euros. */
const plan: Product = {
  id: 1,
  groupId: 5,
  name: 'Starter',
  description: null,
  website: null,
  slug: 'starter',
  packageName: null,
  type: 'SharedHosting',
  status: 'Active',
  pricing: { monthly: 4.5, annual: null, quarterly: 12, semiAnnual: null, biennial: null },
  prices: [
    { currencyCode: 'USD', monthly: 4.5, annual: null, quarterly: 12, semiAnnual: null, biennial: null },
    { currencyCode: 'EUR', monthly: 4.1, annual: null, quarterly: null, semiAnnual: null, biennial: null },
  ],
  serverGroupId: null,
  isFeatured: false,
  sortOrder: 0,
  sku: null,
  metadata: null,
  gracePeriodDays: 7,
}

/** Answers for everything the screen and its dialogs load. */
const routes = {
  'GET /products': () => json([plan, { ...plan, id: 2, name: 'Hidden one', status: 'Inactive', slug: null, type: 'Vps' }]),
  'GET /products/groups': () => json(groups),
  'GET /admin/currencies': () => json([usd, eur]),
  'GET /admin/server-groups': () => json([]),
  'GET /product-features': () => json([]),
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('ProductsView', () => {
  it('lists the products with group, type, base-currency price and status', async () => {
    stubRouteTable(routes)
    const { wrapper } = await mountScreen(ProductsView)
    const row = wrapper.get('[data-testid="product-row-1"]').text()

    expect(wrapper.get('h1').text()).toBe('Products')
    expect(row).toContain('Starter')
    expect(row).toContain('/starter')
    expect(row).toContain('Shared')
    expect(row).toContain('Shared Hosting')
    expect(row).toContain('$4.50')
    expect(row).toContain('+EUR')
    expect(row).toContain('Active')
    expect(wrapper.get('[data-testid="product-row-2"]').text()).toContain('Inactive')
  })

  it('shows a column per billing cycle and the price of a product on a newer cycle', async () => {
    stubRouteTable(routes)
    const { wrapper } = await mountScreen(ProductsView)
    const header = wrapper.get('thead').text()

    for (const label of ['Monthly', 'Annual', 'Quarterly', 'Semi-annual', 'Biennial']) {
      expect(header).toContain(label)
    }
    expect(wrapper.get('[data-testid="product-row-1"]').text()).toContain('$12.00')
  })

  it('labels a digital product with its type', async () => {
    stubRouteTable({ ...routes, 'GET /products': () => json([{ ...plan, type: 'Digital' }]) })
    const { wrapper } = await mountScreen(ProductsView)

    expect(wrapper.get('[data-testid="product-row-1"]').text()).toContain('Digital')
  })

  it('opens the create dialog and closes it from Cancel', async () => {
    stubRouteTable(routes)
    const { wrapper } = await mountScreen(ProductsView)

    await wrapper.get('[data-testid="products-create"]').trigger('click')
    expect(wrapper.find('[data-testid="product-form"]').exists()).toBe(true)

    await wrapper.get('[data-testid="product-form-cancel"]').trigger('click')
    expect(wrapper.find('[data-testid="product-form"]').exists()).toBe(false)
  })

  it('creates a product through POST and closes the dialog', async () => {
    const mock = stubRouteTable({ ...routes, 'POST /products': () => json(3) })
    const { wrapper } = await mountScreen(ProductsView)

    await wrapper.get('[data-testid="products-create"]').trigger('click')
    await wrapper.get('[data-testid="product-form-name"]').setValue('New plan')
    await wrapper.get('[data-testid="product-form"]').trigger('submit')
    await vi.waitFor(() => {
      expect(wrapper.find('[data-testid="product-form"]').exists()).toBe(false)
    })

    expect(requestsOf(mock)).toContain('POST /products')
  })

  it('updates an existing product through PUT', async () => {
    const mock = stubRouteTable({ ...routes, 'PUT /products/1': () => new Response(null, { status: 204 }) })
    const { wrapper } = await mountScreen(ProductsView)

    await wrapper.get('[data-testid="product-row-1"]').get('[data-testid="row-actions-trigger"]').trigger('click')
    await wrapper.get('[data-testid="product-edit-1"]').trigger('click')
    expect((wrapper.get('[data-testid="product-form-name"]').element as HTMLInputElement).value).toBe('Starter')
    await wrapper.get('[data-testid="product-form"]').trigger('submit')
    await vi.waitFor(() => {
      expect(requestsOf(mock)).toContain('PUT /products/1')
    })
    await vi.waitFor(() => {
      expect(wrapper.get('[data-testid="products-feedback"]').text()).toBe('Changes saved successfully.')
    })
  })

  it('sends the SKU, metadata and grace period of the edited product and shows the API field errors under the inputs', async () => {
    let sent: unknown
    stubRouteTable({
      ...routes,
      'PUT /products/1': (body) => {
        sent = body
        return invalid('Validation failed.', { Sku: ['Use letters and digits.'], GracePeriodDays: ['Out of range.'] })
      },
    })
    const { wrapper } = await mountScreen(ProductsView)

    await wrapper.get('[data-testid="product-row-1"]').get('[data-testid="row-actions-trigger"]').trigger('click')
    await wrapper.get('[data-testid="product-edit-1"]').trigger('click')
    await wrapper.get('[data-testid="product-form-sku"]').setValue(' bad sku ')
    await wrapper.get('[data-testid="product-form"]').trigger('submit')

    await vi.waitFor(() => {
      expect(wrapper.text()).toContain('Use letters and digits.')
    })
    expect(sent).toMatchObject({ id: 1, sku: 'bad sku', metadata: '', gracePeriodDays: 7 })
    expect(wrapper.text()).toContain('Out of range.')

    // Editing the SKU clears only its own message (the view wires clear-field-error to the store).
    await wrapper.get('[data-testid="product-form-sku"]').setValue('good-sku')
    expect(wrapper.text()).not.toContain('Use letters and digits.')
    expect(wrapper.text()).toContain('Out of range.')
  })

  it('shows the API sentence next to the save button when a save is refused', async () => {
    stubRouteTable({ ...routes, 'POST /products': () => problem('That slug is taken.', 400) })
    const { wrapper } = await mountScreen(ProductsView)

    await wrapper.get('[data-testid="products-create"]').trigger('click')
    await wrapper.get('[data-testid="product-form-name"]').setValue('Dup')
    await wrapper.get('[data-testid="product-form"]').trigger('submit')

    await vi.waitFor(() => {
      expect(wrapper.get('[data-testid="product-form-error"]').text()).toBe('That slug is taken.')
    })
    expect(wrapper.find('[data-testid="product-form"]').exists()).toBe(true)
  })

  it('opens the specification dialog for a product', async () => {
    stubRouteTable(routes)
    const { wrapper } = await mountScreen(ProductsView)

    await wrapper.get('[data-testid="product-row-1"]').get('[data-testid="row-actions-trigger"]').trigger('click')
    await wrapper.get('[data-testid="product-spec-1"]').trigger('click')

    await vi.waitFor(() => {
      expect(wrapper.get('[role="dialog"]').text()).toContain('Specification')
    })
  })

  it('says so when there are no products', async () => {
    stubRouteTable({ ...routes, 'GET /products': () => json([]) })
    const { wrapper } = await mountScreen(ProductsView)

    expect(wrapper.text()).toContain('No products found')
  })

  it('loads again from the retry of a refused load', async () => {
    let calls = 0
    stubRouteTable({
      ...routes,
      'GET /products': () => {
        calls += 1
        return calls === 1 ? problem('Not allowed.', 403) : json([])
      },
    })
    const { wrapper } = await mountScreen(ProductsView)

    await vi.waitFor(() => {
      expect(wrapper.get('[data-testid="products-error"]').text()).toBe('Not allowed.')
    })
    await wrapper.get('[data-testid="products-error-retry"]').trigger('click')

    await vi.waitFor(() => {
      expect(wrapper.text()).toContain('No products found')
    })
  })
})
