/**
 * Mount tests for ProductBundlesView: the table lists the bundles with their products, domain and
 * discount, a refused load offers a retry, creating and editing save through the dialog, and
 * deleting asks first.
 */
import { flushPromises } from '@vue/test-utils'
import { afterEach, describe, expect, it, vi } from 'vitest'
import ProductBundleFormDialog from '@/components/settings/ProductBundleFormDialog.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import type { ProductBundle, ProductBundlePayload } from '@/types/productbundle'
import { BundleDiscountType } from '@/types/productenums'
import ProductBundlesView from '@/views/ProductBundlesView.vue'
import { json, problem } from '../helpers/fetch'
import { mountScreen } from '../helpers/mount'
import { requestsOf, stubRouteTable } from '../helpers/routes'

/** A bundle with a domain for two years and a percentage discount. */
const PERCENT_BUNDLE: ProductBundle = {
  id: 1,
  name: 'Starter pack',
  description: 'Hosting with a domain',
  isActive: true,
  sortOrder: 0,
  includesDomain: true,
  domainYears: 2,
  discountType: BundleDiscountType.Percentage,
  discountAmount: 15,
  discountCurrency: null,
  components: [
    { productId: 1, name: 'Starter', isPrimary: true },
    { productId: 2, name: 'Mailbox', isPrimary: false },
  ],
}

/** A switched-off bundle with a fixed discount and no domain. */
const FIXED_BUNDLE: ProductBundle = {
  id: 2,
  name: 'Mail only',
  description: null,
  isActive: false,
  sortOrder: 1,
  includesDomain: false,
  domainYears: null,
  discountType: BundleDiscountType.Fixed,
  discountAmount: 5,
  discountCurrency: 'EUR',
  components: [{ productId: 2, name: 'Mailbox', isPrimary: true }],
}

/** The form payload a save reports. */
const PAYLOAD: ProductBundlePayload = {
  name: 'Starter pack',
  description: null,
  sortOrder: 0,
  includesDomain: false,
  domainYears: null,
  discountType: BundleDiscountType.None,
  discountAmount: 0,
  discountCurrency: null,
  items: [{ productId: 1, isPrimary: true }],
}

/** Answers for everything the screen loads. */
const routes = {
  'GET /products/bundles': () => json([PERCENT_BUNDLE, FIXED_BUNDLE]),
  'GET /products': () => json([]),
  'GET /admin/currencies': () => json([]),
}

/**
 * Finds the delete confirmation among the dialogs on the page (the bundle form mounts one too).
 *
 * @param wrapper - The mounted view.
 * @returns The confirm-variant dialog.
 */
