/**
 * Unit tests for DomainRenewPanel: the standing badge, the order summary, the payment method
 * choice, the order and the success state.
 */
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { mountClient } from '~/tests/helpers/mountClient'
import DomainRenewPanel from '~/components/client/domains/DomainRenewPanel.vue'
import type { DomainDetail } from '~/types/domaindetail'
import { DomainStatus } from '~/types/domainstatus'

/** The mocked endpoints the store reaches. */
const domainsApi = vi.hoisted(() => ({ createRenewOrder: vi.fn() }))

/** The mocked billing endpoints the store reaches. */
const billingApi = vi.hoisted(() => ({ fetchGatewayMethods: vi.fn() }))

vi.mock('~/composables/apis/useDomainsApi', () => ({ useDomainsApi: () => domainsApi }))
vi.mock('~/composables/apis/useBillingApi', () => ({ useBillingApi: () => billingApi }))
vi.mock('~/composables/apis/useClientApi', () => ({ useClientApi: () => ({}) }))
vi.mock('~/composables/apis/useCatalogApi', () => ({ useCatalogApi: () => ({}) }))
vi.mock('~/composables/apis/useCurrencyApi', () => ({ useCurrencyApi: () => ({}) }))

/**
 * Builds a domain with the fields the panel reads.
 *
 * @param overrides - Fields to set.
 * @returns A domain.
 */
const domain = (overrides: Partial<DomainDetail> = {}): DomainDetail => ({
  id: 7, name: 'example.com', status: DomainStatus.Active, expiresAt: '2099-01-01T00:00:00Z', recurringAmount: 12, priceCurrency: 'USD',
  ...overrides
} as DomainDetail)

/** A gateway the panel offers. */
const card = { module: 'card_gateway', displayName: 'Card gateway' }

/**
 * Mounts the panel and waits for the payment methods to arrive.
 *
 * @param overrides - Domain fields to set.
 * @returns The wrapper.
 */
const mountPanel = async (overrides: Partial<DomainDetail> = {}) => {
  const wrapper = mountClient(DomainRenewPanel, { props: { domain: domain(overrides), domainId: '7', fullName: 'example.com' } })
  await vi.waitFor(() => expect(wrapper.find('[data-testid="domain-renew-payment"]').exists()).toBe(true))
  return wrapper
}

describe('DomainRenewPanel', () => {
  beforeEach(() => {
    domainsApi.createRenewOrder.mockReset()
    billingApi.fetchGatewayMethods.mockReset()
    billingApi.fetchGatewayMethods.mockResolvedValue([card])
  })

  it.each([
    [{}, 'client.domains.renewActive'],
    [{ status: DomainStatus.Expired }, 'client.domains.renewPastPeriod'],
    [{ expiresAt: new Date(Date.now() + 5 * 86_400_000).toISOString() }, 'client.domains.renewExpiringSoon'],
  ])('reads the standing of %j as %s', async (overrides, label) => {
    const wrapper = await mountPanel(overrides)

    expect(wrapper.text()).toContain(label)
  })

  it('totals the order by the years chosen, in the domain currency', async () => {
    const wrapper = await mountPanel()

    expect(wrapper.find('[data-testid="domain-renew-summary"]').text()).toContain('$12.00')
  })

  it('preselects the first saved payment method', async () => {
    const wrapper = await mountPanel()

    expect(wrapper.find('[data-testid="domain-renew-submit"]').attributes('disabled')).toBeUndefined()
  })

  it('places the order for this domain and shows the invoice link', async () => {
    domainsApi.createRenewOrder.mockResolvedValue({ orderId: 3, invoiceId: 90 })
    const wrapper = await mountPanel()

    await wrapper.find('form').trigger('submit')

    await vi.waitFor(() => expect(wrapper.find('[data-testid="domain-renew-pay"]').exists()).toBe(true))
    expect(domainsApi.createRenewOrder).toHaveBeenCalledWith('7', 1, 'card_gateway')
    expect(wrapper.find('[data-testid="domain-renew-pay"]').attributes('href')).toBe('/client/invoices/90')
  })

  it('keeps the form and shows the refusal when the order fails', async () => {
    domainsApi.createRenewOrder.mockRejectedValue({ data: { detail: 'Cannot renew yet.' } })
    const wrapper = await mountPanel()

    await wrapper.find('form').trigger('submit')

    await vi.waitFor(() => expect(wrapper.find('[data-testid="ui-form-error"]').text()).toContain('Cannot renew yet.'))
    expect(wrapper.find('[data-testid="domain-renew-pay"]').exists()).toBe(false)
  })
})
