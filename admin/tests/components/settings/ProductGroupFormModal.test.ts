/**
 * Tests for ProductGroupFormModal: the multiple choice of payment methods a group may be paid with, the payload it
 * emits (none ticked allows every method), a method the checkout no longer offers staying ticked.
 */
import { describe, expect, it } from 'vitest'
import ProductGroupFormModal from '@/components/settings/ProductGroupFormModal.vue'
import type { PaymentMethodOption, ProductGroup } from '@/types/product'
import { mountScreen } from '../../helpers/mount'

/** The methods the checkout offers. */
const methods: PaymentMethodOption[] = [
  { module: 'bank_transfer', displayName: 'Bank transfer' },
  { module: 'gateway-a', displayName: 'Bank Card (Gateway A)' },
]

/** A stored group restricted to the gateway and to one the checkout no longer offers. */
const stored: ProductGroup = {
  id: 5, name: 'Shared', description: 'Hosting', isActive: true, productCount: 2, allowedPaymentMethods: ['gateway-a', 'retired-gateway'],
}

describe('ProductGroupFormModal', () => {
  it('lists every offered method, none ticked, when creating', async () => {
    const { wrapper } = await mountScreen(ProductGroupFormModal, { group: null, methods, saving: false })

    expect(wrapper.get('h2').text()).toBe('Create product group')
    expect(wrapper.get('[data-testid="product-group-form-method-bank_transfer"]').attributes('aria-checked')).toBe('false')
    expect(wrapper.get('[data-testid="product-group-form-method-gateway-a"]').attributes('aria-checked')).toBe('false')
    expect(wrapper.get('[data-testid="product-group-form-methods"]').text()).toContain('Bank Card (Gateway A)')
  })

  it('emits an empty allowed list when nothing is ticked, so every method stays allowed', async () => {
    const { wrapper } = await mountScreen(ProductGroupFormModal, { group: null, methods, saving: false })

    await wrapper.get('[data-testid="product-group-form-name"]').setValue(' Shared ')
    await wrapper.get('[data-testid="product-group-form"]').trigger('submit')

    expect(wrapper.emitted('save')).toEqual([[{ name: 'Shared', description: null, allowedPaymentMethods: [] }]])
  })

  it('emits the ticked methods and unticking removes one again', async () => {
    const { wrapper } = await mountScreen(ProductGroupFormModal, { group: null, methods, saving: false })

    await wrapper.get('[data-testid="product-group-form-name"]').setValue('Shared')
    await wrapper.get('[data-testid="product-group-form-method-bank_transfer"]').trigger('click')
    await wrapper.get('[data-testid="product-group-form-method-gateway-a"]').trigger('click')
    await wrapper.get('[data-testid="product-group-form-method-bank_transfer"]').trigger('click')
    await wrapper.get('[data-testid="product-group-form"]').trigger('submit')

    expect(wrapper.emitted('save')).toEqual([[{ name: 'Shared', description: null, allowedPaymentMethods: ['gateway-a'] }]])
  })

  it('opens a group with its methods ticked, a method no longer offered kept and marked', async () => {
    const { wrapper } = await mountScreen(ProductGroupFormModal, { group: stored, methods, saving: false })

    expect(wrapper.get('h2').text()).toBe('Edit product group')
    expect(wrapper.get('[data-testid="product-group-form-method-gateway-a"]').attributes('aria-checked')).toBe('true')
    expect(wrapper.get('[data-testid="product-group-form-method-bank_transfer"]').attributes('aria-checked')).toBe('false')
    expect(wrapper.get('[data-testid="product-group-form-method-retired-gateway"]').attributes('aria-checked')).toBe('true')
    expect(wrapper.get('[data-testid="product-group-form-methods"]').text()).toContain('retired-gateway (not on offer now)')

    await wrapper.get('[data-testid="product-group-form"]').trigger('submit')
    expect(wrapper.emitted('save')).toEqual([[{ name: 'Shared', description: 'Hosting', allowedPaymentMethods: ['gateway-a', 'retired-gateway'] }]])
  })


  it('emits close on cancel', async () => {
    const { wrapper } = await mountScreen(ProductGroupFormModal, { group: null, methods, saving: false })

    await wrapper.get('[data-testid="product-group-form-cancel"]').trigger('click')

    expect(wrapper.emitted('close')).toHaveLength(1)
  })
})
