/**
 * Tests for ProductFeaturesModal: the product's specification lines through the store at network
 * level: loading, the empty state, adding, removing and a refusal.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import ProductFeaturesModal from '@/components/settings/ProductFeaturesModal.vue'
import type { Product } from '@/types/product'
import type { ProductFeature } from '@/types/productfeature'
import { json, problem } from '../../helpers/fetch'
import { mountScreen } from '../../helpers/mount'
import { requestsOf, stubRouteTable } from '../../helpers/routes'

/** The product being specified. */
const product: Product = {
  id: 1,
  groupId: 1,
  name: 'Starter',
  description: null,
  website: null,
  slug: null,
  packageName: null,
  type: 'SharedHosting',
  status: 'Active',
  pricing: { monthly: null, annual: null },
  prices: [],
  serverGroupId: null,
  isFeatured: false,
  sortOrder: 0,
  sku: null,
  metadata: null,
  gracePeriodDays: 7,
}

/** Two stored lines. */
const lines: ProductFeature[] = [
  { id: 7, productId: 1, label: 'Disk', value: '10 GB', sortOrder: 0 },
  { id: 8, productId: 1, label: 'RAM', value: '1 GB', sortOrder: 1 },
]

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('ProductFeaturesModal', () => {
  it('names the product and lists its lines', async () => {
    stubRouteTable({ 'GET /product-features': () => json(lines) })
    const { wrapper } = await mountScreen(ProductFeaturesModal, { product })

    expect(wrapper.text()).toContain('Starter')
    expect((wrapper.get('[data-testid="product-feature-label-7"]').element as HTMLInputElement).value).toBe('Disk')
    expect((wrapper.get('[data-testid="product-feature-value-8"]').element as HTMLInputElement).value).toBe('1 GB')
  })

  it('explains an empty specification', async () => {
    stubRouteTable({ 'GET /product-features': () => json([]) })
    const { wrapper } = await mountScreen(ProductFeaturesModal, { product })

    expect(wrapper.get('[data-testid="product-features-empty"]').text()).toContain('Nothing specified yet')
    expect(wrapper.find('[data-testid="product-features-list"]').exists()).toBe(false)
  })

  it('adds a line at the end and reloads the list', async () => {
    const mock = stubRouteTable({
      'GET /product-features': () => json(lines),
      'POST /product-features': () => new Response(null, { status: 204 }),
    })
    const { wrapper } = await mountScreen(ProductFeaturesModal, { product })
    const add = wrapper.get('[data-testid="product-feature-add"]')
    expect(add.attributes('disabled')).toBeDefined()

    await wrapper.get('[data-testid="product-feature-new-label"]').setValue('CPU')
    await wrapper.get('[data-testid="product-feature-new-value"]').setValue('2 cores')
    await add.trigger('click')
    await vi.waitFor(() => {
      expect(requestsOf(mock).filter((call) => call.startsWith('GET /product-features'))).toHaveLength(2)
    })

    const post = mock.mock.calls.find((call) => (call[1] as RequestInit | undefined)?.method === 'POST')
    expect(JSON.parse((post?.[1] as RequestInit | undefined)?.body as string)).toEqual({ productId: 1, label: 'CPU', value: '2 cores', sortOrder: 2 })
  })

  it('removes a line and reloads', async () => {
    const mock = stubRouteTable({
      'GET /product-features': () => json(lines),
      'DELETE /product-features/7': () => new Response(null, { status: 204 }),
    })
    const { wrapper } = await mountScreen(ProductFeaturesModal, { product })

    await wrapper.get('[data-testid="product-feature-remove-7"]').trigger('click')
    expect(requestsOf(mock)).not.toContain('DELETE /product-features/7')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await vi.waitFor(() => {
      expect(requestsOf(mock)).toContain('DELETE /product-features/7')
    })
    await vi.waitFor(() => {
      expect(wrapper.get('[data-testid="product-features-error"]').text()).toBe('Record deleted.')
    })
  })

  it('keeps the line when the removal is dismissed or refused', async () => {
    const mock = stubRouteTable({
      'GET /product-features': () => json(lines),
      'DELETE /product-features/7': () => problem('Line is locked.', 409),
    })
    const { wrapper } = await mountScreen(ProductFeaturesModal, { product })

    await wrapper.get('[data-testid="product-feature-remove-7"]').trigger('click')
    await wrapper.get('[data-testid="dialog-cancel"]').trigger('click')
    expect(requestsOf(mock)).not.toContain('DELETE /product-features/7')

    await wrapper.get('[data-testid="product-feature-remove-7"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await vi.waitFor(() => {
      expect(wrapper.get('[data-testid="product-features-error"]').text()).toBe('Line is locked.')
    })
    expect(wrapper.find('[data-testid="product-feature-label-7"]').exists()).toBe(true)
  })

  it('offers a retry when the lines cannot be loaded', async () => {
    let calls = 0
    stubRouteTable({
      'GET /product-features': () => {
        calls += 1
        return calls === 1 ? problem('No such product.', 404) : json(lines)
      },
    })
    const { wrapper } = await mountScreen(ProductFeaturesModal, { product })

    await vi.waitFor(() => {
      expect(wrapper.get('[data-testid="product-features-load-error"]').text()).toBe('No such product.')
    })
    await wrapper.get('[data-testid="product-features-load-error-retry"]').trigger('click')

    await vi.waitFor(() => {
      expect(wrapper.find('[data-testid="product-feature-label-7"]').exists()).toBe(true)
    })
  })

  it('shows the API sentence when adding is refused', async () => {
    stubRouteTable({
      'GET /product-features': () => json(lines),
      'POST /product-features': () => problem('That name is already used for this product.', 400),
    })
    const { wrapper } = await mountScreen(ProductFeaturesModal, { product })

    await wrapper.get('[data-testid="product-feature-new-label"]').setValue('Disk')
    await wrapper.get('[data-testid="product-feature-new-value"]').setValue('20 GB')
    await wrapper.get('[data-testid="product-feature-add"]').trigger('click')

    await vi.waitFor(() => {
      expect(wrapper.get('[data-testid="product-features-error"]').text()).toBe('That name is already used for this product.')
    })
  })

  it('closes from the dialog header', async () => {
    stubRouteTable({ 'GET /product-features': () => json([]) })
    const { wrapper } = await mountScreen(ProductFeaturesModal, { product })

    await wrapper.get('[data-testid="dialog-close"]').trigger('click')

    expect(wrapper.emitted('close')).toHaveLength(1)
  })
})
