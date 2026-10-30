/**
 * Unit tests for PaymentMethodsTab: loading, failed read vs empty and the actions on a saved method.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mountAccount } from '~/tests/helpers/mountAccount'
import { stubLocalePath } from '~/tests/helpers/runtimeStubs'
import PaymentMethodsTab from '~/components/account/PaymentMethodsTab.vue'
import { Permission } from '~/composables/usePermissions'
import { grantPermissions } from '~/tests/helpers/grantPermissions'
import { useAccountStore } from '~/stores/account'
import { ActionOutcome } from '~/types/actionoutcome'
import { PaymentMethodType } from '~/types/payment'
import type { PaymentMethod } from '~/types/payment'

vi.mock('~/composables/apis/useClientApi', () => ({ useClientApi: () => ({}) }))
vi.mock('~/composables/apis/useBillingApi', () => ({ useBillingApi: () => ({}) }))
vi.mock('~/composables/apis/useAuthApi', () => ({ useAuthApi: () => ({}) }))
vi.mock('qrcode', () => ({ default: { toDataURL: async (): Promise<string> => '' } }))

/** One saved card. */
const method: PaymentMethod = { id: '9', type: PaymentMethodType.Card, description: 'My card', gatewayName: 'stripe', cardLastFour: '4242' }

describe('PaymentMethodsTab', () => {
  beforeEach(() => {
    stubLocalePath('/en')
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('shows a skeleton while loading', async () => {
    const wrapper = mountAccount(PaymentMethodsTab)
    useAccountStore().paymentLoading = true
    await wrapper.vm.$nextTick()

    expect(wrapper.find('[data-testid="account-payment-loading"]').exists()).toBe(true)
  })

  it('shows the empty state when the read worked and nothing is saved', () => {
    const wrapper = mountAccount(PaymentMethodsTab)

    expect(wrapper.find('[data-testid="ui-empty-state"]').text()).toContain('client.payment.empty')
    expect(wrapper.find('[data-testid="ui-empty-state"]').text()).toContain('client.payment.emptyDesc')
    expect(wrapper.find('[data-testid="account-payment-load-error"]').exists()).toBe(false)
  })

  it('links a customer without a saved card to the invoices, where the hosted gateway takes the payment', () => {
    const wrapper = mountAccount(PaymentMethodsTab)

    expect(wrapper.find('[data-testid="account-payment-invoices"]').attributes('href')).toBe('/en/client/invoices')
  })

  it('shows the failure, not the empty state, when the read failed', async () => {
    const wrapper = mountAccount(PaymentMethodsTab)
    useAccountStore().paymentLoadError = 'Billing is down'
    await wrapper.vm.$nextTick()

    expect(wrapper.find('[data-testid="account-payment-load-error"]').text()).toContain('Billing is down')
    expect(wrapper.find('[data-testid="ui-empty-state"]').exists()).toBe(false)
  })

  it('offers no add action, since the API has no way to add a card', () => {
    const wrapper = mountAccount(PaymentMethodsTab)

    expect(wrapper.find('[data-testid="account-payment-add"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="payment-methods-add"]').exists()).toBe(false)
  })

  it('makes a method the default and reports it', async () => {
    const wrapper = mountAccount(PaymentMethodsTab)
    const store = useAccountStore()
    store.paymentMethods = [method]
    vi.spyOn(store, 'setDefaultPaymentMethod').mockResolvedValue(ActionOutcome.Succeeded)
    const reload = vi.spyOn(store, 'loadPaymentMethods').mockResolvedValue()
    await wrapper.vm.$nextTick()

    await wrapper.find('[data-testid="account-payment-default-9"]').trigger('click')
    await Promise.resolve()
    await wrapper.vm.$nextTick()

    expect(reload).toHaveBeenCalled()
    expect(wrapper.find('[data-testid="account-payment-success"]').text()).toBe('client.payment.defaultSet')
  })

  it('removes a method only after confirmation', async () => {
    const wrapper = mountAccount(PaymentMethodsTab)
    const store = useAccountStore()
    store.paymentMethods = [method]
    const remove = vi.spyOn(store, 'removePaymentMethod').mockResolvedValue(ActionOutcome.Succeeded)
    vi.spyOn(store, 'loadPaymentMethods').mockResolvedValue()
    await wrapper.vm.$nextTick()

    await wrapper.find('[data-testid="account-payment-remove-9"]').trigger('click')
    expect(remove).not.toHaveBeenCalled()

    await wrapper.find('[data-testid="ui-confirm-confirm"]').trigger('click')
    await Promise.resolve()

    expect(remove).toHaveBeenCalledWith('9')
  })

  it('shows the API sentence of a refused action', async () => {
    const wrapper = mountAccount(PaymentMethodsTab)
    useAccountStore().paymentActionError = 'Cannot remove the last method'
    await wrapper.vm.$nextTick()

    expect(wrapper.find('[data-testid="account-payment-action-error"]').text()).toContain('Cannot remove the last method')
  })

  describe('without the permission to view and pay invoices', () => {
    it('shows only the reason, no list, no empty state and no skeleton', async () => {
      const wrapper = mountAccount(PaymentMethodsTab)
      useAccountStore().paymentMethods = [method]
      await grantPermissions(Permission.ViewEmails)

      expect(wrapper.find('[data-testid="account-payment-denied"]').text()).toContain('client.payment.permissionDenied')
      expect(wrapper.find('[data-testid="account-payment-default-9"]').exists()).toBe(false)
      expect(wrapper.find('[data-testid="account-payment-remove-9"]').exists()).toBe(false)
      expect(wrapper.find('[data-testid="ui-empty-state"]').exists()).toBe(false)
      expect(wrapper.find('[data-testid="account-payment-loading"]').exists()).toBe(false)
    })

    it('shows the saved methods when the person holds the permission', async () => {
      const wrapper = mountAccount(PaymentMethodsTab)
      useAccountStore().paymentMethods = [method]
      await grantPermissions(Permission.ViewPayInvoices)

      expect(wrapper.find('[data-testid="account-payment-denied"]').exists()).toBe(false)
      expect(wrapper.find('[data-testid="account-payment-default-9"]').exists()).toBe(true)
    })
  })
})
