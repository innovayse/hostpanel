/**
 * Smoke tests for ProductsView: the paged products table with the base-currency prices and the "+other
 * currencies" hint, the search, group, type and status filters and the page kept in the address and sent to the API,
 * the create and edit dialogs, the specification dialog, and save at network level.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import ProductsView from '@/views/ProductsView.vue'
import type { CurrencyDto } from '@/types/currency'
import type { Product, ProductGroup } from '@/types/product'
import { invalid, json, problem } from '../helpers/fetch'
import { applyListFilter } from '../helpers/listFilter'
import { mountRouted, mountScreen } from '../helpers/mount'
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
  isDigitalService: false,
  maxActiveServices: null,
  onePerClient: false,
  trial: { enabled: false, days: 14, autoConvert: true, reminderDays: [3, 1], gracePeriodDays: 3 },
}

/** The address the screen is registered under. */
const PATH = '/settings/products'

/**
 * Wraps rows in the page shape the list endpoint answers with.
 *
 * @param items - The rows of the page.
 * @param totalCount - Matches across all pages; the page length by default.
 * @returns The JSON body.
 */
const page = (items: unknown[], totalCount = items.length) => ({
  items,
  pageNumber: 1,
  pageSize: 20,
  totalCount,
  totalPages: Math.max(1, Math.ceil(totalCount / 20)),
})

