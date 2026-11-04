/**
 * Tests for PaymentClaimsView: it asks the API for held claims, shows the empty state when there
 * are none, renders the invoice link, client, amount, gateway module, date and status of each claim,
 * and on a refused load shows the error without the empty state or the count and loads again from Retry.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import PaymentClaimsView from '@/views/PaymentClaimsView.vue'
import { mountRouted } from '../helpers/mount'
import { callOf, json, stubFetch, problem } from '../helpers/fetch'

/** A page with one held claim. */
const page = {
  items: [
    {
      id: 3,
      invoiceId: 5,
      clientId: 9,
      clientName: 'Ada Lovelace',
      gatewayModuleId: 'bank-gateway',
      amount: 12.5,
      currency: 'USD',
      status: 'Held',
      transactionId: 'tx-1',
      createdAt: '2026-10-14T09:00:00Z',
      resolvedAt: '2026-10-14T09:01:00Z',
    },
  ],
  pageNumber: 1,
  pageSize: 20,
  totalCount: 1,
  totalPages: 1,
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('PaymentClaimsView', () => {
  it('shows the empty state when there are no held claims', async () => {
    setActivePinia(createPinia())
    stubFetch(json({ items: [], pageNumber: 1, pageSize: 20, totalCount: 0, totalPages: 0 }))
    const { wrapper } = await mountRouted(PaymentClaimsView, '/billing/payment-claims', '/billing/payment-claims')
    await flushPromises()

    expect(wrapper.text()).toContain('Held Payment Claims')
    expect(wrapper.get('[data-testid="billing-payment-claims-table"]').text()).toContain('No held payment claims.')
    expect(wrapper.get('[data-testid="billing-payment-claims-count"]').text()).toContain('0 Records Found')
  })

  it('asks for held claims and renders each row', async () => {
    setActivePinia(createPinia())
    const mock = stubFetch(json(page))
    const { wrapper } = await mountRouted(PaymentClaimsView, '/billing/payment-claims', '/billing/payment-claims')
    await flushPromises()

    expect(callOf(mock).url).toBe('/api/billing/payment-claims?page=1&pageSize=20&status=Held')
    const table = wrapper.get('[data-testid="billing-payment-claims-table"]').text()
    expect(table).toContain('Ada Lovelace')
    expect(table).toContain('bank-gateway')
    expect(table).toContain('$12.50')
    expect(table).toContain('Held')
    expect(table).toContain('14/10/2026')
    expect(table).not.toContain('Currency')
    expect(wrapper.get('[data-testid="billing-payment-claim-invoice-3"]').attributes('href')).toBe('/billing/5')
    expect(wrapper.get('[data-testid="billing-payment-claim-client-3"]').attributes('href')).toBe('/clients/9')
  })

  it('loads again from the Retry of a refused load', async () => {
    stubFetch(problem('Not allowed.', 403))
    const wrapper = (await mountRouted(PaymentClaimsView, '/billing/payment-claims', '/billing/payment-claims')).wrapper
    await flushPromises()
    expect(wrapper.get('[data-testid="billing-payment-claims-error"]').text()).toContain('Not allowed.')
    expect(wrapper.find('[data-testid="billing-payment-claims-count"]').exists()).toBe(false)
    expect(wrapper.text()).not.toContain('No held payment claims.')
    const fetchMock = vi.mocked(fetch)
    const before = fetchMock.mock.calls.length

    await wrapper.get('[data-testid="billing-payment-claims-error-retry"]').trigger('click')
    await flushPromises()

    expect(fetchMock.mock.calls.length).toBeGreaterThan(before)
  })
})
