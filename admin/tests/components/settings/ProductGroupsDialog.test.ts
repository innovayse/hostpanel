/**
 * Tests for ProductGroupsDialog: each group is listed with the payment methods it is restricted to ("All methods"
 * when it is not), and the add and edit buttons report the choice to the parent.
 */
import { describe, expect, it } from 'vitest'
import ProductGroupsDialog from '@/components/settings/ProductGroupsDialog.vue'
import type { PaymentMethodOption, ProductGroup } from '@/types/product'
import { mountScreen } from '../../helpers/mount'

/** The methods the checkout offers. */
const methods: PaymentMethodOption[] = [{ module: 'gateway-a', displayName: 'Bank Card (Gateway A)' }]

/** An unrestricted group and one restricted to an offered and to a retired method. */
const groups: ProductGroup[] = [
  { id: 5, name: 'Shared', description: null, isActive: true, productCount: 2, allowedPaymentMethods: [] },
  { id: 6, name: 'Cloud', description: null, isActive: true, productCount: 0, allowedPaymentMethods: ['gateway-a', 'retired-gateway'] },
]

describe('ProductGroupsDialog', () => {
  it('says "All methods" for a group that restricts nothing and names the methods of one that does', async () => {
    const { wrapper } = await mountScreen(ProductGroupsDialog, { groups, methods })

    expect(wrapper.get('[data-testid="product-group-5"]').text()).toContain('All methods')
    expect(wrapper.get('[data-testid="product-group-6"]').text()).toContain('Bank Card (Gateway A), retired-gateway')
  })

  it('says so when there is no group yet', async () => {
    const { wrapper } = await mountScreen(ProductGroupsDialog, { groups: [], methods })

    expect(wrapper.find('[data-testid="product-groups-empty"]').exists()).toBe(true)
  })

  it('reports the group to edit and the request to add one', async () => {
    const { wrapper } = await mountScreen(ProductGroupsDialog, { groups, methods })

    await wrapper.get('[data-testid="product-group-edit-6"]').trigger('click')
    await wrapper.get('[data-testid="product-groups-add"]').trigger('click')

    expect(wrapper.emitted('edit')).toEqual([[groups[1]]])
    expect(wrapper.emitted('add')).toHaveLength(1)
  })
})
