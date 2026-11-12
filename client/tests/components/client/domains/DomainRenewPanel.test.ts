/**
 * Unit tests for DomainRenewPanel: the standing badge, the order summary, the payment method
 * choice, the order and the success state. The store, the API composables and the transport run for
 * real against a stub BFF.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mountClient } from '~/tests/helpers/mountClient'
import DomainRenewPanel from '~/components/client/domains/DomainRenewPanel.vue'
import type { DomainDetail } from '~/types/domaindetail'
import { DomainStatus } from '~/types/domainstatus'
import { HttpMethod } from '~/types/httpmethod'
import { DOMAIN_ID, DomainAction, domainPath } from '~/tests/helpers/domainPanelPaths'
import { AccountEndpoint } from '~/tests/helpers/accountPaths'
import { bodyOf } from '~/tests/helpers/requestBody'
import { requestsTo, routeTable, refusal } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'

/** A gateway the panel offers. */
const card = { module: 'card_gateway', displayName: 'Card gateway' }

/** The stub playing the BFF. */
let stub: StubServer

/** The replies of the current test; a test changes them before it submits. */
let table: StubRouteTable

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

/**
 * Mounts the panel and waits for the payment methods to arrive.
 *
 * @param overrides - Domain fields to set.
 * @returns The wrapper.
 */
const mountPanel = async (overrides: Partial<DomainDetail> = {}) => {
  const wrapper = mountClient(DomainRenewPanel, { props: { domain: domain(overrides), domainId: DOMAIN_ID, fullName: 'example.com' } })
  await vi.waitFor(() => expect(wrapper.find('[data-testid="domain-renew-payment"]').exists()).toBe(true))
  return wrapper
}

beforeEach(async () => {
  table = { [`GET ${AccountEndpoint.PaymentMethods}`]: { body: [card] } }
  stub = await startTransport(request => routeTable(table)(request))
})

afterEach(async () => {
  await stub.close()
})

describe('DomainRenewPanel', () => {
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
    table[`POST ${domainPath(DomainAction.RenewOrder)}`] = { body: { orderId: 3, invoiceId: 90 } }
    const wrapper = await mountPanel()

    await wrapper.find('form').trigger('submit')

    await vi.waitFor(() => expect(wrapper.find('[data-testid="domain-renew-pay"]').exists()).toBe(true))
    const orders = requestsTo(stub.requests, domainPath(DomainAction.RenewOrder))
    expect(orders).toHaveLength(1)
    expect(orders[0]?.method).toBe(HttpMethod.Post)
    expect(bodyOf(orders[0])).toEqual({ years: 1, paymentmethod: 'card_gateway' })
    expect(wrapper.find('[data-testid="domain-renew-pay"]').attributes('href')).toBe('/client/invoices/90')
  })

  it('keeps the form and shows the refusal when the order fails', async () => {
    table[`POST ${domainPath(DomainAction.RenewOrder)}`] = refusal('Cannot renew yet.')
    const wrapper = await mountPanel()

    await wrapper.find('form').trigger('submit')

    await vi.waitFor(() => expect(wrapper.find('[data-testid="ui-form-error"]').text()).toContain('Cannot renew yet.'))
    expect(wrapper.find('[data-testid="domain-renew-pay"]').exists()).toBe(false)
  })
})
