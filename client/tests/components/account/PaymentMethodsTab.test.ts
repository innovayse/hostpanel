/**
 * Unit tests for PaymentMethodsTab: the stored-methods card for a person who may pay invoices and
 * the reason instead of it for a person who may not.
 */
import { describe, expect, it, vi } from 'vitest'
import { mountAccount } from '~/tests/helpers/mountAccount'
import PaymentMethodsTab from '~/components/account/PaymentMethodsTab.vue'
import { Permission } from '~/composables/usePermissions'
import { grantPermissions } from '~/tests/helpers/grantPermissions'

vi.mock('~/composables/apis/useClientApi', () => ({ useClientApi: () => ({}) }))
vi.mock('~/composables/apis/useAuthApi', () => ({ useAuthApi: () => ({}) }))
vi.mock('qrcode', () => ({ default: { toDataURL: async (): Promise<string> => '' } }))

/** The stored-methods card is its own unit; here it is only a marker. */
const stubs = { AccountStoredMethodsCard: { template: '<div data-testid="stored-methods-stub" />' } }

describe('PaymentMethodsTab', () => {
  it('shows the stored-methods card when the person holds the permission', async () => {
    const wrapper = mountAccount(PaymentMethodsTab, { components: stubs })
    await grantPermissions(Permission.ViewPayInvoices)

    expect(wrapper.find('[data-testid="stored-methods-stub"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="account-payment-denied"]').exists()).toBe(false)
  })

  it('shows only the reason, no card, without the permission to view and pay invoices', async () => {
    const wrapper = mountAccount(PaymentMethodsTab, { components: stubs })
    await grantPermissions(Permission.ViewEmails)

    expect(wrapper.find('[data-testid="account-payment-denied"]').text()).toContain('client.payment.permissionDenied')
    expect(wrapper.find('[data-testid="stored-methods-stub"]').exists()).toBe(false)
  })
})
