/**
 * Mount tests for ProductBundleFormDialog: the form starts with one empty component row for a new
 * bundle and from the bundle being edited, keeps exactly one primary component, leaves domain
 * products and already picked products out of the pickers, reports the finished payload (with the
 * fields the picked options make meaningless sent empty), blocks a save without a name or a
 * primary product and shows the API's messages.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import type { VueWrapper } from '@vue/test-utils'
import ProductBundleFormDialog from '@/components/settings/ProductBundleFormDialog.vue'
import type { CurrencyDto } from '@/types/currency'
import type { Product } from '@/types/product'
import type { ProductBundle } from '@/types/productbundle'
import { BundleDiscountType, ProductType } from '@/types/productenums'
import { createTestI18n } from '../../helpers/i18n'

/**
 * Builds a catalogue product with only the fields the pickers read filled in.
 *
 * @param id - Product id.
 * @param name - Product name.
 * @param type - Product type.
 * @returns The product.
 */
const product = (id: number, name: string, type: ProductType): Product =>
  ({ id, name, type, status: 'Active' }) as Product

/** The catalogue the pickers offer: two sellable products and a domain product. */
const PRODUCTS: Product[] = [
  product(1, 'Starter', ProductType.SharedHosting),
  product(2, 'Mailbox', ProductType.Email),
  product(3, '.com', ProductType.Domain),
]

/** The configured currencies. */
const CURRENCIES: CurrencyDto[] = [
  { code: 'USD', numeric: '840', prefix: '$', suffix: '', decimals: 2, rateToBase: 1, isBase: true, isEnabled: true },
  { code: 'EUR', numeric: '978', prefix: '€', suffix: '', decimals: 2, rateToBase: 0.92, isBase: false, isEnabled: true },
]

/** A stored bundle with two products, a domain for three years and a fixed discount. */
const BUNDLE: ProductBundle = {
  id: 4,
  name: 'Starter pack',
  description: 'Hosting with mail',
  isActive: false,
  sortOrder: 2,
  includesDomain: true,
  domainYears: 3,
  discountType: BundleDiscountType.Fixed,
  discountAmount: 5,
  discountCurrency: 'EUR',
  components: [
    { productId: 2, name: 'Mailbox', isPrimary: true },
    { productId: 1, name: 'Starter', isPrimary: false },
  ],
}

/**
 * Mounts the dialog.
 *
 * @param props - Props that replace the defaults.
 * @returns The wrapper.
 */
const mountDialog = (props: Record<string, unknown> = {}) =>
  mount(ProductBundleFormDialog, {
    props: {
      open: true,
      bundle: null,
      products: PRODUCTS,
      currencies: CURRENCIES,
      saving: false,
      error: null,
      fieldErrors: {},
      ...props,
    },
    global: { plugins: [createTestI18n()], stubs: { teleport: true } },
  })

/**
 * Picks an option of the nth component product picker.
 *
 * @param wrapper - The mounted dialog.
 * @param row - Zero-based component row.
 * @param label - Text of the option to pick.
 * @returns Promise that resolves when the pick has rendered.
 */
const pickProduct = async (wrapper: VueWrapper, row: number, label: string): Promise<void> => {
  const picker = wrapper.findAll('[data-testid="product-bundle-form-product"]')[row]
  await picker?.get('[data-testid="ui-select-trigger"]').trigger('click')
  const option = picker?.findAll('[data-testid="ui-select-option"]').find(candidate => candidate.text() === label)
  await option?.trigger('mousedown')
}

