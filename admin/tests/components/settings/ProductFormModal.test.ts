/**
 * Tests for ProductFormModal: create and edit modes, the slug that follows the name until it is
 * edited by hand, the pricing grid built from the configured currencies, and the emitted payload.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import ProductFormModal from '@/components/settings/ProductFormModal.vue'
import type { CurrencyDto } from '@/types/currency'
import type { Product, ProductGroup } from '@/types/product'
import { json } from '../../helpers/fetch'
import { mountScreen } from '../../helpers/mount'
import { stubRouteTable } from '../../helpers/routes'

/** The base currency. */
const usd: CurrencyDto = {
  code: 'USD', numeric: '840', prefix: '$', suffix: '', decimals: 2, rateToBase: 1, isBase: true, isEnabled: true,
}

/** A non-base currency. */
const eur: CurrencyDto = { ...usd, code: 'EUR', numeric: '978', prefix: '€', rateToBase: 0.92, isBase: false }

/** The product groups offered. */
const groups: ProductGroup[] = [
  { id: 5, name: 'Shared', description: null, isActive: true, productCount: 2 },
  { id: 6, name: 'Cloud', description: null, isActive: true, productCount: 0 },
]

/** A stored product sold in the base currency only. */
const stored: Product = {
  id: 9,
  groupId: 6,
  name: 'Pro Plan',
  description: 'The pro plan',
  website: null,
  slug: 'pro-plan',
  packageName: 'pro',
  type: 'Vps',
  status: 'Inactive',
  pricing: { monthly: 10, annual: null },
  prices: [{ currencyCode: 'USD', monthly: 10, annual: null }],
  serverGroupId: null,
  isFeatured: true,
  sortOrder: 4,
}

/** Answers for the two lists the dialog loads on mount. */
const lists = {
  'GET /admin/server-groups': () => json([{ id: 1, name: 'Group A' }]),
  'GET /admin/currencies': () => json([usd, eur]),
}

/**
 * Reads the slug field of the mounted dialog.
 *
 * @param wrapper - The mounted dialog.
 * @returns The slug input's current value.
 */
