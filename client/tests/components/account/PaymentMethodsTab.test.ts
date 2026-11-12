/**
 * Unit tests for PaymentMethodsTab: the stored-methods card for a person who may pay invoices and
 * the reason instead of it for a person who may not. The card, the stored-methods store, its API
 * composable and the transport run for real against a stub BFF.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { mountAccount } from '~/tests/helpers/mountAccount'
import PaymentMethodsTab from '~/components/account/PaymentMethodsTab.vue'
import { AccountEndpoint } from '~/tests/helpers/accountPaths'
import { grantPermissions } from '~/tests/helpers/grantPermissions'
import { stubLocalePath } from '~/tests/helpers/runtimeStubs'
import { requestsTo, routeTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import { HttpMethod } from '~/types/httpmethod'
import { Permission } from '~/types/permission'

/** The stub playing the BFF: the client holds no stored method. */
let stub: StubServer

beforeEach(async () => {
  stubLocalePath('/en')
  stub = await startTransport(routeTable({ [`${HttpMethod.Get} ${AccountEndpoint.StoredMethods}`]: { body: [] } }))
})

afterEach(async () => {
  vi.unstubAllGlobals()
  await stub.close()
})

describe('PaymentMethodsTab', () => {
  it('shows the stored-methods card when the person holds the permission', async () => {
    const wrapper = mountAccount(PaymentMethodsTab)
    await grantPermissions(Permission.ViewPayInvoices)

    await vi.waitFor(() => expect(requestsTo(stub.requests, AccountEndpoint.StoredMethods)).toHaveLength(1))
    await flushPromises()
    expect(wrapper.find('[data-testid="stored-methods-card"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="account-payment-denied"]').exists()).toBe(false)
  })

  it('shows only the reason, no card, without the permission to view and pay invoices', async () => {
    const wrapper = mountAccount(PaymentMethodsTab)
    await grantPermissions(Permission.ViewEmails)

    expect(wrapper.find('[data-testid="account-payment-denied"]').text()).toContain('client.payment.permissionDenied')
    expect(wrapper.find('[data-testid="stored-methods-card"]').exists()).toBe(false)
  })
})
