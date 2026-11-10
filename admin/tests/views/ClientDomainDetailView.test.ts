/**
 * Mount tests for ClientDomainDetailView: it renders against the real kit and settles on an API
 * refusal with a Retry, and a registrar command reports its outcome next to the Save button.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import ClientDomainDetailView from '@/views/ClientDomainDetailView.vue'
import { mountClientView } from '../helpers/clientViews'
import { json, problem } from '../helpers/fetch'
import { mountRouted } from '../helpers/mount'
import { stubRoutes } from '../helpers/routes'

/**
 * Domain 3 of client 7.
 *
 * @param status - The domain status; pending registration by default.
 * @returns The detail answer.
 */
const domain = (status = 'PendingRegistration') =>
  json({
    id: 3,
    clientId: 7,
    name: 'example.com',
    tld: 'com',
    status,
    adminNotes: null,
    term: {
      registeredAt: '2026-01-01T00:00:00Z',
      expiresAt: '2027-01-01T00:00:00Z',
      nextDueDate: '2027-01-01T00:00:00Z',
      registrationPeriod: 1,
    },
    registrar: { module: null, reference: null, eppCode: null },
    features: { autoRenew: false, whoisPrivacy: false, isLocked: false, dnsManagement: false, emailForwarding: false },
    billing: {
      firstPaymentAmount: 10,
      recurringAmount: 10,
      priceCurrency: 'USD',
      paymentMethod: null,
      promotionCode: null,
      subscriptionId: null,
    },
    origin: { orderId: null, orderType: 'Register', linkedServiceId: null },
    nameservers: [],
    dnsRecords: [],
    emailForwardingRules: [],
    reminders: [],
  })

/**
 * Mounts the screen for domain 3 of client 7 and waits for its first load.
 *
 * @returns The wrapper.
 */
const mountDomain = async () => {
  const { wrapper } = await mountRouted(ClientDomainDetailView, '/clients/:id/domains/:domainId', '/clients/7/domains/3')
  await flushPromises()
  return wrapper
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('ClientDomainDetailView', () => {
  it('mounts without throwing', async () => {
    const wrapper = await mountClientView(ClientDomainDetailView)

    expect(wrapper.exists()).toBe(true)
  })

  it('shows the refusal sentence from the API', async () => {
    const wrapper = await mountClientView(ClientDomainDetailView, 'Server said no')

    expect(wrapper.text()).toContain('Server said no')
  })

  it('reports an accepted registrar command next to the Save button', async () => {
    stubRoutes({ 'POST /api/domains/3/register': () => new Response(null, { status: 204 }), 'GET /api/domains/3': () => domain() })
    const wrapper = await mountDomain()

    await wrapper.get('[data-testid="client-domain-register"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="client-domain-saved"]').text()).toBe('Done. The registrar accepted the change.')
  })

  it('shows the API sentence when a registrar command is refused', async () => {
    stubRoutes({ 'POST /api/domains/3/register': () => problem('The registrar is offline.', 502), 'GET /api/domains/3': () => domain() })
    const wrapper = await mountDomain()

    await wrapper.get('[data-testid="client-domain-register"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="client-domain-action-error"]').text()).toBe('The registrar is offline.')
    expect(wrapper.find('[data-testid="client-domain-saved"]').exists()).toBe(false)
  })

  it('offers Cancel Transfer only while a transfer is pending', async () => {
    stubRoutes({ 'GET /api/domains/3': () => domain() })
    const wrapper = await mountDomain()

    expect(wrapper.find('[data-testid="client-domain-cancel-transfer"]').exists()).toBe(false)
  })

  it('shows the readable sentence when the registrar cannot cancel a transfer', async () => {
    stubRoutes({
      'POST /api/domains/3/cancel-transfer': () => problem("The registrar does not offer this action. Please contact the registrar's support.", 409),
      'GET /api/domains/3': () => domain('PendingTransfer'),
    })
    const wrapper = await mountDomain()

    await wrapper.get('[data-testid="client-domain-cancel-transfer"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="client-domain-action-error"]').text()).toBe("The registrar does not offer this action. Please contact the registrar's support.")
    expect(wrapper.find('[data-testid="client-domain-saved"]').exists()).toBe(false)
  })

  it('reloads the domain from the Retry of a refused load', async () => {
    let refused = true
    stubRoutes({ 'GET /api/domains/3': () => (refused ? problem('Domain is unavailable.', 503) : domain()) })
    const wrapper = await mountDomain()

    refused = false
    await wrapper.get('[data-testid="client-domain-error-retry"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="client-domain-error"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="client-domain-register"]').exists()).toBe(true)
  })
})
