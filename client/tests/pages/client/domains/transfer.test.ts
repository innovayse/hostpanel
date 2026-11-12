/**
 * Tests for `pages/client/domains/transfer.vue`: the prefilled form, the order and the outcomes.
 */
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { mountPage, stubClientRuntime } from '~/tests/helpers/mountClient'
import TransferPage from '~/pages/client/domains/transfer.vue'

/** The mocked endpoints the store reaches. */
const domainsApi = vi.hoisted(() => ({ createTransferOrder: vi.fn() }))

/** The mocked billing endpoints the store reaches. */
const billingApi = vi.hoisted(() => ({ fetchGatewayMethods: vi.fn() }))

vi.mock('~/composables/apis/useDomainsApi', () => ({ useDomainsApi: () => domainsApi }))
vi.mock('~/composables/apis/useBillingApi', () => ({ useBillingApi: () => billingApi }))
vi.mock('~/composables/apis/useClientApi', () => ({ useClientApi: () => ({}) }))
vi.mock('~/composables/apis/useCatalogApi', () => ({ useCatalogApi: () => ({}) }))
vi.mock('~/composables/apis/useCurrencyApi', () => ({ useCurrencyApi: () => ({}) }))

/** A gateway. */
const card = { module: 'card_gateway', displayName: 'Card gateway', supportsStoredCredentials: false }

describe('domain transfer page', () => {
  beforeEach(() => {
    domainsApi.createTransferOrder.mockReset()
    billingApi.fetchGatewayMethods.mockReset()
    billingApi.fetchGatewayMethods.mockResolvedValue([card])
    stubClientRuntime({ query: { domain: 'moving.org', eppcode: 'AUTH-1' } })
  })

  it('prefills the domain and the code from the query', async () => {
    const wrapper = await mountPage(TransferPage)

    expect((wrapper.find('[data-testid="transfer-domain"]').element as HTMLInputElement).value).toBe('moving.org')
    expect((wrapper.find('[data-testid="transfer-epp"]').element as HTMLInputElement).value).toBe('AUTH-1')
  })

  it('has a placeholder on every field', async () => {
    const wrapper = await mountPage(TransferPage)

    expect(wrapper.find('[data-testid="transfer-domain"]').attributes('placeholder')).toBe('client.domainTransfer.domainPlaceholder')
    expect(wrapper.find('[data-testid="transfer-epp"]').attributes('placeholder')).toBe('client.domainTransfer.eppPlaceholder')
  })

  it('preselects the first saved payment method and enables the order', async () => {
    const wrapper = await mountPage(TransferPage)

    expect(wrapper.find('[data-testid="transfer-submit"]').attributes('disabled')).toBeUndefined()
  })

  it('says so, and cannot order, when there is no payment method', async () => {
    billingApi.fetchGatewayMethods.mockResolvedValue([])

    const wrapper = await mountPage(TransferPage)

    expect(wrapper.find('[data-testid="transfer-no-payment"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="transfer-submit"]').attributes('disabled')).toBeDefined()
  })

  it('places the order and links to the invoice', async () => {
    domainsApi.createTransferOrder.mockResolvedValue({ orderId: 3, invoiceId: 90 })
    const wrapper = await mountPage(TransferPage)

    await wrapper.find('form').trigger('submit')

    await vi.waitFor(() => expect(wrapper.find('[data-testid="transfer-success"]').exists()).toBe(true))
    expect(domainsApi.createTransferOrder).toHaveBeenCalledWith(expect.objectContaining({ domain: 'moving.org', eppcode: 'AUTH-1', years: 1, paymentmethod: 'card_gateway' }))
    expect(wrapper.find('[data-testid="transfer-pay-invoice"]').attributes('href')).toBe('/client/invoices/90')
  })

  it('shows the refusal and keeps the form when the order fails', async () => {
    domainsApi.createTransferOrder.mockRejectedValue({ data: { detail: 'Domain is locked.' } })
    const wrapper = await mountPage(TransferPage)

    await wrapper.find('form').trigger('submit')

    await vi.waitFor(() => expect(wrapper.find('[data-testid="ui-form-error"]').text()).toContain('Domain is locked.'))
    expect(wrapper.find('[data-testid="transfer-success"]').exists()).toBe(false)
  })
})