const slugOf = (wrapper: Awaited<ReturnType<typeof mountScreen>>['wrapper']): string =>
  (wrapper.get('[data-testid="product-form-slug"]').element as HTMLInputElement).value

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('ProductFormModal', () => {
  it('shows a refused save next to the save button', async () => {
    stubRouteTable(lists)
    const { wrapper } = await mountScreen(ProductFormModal, { product: null, groups, saving: false, error: 'That slug is taken.' })

    expect(wrapper.get('[data-testid="product-form-error"]').text()).toBe('That slug is taken.')
    expect(wrapper.get('[data-testid="product-form-error"]').attributes('data-tone')).toBe('danger')
  })

  it('offers the four product types in create mode and starts on shared hosting', async () => {
    stubRouteTable(lists)
    const { wrapper } = await mountScreen(ProductFormModal, { product: null, groups, saving: false })

    expect(wrapper.get('h2').text()).toBe('Create New Product')
    expect(wrapper.get('[data-testid="product-form-type-SharedHosting"]').attributes('aria-pressed')).toBe('true')
    expect(wrapper.find('[data-testid="product-form-type-Vps"]').exists()).toBe(true)
  })

  it('offers a digital product type card that can be chosen', async () => {
    stubRouteTable(lists)
    const { wrapper } = await mountScreen(ProductFormModal, { product: null, groups, saving: false })
    const digital = wrapper.get('[data-testid="product-form-type-Digital"]')

    expect(digital.text()).toContain('Digital')
    await digital.trigger('click')
    expect(digital.attributes('aria-pressed')).toBe('true')
  })

  it('follows the name with a generated slug until the slug is edited by hand', async () => {
    stubRouteTable(lists)
    const { wrapper } = await mountScreen(ProductFormModal, { product: null, groups, saving: false })

    await wrapper.get('[data-testid="product-form-name"]').setValue('My Great Plan!')
    expect(slugOf(wrapper)).toBe('my-great-plan')

    await wrapper.get('[data-testid="product-form-slug"]').setValue('custom')
    await wrapper.get('[data-testid="product-form-name"]').setValue('Another')
    expect(slugOf(wrapper)).toBe('custom')
  })

  it('draws a pricing row per enabled currency, the base always on and the others off', async () => {
    stubRouteTable(lists)
    const { wrapper } = await mountScreen(ProductFormModal, { product: null, groups, saving: false })

    expect(wrapper.find('[data-testid="product-form-price-USD"]').text()).toContain('Base')
    const eurRow = wrapper.get('[data-testid="product-form-price-EUR"]')
    expect(eurRow.get('[role="checkbox"]').attributes('aria-checked')).toBe('false')

    await eurRow.get('[role="checkbox"]').trigger('click')
    expect(eurRow.get('[role="checkbox"]').attributes('aria-checked')).toBe('true')
  })

  it('emits the payload with enabled currencies only and blank cycles as null', async () => {
    stubRouteTable(lists)
    const { wrapper } = await mountScreen(ProductFormModal, { product: null, groups, saving: false })

    await wrapper.get('[data-testid="product-form-type-Vps"]').trigger('click')
    await wrapper.get('[data-testid="product-form-name"]').setValue('Plan')
    await wrapper.get('[data-testid="product-form"]').trigger('submit')

    expect(wrapper.emitted('save')).toEqual([[{
      groupId: 5,
      name: 'Plan',
      description: null,
      website: null,
      slug: 'plan',
      packageName: null,
      type: 'Vps',
      prices: [{ currencyCode: 'USD', monthly: null, annual: null }],
      serverGroupId: null,
      isFeatured: false,
      sortOrder: 0,
    }]])
  })

  it('emits the recommended flag and the sort order the operator set', async () => {
    stubRouteTable(lists)
    const { wrapper } = await mountScreen(ProductFormModal, { product: null, groups, saving: false })

    await wrapper.get('[data-testid="product-form-name"]').setValue('Plan')
    await wrapper.get('[data-testid="product-form-featured"]').trigger('click')
    await wrapper.get('[data-testid="product-form-sort-order"] input').setValue('7')
    await wrapper.get('[data-testid="product-form"]').trigger('submit')

    expect(wrapper.emitted('save')?.[0]?.[0]).toMatchObject({ isFeatured: true, sortOrder: 7 })
  })

  it('opens an existing product with its recommended flag and sort order', async () => {
    stubRouteTable(lists)
    const { wrapper } = await mountScreen(ProductFormModal, { product: stored, groups, saving: false })

    expect(wrapper.get('[data-testid="product-form-featured"]').attributes('aria-checked')).toBe('true')
    expect((wrapper.get('[data-testid="product-form-sort-order"] input').element as HTMLInputElement).value).toBe('4')
  })

  it('opens an existing product without the type cards and with its values', async () => {
    stubRouteTable(lists)
    const { wrapper } = await mountScreen(ProductFormModal, { product: stored, groups, saving: false })

    expect(wrapper.get('h2').text()).toBe('Edit Product')
    expect(wrapper.find('[data-testid="product-form-type-Vps"]').exists()).toBe(false)
    expect((wrapper.get('[data-testid="product-form-name"]').element as HTMLInputElement).value).toBe('Pro Plan')
    expect(wrapper.get('[data-testid="product-form-hidden"]').attributes('aria-checked')).toBe('true')
  })

  it('closes from Cancel and blocks Save while saving', async () => {
    stubRouteTable(lists)
    const { wrapper } = await mountScreen(ProductFormModal, { product: stored, groups, saving: true })

    expect(wrapper.get('[data-testid="product-form-save"]').attributes('disabled')).toBeDefined()
    await wrapper.get('[data-testid="product-form-cancel"]').trigger('click')
    expect(wrapper.emitted('close')).toHaveLength(1)
  })
})