const confirmDialog = (wrapper: Awaited<ReturnType<typeof mountScreen>>['wrapper']) => {
  const found = wrapper.findAllComponents(UiDialog).find(dialog => dialog.props('variant') === 'confirm')
  if (!found) {
    throw new Error('The delete confirmation is not open.')
  }
  return found
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('ProductBundlesView', () => {
  it('lists the bundles with their products, domain, discount and status', async () => {
    stubRouteTable(routes)
    const { wrapper } = await mountScreen(ProductBundlesView)
    const rows = wrapper.findAll('[data-testid="product-bundle-row"]')

    expect(rows).toHaveLength(2)
    expect(rows[0]?.text()).toContain('Starter pack')
    expect(rows[0]?.text()).toContain('Starter, Mailbox')
    expect(rows[0]?.text()).toContain('2 years')
    expect(rows[0]?.text()).toContain('15%')
    expect(rows[0]?.text()).toContain('Active')
    expect(rows[1]?.text()).toContain('5 EUR')
    expect(rows[1]?.text()).toContain('Inactive')
  })

  it('shows the empty state when there are no bundles', async () => {
    stubRouteTable({ ...routes, 'GET /products/bundles': () => json([]) })
    const { wrapper } = await mountScreen(ProductBundlesView)

    expect(wrapper.text()).toContain('No product bundles yet.')
  })

  it('shows the refusal with a retry that loads again', async () => {
    const mock = stubRouteTable({ ...routes, 'GET /products/bundles': () => problem('Server said no', 500) })
    const { wrapper } = await mountScreen(ProductBundlesView)
    expect(wrapper.text()).toContain('Server said no')

    await wrapper.get('[data-testid="product-bundles-error-retry"]').trigger('click')
    await flushPromises()

    expect(requestsOf(mock).filter(request => request === 'GET /products/bundles')).toHaveLength(2)
  })

  it('creates a bundle through the dialog and closes it', async () => {
    const mock = stubRouteTable({ ...routes, 'POST /products/bundles': () => json(7, 201) })
    const { wrapper } = await mountScreen(ProductBundlesView)

    await wrapper.get('[data-testid="product-bundles-add"]').trigger('click')
    const dialog = wrapper.getComponent(ProductBundleFormDialog)
    expect(dialog.props('open')).toBe(true)
    expect(dialog.props('bundle')).toBeNull()
    dialog.vm.$emit('save', PAYLOAD)
    await flushPromises()

    expect(requestsOf(mock)).toContain('POST /products/bundles')
    expect(wrapper.getComponent(ProductBundleFormDialog).props('open')).toBe(false)
  })

  it('edits a bundle through the dialog with PUT', async () => {
    const mock = stubRouteTable({ ...routes, 'PUT /products/bundles/2': () => new Response(null, { status: 204 }) })
    const { wrapper } = await mountScreen(ProductBundlesView)

    await wrapper.get('[data-testid="product-bundle-actions-2"]').trigger('click')
    await wrapper.get('[data-testid="product-bundle-edit-2"]').trigger('click')
    const dialog = wrapper.getComponent(ProductBundleFormDialog)
    expect(dialog.props('bundle')).toMatchObject({ id: 2, name: 'Mail only' })
    dialog.vm.$emit('save', { ...PAYLOAD, isActive: true })
    await flushPromises()

    expect(requestsOf(mock)).toContain('PUT /products/bundles/2')
    expect(wrapper.getComponent(ProductBundleFormDialog).props('open')).toBe(false)
  })

  it('keeps the dialog open with the refusal when a save is refused', async () => {
    stubRouteTable({ ...routes, 'POST /products/bundles': () => problem('A bundle needs exactly one primary product.', 400) })
    const { wrapper } = await mountScreen(ProductBundlesView)

    await wrapper.get('[data-testid="product-bundles-add"]').trigger('click')
    wrapper.getComponent(ProductBundleFormDialog).vm.$emit('save', PAYLOAD)
    await flushPromises()

    expect(wrapper.getComponent(ProductBundleFormDialog).props('open')).toBe(true)
    expect(wrapper.getComponent(ProductBundleFormDialog).props('error')).toBe('A bundle needs exactly one primary product.')
  })

  it('asks before deleting and deletes on confirm', async () => {
    const mock = stubRouteTable({ ...routes, 'DELETE /products/bundles/1': () => new Response(null, { status: 204 }) })
    const { wrapper } = await mountScreen(ProductBundlesView)

    await wrapper.get('[data-testid="product-bundle-actions-1"]').trigger('click')
    await wrapper.get('[data-testid="product-bundle-delete-1"]').trigger('click')
    const confirm = confirmDialog(wrapper)
    expect(confirm.props('message')).toContain('Starter pack')
    expect(requestsOf(mock)).not.toContain('DELETE /products/bundles/1')

    confirm.vm.$emit('confirm')
    await flushPromises()

    expect(requestsOf(mock)).toContain('DELETE /products/bundles/1')
    expect(wrapper.findAllComponents(UiDialog).some(dialog => dialog.props('variant') === 'confirm')).toBe(false)
  })

  it('shows the sentence of a refused delete', async () => {
    stubRouteTable({ ...routes, 'DELETE /products/bundles/1': () => problem('Bundle not found.', 404) })
    const { wrapper } = await mountScreen(ProductBundlesView)

    await wrapper.get('[data-testid="product-bundle-actions-1"]').trigger('click')
    await wrapper.get('[data-testid="product-bundle-delete-1"]').trigger('click')
    confirmDialog(wrapper).vm.$emit('confirm')
    await flushPromises()

    expect(wrapper.get('[data-testid="product-bundles-action-error"]').text()).toContain('Bundle not found.')
  })
})