/** Answers for everything the screen and its dialogs load. */
const routes = {
  'GET /admin/products': () => json(page([plan, { ...plan, id: 2, name: 'Hidden one', status: 'Inactive', slug: null, type: 'Vps' }])),
  'GET /products/groups': () => json(groups),
  'GET /admin/currencies': () => json([usd, eur]),
  'GET /admin/server-groups': () => json([]),
  'GET /product-features': () => json([]),
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('ProductsView', () => {
  it('keeps the table header in place and draws placeholder rows while the products load', async () => {
    vi.stubGlobal('fetch', vi.fn(() => new Promise<Response>(() => {})))

    const { wrapper } = await mountRouted(ProductsView, '/', '/')

    expect(wrapper.find('thead').exists()).toBe(true)
    expect(wrapper.find('[data-testid="ui-table-skeleton"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="settings-loading"]').exists()).toBe(false)
  })

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
    stubRouteTable({ ...routes, 'GET /admin/products': () => json(page([{ ...plan, type: 'Digital' }])) })
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

  it('sends the trial settings of the edited product through PUT', async () => {
    let sent: unknown
    stubRouteTable({
      ...routes,
      'PUT /products/1': (body) => {
        sent = body
        return new Response(null, { status: 204 })
      },
    })
    const { wrapper } = await mountScreen(ProductsView)

    await wrapper.get('[data-testid="product-actions-1"]').trigger('click')
    await wrapper.get('[data-testid="product-edit-1"]').trigger('click')
    await wrapper.get('[data-testid="product-form-trial-enabled"]').trigger('click')
    await wrapper.get('[data-testid="product-form"]').trigger('submit')

    await vi.waitFor(() => {
      expect(sent).toMatchObject({ id: 1, trial: { enabled: true, days: 14, autoConvert: true, reminderDays: [3, 1], gracePeriodDays: 3 } })
    })
  })

  it('updates an existing product through PUT', async () => {
    const mock = stubRouteTable({ ...routes, 'PUT /products/1': () => new Response(null, { status: 204 }) })
    const { wrapper } = await mountScreen(ProductsView)

    await wrapper.get('[data-testid="product-actions-1"]').trigger('click')
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

    await wrapper.get('[data-testid="product-actions-1"]').trigger('click')
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

    await wrapper.get('[data-testid="product-actions-1"]').trigger('click')
    await wrapper.get('[data-testid="product-spec-1"]').trigger('click')

    await vi.waitFor(() => {
      expect(wrapper.get('[role="dialog"]').text()).toContain('Specification')
    })
  })

  it('says so when there are no products', async () => {
    stubRouteTable({ ...routes, 'GET /admin/products': () => json(page([])) })
    const { wrapper } = await mountScreen(ProductsView)

    expect(wrapper.text()).toContain('No products found')
  })

  it('loads again from the retry of a refused load', async () => {
    let calls = 0
    stubRouteTable({
      ...routes,
      'GET /admin/products': () => {
        calls += 1
        return calls === 1 ? problem('Not allowed.', 403) : json(page([]))
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

describe('ProductsView list', () => {
  /**
   * Mounts the list at an address and waits for its first load.
   *
   * @param url - The address to open, with its query.
   * @returns The wrapper, the router and the stubbed fetch.
   */
  const mountListAt = async (url: string = PATH) => {
    const mock = stubRouteTable(routes)
    const screen = await mountRouted(ProductsView, PATH, url)
    await vi.waitFor(() => {
      expect(screen.wrapper.find('[data-testid="product-row-1"]').exists()).toBe(true)
    })
    return { ...screen, mock }
  }

  /**
   * Lists the list requests the mock has seen.
   *
   * @param mock - The stubbed fetch.
   * @returns The requests that went to the paged list endpoint.
   */
  const listRequests = (mock: ReturnType<typeof vi.fn>): string[] =>
    requestsOf(mock).filter((request) => request.startsWith('GET /admin/products?'))

  it('restores the search, the group, the type, the status and the page from the address and sends them together', async () => {
    const { wrapper, mock } = await mountListAt(`${PATH}?search=starter&group=5&type=Vps&status=Active&page=2`)

    expect(listRequests(mock)[0]).toBe('GET /admin/products?page=2&pageSize=20&search=starter&groupId=5&type=Vps&status=Active')
    expect((wrapper.get('[data-testid="products-search"]').element as HTMLInputElement).value).toBe('starter')
    await vi.waitFor(() => {
      expect(wrapper.get('[data-testid="filter-chip-group"]').text()).toContain('Shared')
    })
    expect(wrapper.get('[data-testid="filter-chip-type"]').text()).toContain('VPS')
    expect(wrapper.get('[data-testid="filter-chip-status"]').text()).toContain('Active')
    expect(wrapper.get('[data-testid="filter-trigger-count"]').text()).toBe('3')
  })

  it('does not send a group, a type or a status the address holds that is not a known value', async () => {
    const { mock } = await mountListAt(`${PATH}?group=abc&type=Nonsense&status=Sideways`)

    expect(listRequests(mock)[0]).toBe('GET /admin/products?page=1&pageSize=20')
  })

  it('puts the typed search in the address and sends it after the pause', async () => {
    const { wrapper, router, mock } = await mountListAt()

    await wrapper.get('[data-testid="products-search"]').setValue('start')
    await new Promise((resolve) => setTimeout(resolve, 450))
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ search: 'start' })
    expect(listRequests(mock).at(-1)).toBe('GET /admin/products?page=1&pageSize=20&search=start')
  })

  it('sends the group, type and status picked in the filter dialog on Apply', async () => {
    const { wrapper, router, mock } = await mountListAt()

    await applyListFilter(wrapper, { 'products-filter-group': '5', 'products-filter-type': 'Digital', 'products-filter-status': 'Inactive' })

    expect(router.currentRoute.value.query).toEqual({ group: '5', type: 'Digital', status: 'Inactive' })
    expect(listRequests(mock).at(-1)).toBe('GET /admin/products?page=1&pageSize=20&groupId=5&type=Digital&status=Inactive')
  })

  it('removes a filter from the address and reloads when its chip is removed', async () => {
    const { wrapper, router, mock } = await mountListAt(`${PATH}?type=Vps&status=Active`)

    await wrapper.get('[data-testid="filter-chip-remove-type"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ status: 'Active' })
    expect(listRequests(mock).at(-1)).toBe('GET /admin/products?page=1&pageSize=20&status=Active')
  })

  it('keeps the chosen page in the address and reads it', async () => {
    const mock = stubRouteTable({ ...routes, 'GET /admin/products': () => json(page([plan], 45)) })
    const { wrapper, router } = await mountRouted(ProductsView, PATH, PATH)
    await flushPromises()

    await wrapper.get('[data-testid="products-pagination-next"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ page: '2' })
    expect(requestsOf(mock).filter((request) => request.startsWith('GET /admin/products?')).at(-1)).toBe('GET /admin/products?page=2&pageSize=20')
    expect(wrapper.text()).toContain('Products: 45')
  })

  it('says so when no product matches the filters and offers to clear them', async () => {
    stubRouteTable({ ...routes, 'GET /admin/products': () => json(page([])) })
    const { wrapper, router } = await mountRouted(ProductsView, PATH, `${PATH}?status=Inactive`)
    await flushPromises()

    expect(wrapper.get('[data-testid="products-empty"]').text()).toContain('No products match your filters')
    await wrapper.get('[data-testid="products-clear-empty"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({})
  })

  it('reads the page the address names again after a product is saved', async () => {
    const mock = stubRouteTable({ ...routes, 'POST /products': () => json(3) })
    const { wrapper } = await mountRouted(ProductsView, PATH, `${PATH}?status=Active`)
    await flushPromises()

    await wrapper.get('[data-testid="products-create"]').trigger('click')
    await wrapper.get('[data-testid="product-form-name"]').setValue('New plan')
    await wrapper.get('[data-testid="product-form"]').trigger('submit')
    await vi.waitFor(() => {
      expect(wrapper.find('[data-testid="product-form"]').exists()).toBe(false)
    })
    await flushPromises()

    const reads = requestsOf(mock).filter((request) => request.startsWith('GET /admin/products?'))
    expect(reads).toHaveLength(2)
    expect(reads.at(-1)).toBe('GET /admin/products?page=1&pageSize=20&status=Active')
  })
})
