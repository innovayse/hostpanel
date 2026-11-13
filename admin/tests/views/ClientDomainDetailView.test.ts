/**
 * Mount tests for ClientDomainDetailView: it renders against the real kit and settles on an API
 * refusal with a Retry, a registrar command reports its outcome next to the Save button, and the page is a header
 * card with the domain status over details, registrar and management tabs of one form.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import type { VueWrapper } from '@vue/test-utils'
import ClientDomainDetailView from '@/views/ClientDomainDetailView.vue'
import { mountClientView } from '../helpers/clientViews'
import { callOf, json, problem } from '../helpers/fetch'
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

/**
 * Opens a tab of the page.
 *
 * @param wrapper - The mounted page.
 * @param tab - The tab to open.
 */
const openTab = async (wrapper: VueWrapper, tab: 'details' | 'registrar' | 'management') => {
  await wrapper.get(`[data-testid="client-domain-tab-${tab}"]`).trigger('click')
  await flushPromises()
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('ClientDomainDetailView', () => {
  it('keeps the real header, tabs and sections as placeholders while the domain loads', async () => {
    vi.stubGlobal('fetch', vi.fn(() => new Promise<Response>(() => undefined)))
    const wrapper = await mountDomain()

    expect(wrapper.find('[data-testid="client-domain-loading"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="client-domain-tab-registrar"]').exists()).toBe(true)
    expect(wrapper.text()).toContain('Domain Info')
    expect(wrapper.findAll('[data-testid="ui-field-skeleton"]').length).toBeGreaterThan(0)
    expect(wrapper.get('[data-testid="client-domain-save"]').attributes('disabled')).toBeDefined()
  })

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
    await openTab(wrapper, 'registrar')

    await wrapper.get('[data-testid="client-domain-register"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="client-domain-saved"]').text()).toBe('Done. The registrar accepted the change.')
  })

  it('shows the API sentence when a registrar command is refused', async () => {
    stubRoutes({ 'POST /api/domains/3/register': () => problem('The registrar is offline.', 502), 'GET /api/domains/3': () => domain() })
    const wrapper = await mountDomain()
    await openTab(wrapper, 'registrar')

    await wrapper.get('[data-testid="client-domain-register"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="client-domain-action-error"]').text()).toBe('The registrar is offline.')
    expect(wrapper.find('[data-testid="client-domain-saved"]').exists()).toBe(false)
  })

  it('offers Cancel Transfer only while a transfer is pending', async () => {
    stubRoutes({ 'GET /api/domains/3': () => domain() })
    const wrapper = await mountDomain()
    await openTab(wrapper, 'registrar')

    expect(wrapper.find('[data-testid="client-domain-cancel-transfer"]').exists()).toBe(false)
  })

  it('shows the readable sentence when the registrar cannot cancel a transfer', async () => {
    stubRoutes({
      'POST /api/domains/3/cancel-transfer': () => problem("The registrar does not offer this action. Please contact the registrar's support.", 409),
      'GET /api/domains/3': () => domain('PendingTransfer'),
    })
    const wrapper = await mountDomain()
    await openTab(wrapper, 'registrar')

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
    expect(wrapper.find('[data-testid="client-domain-header"]').exists()).toBe(true)
  })

  it('names the domain and its status in the header', async () => {
    stubRoutes({ 'GET /api/domains/3': () => domain('Active') })
    const wrapper = await mountDomain()

    expect(wrapper.get('[data-testid="client-domain-header"]').text()).toContain('example.com')
    expect(wrapper.get('[data-testid="client-domain-status-badge"]').text()).toBe('Active')
  })

  it('shows one tab at a time', async () => {
    stubRoutes({ 'GET /api/domains/3': () => domain() })
    const wrapper = await mountDomain()

    expect(wrapper.find('[data-testid="client-domain-first-payment"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="client-domain-register"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="client-domain-toggle-dns"]').exists()).toBe(false)

    await openTab(wrapper, 'registrar')
    expect(wrapper.find('[data-testid="client-domain-first-payment"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="client-domain-register"]').exists()).toBe(true)

    await openTab(wrapper, 'management')
    expect(wrapper.find('[data-testid="client-domain-toggle-dns"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="client-domain-admin-notes"]').exists()).toBe(true)
  })

  it('saves a field of a tab that is not on show with the rest of the form', async () => {
    const mock = stubRoutes({
      'PUT /api/domains/3': () => new Response(null, { status: 204 }),
      'GET /api/domains/3': () => domain(),
    })
    const wrapper = await mountDomain()

    await wrapper.get('[data-testid="client-domain-promotion"]').setValue('SAVE10')
    await openTab(wrapper, 'registrar')
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    const put = mock.mock.calls
      .map((_, index) => callOf(mock, index))
      .find((call) => call.method === 'PUT')
    expect(put?.url).toBe('/api/domains/3')
    expect(put?.body).toEqual(expect.objectContaining({ promotionCode: 'SAVE10' }))
  })
})