describe('ProductBundleFormDialog', () => {
  it('starts a new bundle with one empty component row and Save disabled', () => {
    const wrapper = mountDialog()

    expect(wrapper.findAll('[data-testid="product-bundle-form-row"]')).toHaveLength(1)
    expect(wrapper.get('[data-testid="product-bundle-form-save"]').attributes('disabled')).toBeDefined()
    expect(wrapper.find('[data-testid="product-bundle-form-active"]').exists()).toBe(false)
  })

  it('offers sellable products only and drops a product another row already holds', async () => {
    const wrapper = mountDialog()
    await pickProduct(wrapper, 0, 'Starter')
    await wrapper.get('[data-testid="product-bundle-form-add-component"]').trigger('click')
    await wrapper.findAll('[data-testid="product-bundle-form-product"]')[1]?.get('[data-testid="ui-select-trigger"]').trigger('click')

    const options = wrapper.findAll('[data-testid="product-bundle-form-product"]')[1]?.findAll('[data-testid="ui-select-option"]')
    expect(options?.map(option => option.text())).toEqual(['Mailbox'])
  })

  it('reports the typed fields with the first picked product as the primary one', async () => {
    const wrapper = mountDialog()

    await wrapper.get('[data-testid="product-bundle-form-name"]').setValue('  Starter pack ')
    await wrapper.get('[data-testid="product-bundle-form-description"]').setValue('Hosting with mail')
    await pickProduct(wrapper, 0, 'Starter')
    await wrapper.get('[data-testid="product-bundle-form-save"]').trigger('click')

    expect(wrapper.emitted('save')).toEqual([
      [{
        name: 'Starter pack',
        description: 'Hosting with mail',
        sortOrder: 0,
        includesDomain: false,
        domainYears: null,
        discountType: BundleDiscountType.None,
        discountAmount: 0,
        discountCurrency: null,
        items: [{ productId: 1, isPrimary: true }],
        isActive: undefined,
      }],
    ])
  })

  it('moves the primary mark to the chosen row and to the first row when the primary row is removed', async () => {
    const wrapper = mountDialog({ bundle: BUNDLE })
    const marks = (): boolean[] =>
      wrapper.findAll('[data-testid="product-bundle-form-primary"]').map(radio => (radio.element as HTMLInputElement).checked)

    expect(marks()).toEqual([true, false])

    await wrapper.findAll('[data-testid="product-bundle-form-primary"]')[1]?.trigger('change')
    expect(marks()).toEqual([false, true])

    await wrapper.findAll('[data-testid="product-bundle-form-remove"]')[1]?.trigger('click')
    expect(marks()).toEqual([true])
  })

  it('starts from the bundle being edited and reports it, active switch included', async () => {
    const wrapper = mountDialog({ bundle: BUNDLE })

    await wrapper.get('[data-testid="product-bundle-form-active"]').trigger('click')
    await wrapper.get('[data-testid="product-bundle-form-save"]').trigger('click')

    expect(wrapper.emitted('save')).toEqual([
      [{
        name: 'Starter pack',
        description: 'Hosting with mail',
        sortOrder: 2,
        includesDomain: true,
        domainYears: 3,
        discountType: BundleDiscountType.Fixed,
        discountAmount: 5,
        discountCurrency: 'EUR',
        items: [{ productId: 2, isPrimary: true }, { productId: 1, isPrimary: false }],
        isActive: true,
      }],
    ])
  })

  it('sends no years, amount or currency once the domain and the discount are switched off', async () => {
    const wrapper = mountDialog({ bundle: BUNDLE })

    await wrapper.get('[data-testid="product-bundle-form-includes-domain"]').trigger('click')
    await wrapper.get('[data-testid="product-bundle-form-discount-type"]').get('[data-testid="ui-select-trigger"]').trigger('click')
    const none = wrapper
      .get('[data-testid="product-bundle-form-discount-type"]')
      .findAll('[data-testid="ui-select-option"]')
      .find(option => option.text() === 'No discount')
    await none?.trigger('mousedown')
    await wrapper.get('[data-testid="product-bundle-form-save"]').trigger('click')

    expect(wrapper.find('[data-testid="product-bundle-form-domain-years"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="product-bundle-form-discount-amount"]').exists()).toBe(false)
    expect(wrapper.emitted('save')?.[0]?.[0]).toMatchObject({
      includesDomain: false,
      domainYears: null,
      discountType: BundleDiscountType.None,
      discountAmount: 0,
      discountCurrency: null,
    })
  })

  it('asks for a currency only for a fixed discount', async () => {
    const wrapper = mountDialog({ bundle: { ...BUNDLE, discountType: BundleDiscountType.Percentage, discountCurrency: null } })

    expect(wrapper.find('[data-testid="product-bundle-form-discount-amount"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="product-bundle-form-discount-currency"]').exists()).toBe(false)
  })

  it('shows the refusal sentence and the field message', () => {
    const wrapper = mountDialog({ error: 'Refused.', fieldErrors: { name: 'Name is taken.' } })

    expect(wrapper.text()).toContain('Refused.')
    expect(wrapper.text()).toContain('Name is taken.')
  })

  it('emits close from Cancel', async () => {
    const wrapper = mountDialog()

    await wrapper.get('[data-testid="product-bundle-form-cancel"]').trigger('click')

    expect(wrapper.emitted('close')).toHaveLength(1)
  })
})
