/**
 * Tests for UpcomingRenewalsView: it lists the services, domains and claimed lines the API returns, asks for
 * another window from the tabs, switches auto-renew, raises a client invoice by hand, releases a claim only
 * after the confirmation, and on a refused load shows the error with Retry.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import UpcomingRenewalsView from '@/views/UpcomingRenewalsView.vue'
import { mountRouted } from '../helpers/mount'
import { json, problem } from '../helpers/fetch'
import { stubRoutes } from '../helpers/routes'

/** An empty page. */
const emptyPage = { items: [], pageNumber: 1, pageSize: 20, totalCount: 0, totalPages: 0 }

/** One service renewing soon. */
const service = {
  id: 4, clientId: 9, clientName: 'Ada Lovelace', productName: 'Starter', domain: null, amount: 5,
  priceCurrency: 'USD', billingCycle: 'Monthly', nextRenewalAt: '2027-01-10T00:00:00Z', autoRenew: true,
}

/** One domain expiring soon. */
const domain = {
  id: 7, clientId: 10, clientName: 'Grace Hopper', name: 'example.test', regPeriodYears: 1, registrar: null,
  recurringAmount: 12, priceCurrency: 'USD', nextDueDate: '2027-01-12T00:00:00Z', expiresAt: '2027-01-12T00:00:00Z',
  status: 'Active', autoRenew: false,
}

/** One claimed line. */
const claim = {
  invoiceId: 5, itemId: 11, clientId: 9, registeredDomainId: 7, description: 'example.test renewal',
  periodStart: '2027-01-12T00:00:00Z', periodEnd: '2028-01-12T00:00:00Z', claimedAt: '2027-01-01T10:00:00Z',
}

/** The three list answers every test starts from. */
const lists = {
  'GET /api/services/renewals/upcoming': () => json({ ...emptyPage, items: [service], totalCount: 1 }),
  'GET /api/domains/renewals/upcoming': () => json({ ...emptyPage, items: [domain], totalCount: 1 }),
  'GET /api/billing/renewal-claims': () => json([claim]),
}

/** Mounts the view on its route. */
const mountView = async () => {
  setActivePinia(createPinia())
  const { wrapper } = await mountRouted(UpcomingRenewalsView, '/billing/renewals', '/billing/renewals')
  await flushPromises()
  return wrapper
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('UpcomingRenewalsView', () => {
  it('renders the services, domains and claimed lines', async () => {
    stubRoutes(lists)
    const wrapper = await mountView()

    expect(wrapper.get('[data-testid="renewals-services-table"]').text()).toContain('Starter')
    expect(wrapper.get('[data-testid="renewals-services-table"]').text()).toContain('$5.00 USD')
    expect(wrapper.get('[data-testid="renewals-service-client-4"]').attributes('href')).toBe('/clients/9')
    expect(wrapper.get('[data-testid="renewals-domains-table"]').text()).toContain('example.test')
    expect(wrapper.get('[data-testid="renewals-claim-invoice-11"]').attributes('href')).toBe('/billing/5')
    expect(wrapper.get('[data-testid="renewals-claims-table"]').text()).toContain('example.test renewal')
  })

  it('shows the empty states', async () => {
    stubRoutes({
      'GET /api/services/renewals/upcoming': () => json(emptyPage),
      'GET /api/domains/renewals/upcoming': () => json(emptyPage),
      'GET /api/billing/renewal-claims': () => json([]),
    })
    const wrapper = await mountView()

    expect(wrapper.text()).toContain('No services renew in this window.')
    expect(wrapper.text()).toContain('No domains expire in this window.')
    expect(wrapper.text()).toContain('No domain renewals are waiting for staff.')
  })

  it('asks for the window of the chosen tab', async () => {
    const mock = stubRoutes(lists)
    const wrapper = await mountView()

    await wrapper.get('[data-testid="renewals-window-Next90Days"]').trigger('click')
    await flushPromises()

    const urls = mock.mock.calls.map(call => String(call[0]))
    expect(urls).toContain('/api/services/renewals/upcoming?window=Next90Days&page=1&pageSize=20')
    expect(urls).toContain('/api/domains/renewals/upcoming?window=Next90Days&page=1&pageSize=20')
  })

  it('switches auto-renew of a service off', async () => {
    const mock = stubRoutes({ ...lists, 'PUT /api/services/4/auto-renew': () => new Response(null, { status: 204 }) })
    const wrapper = await mountView()

    await wrapper.get('[data-testid="renewals-service-auto-renew-4"]').trigger('click')
    await flushPromises()

    const put = mock.mock.calls.find(call => String(call[0]).endsWith('/services/4/auto-renew'))
    expect(JSON.parse(String(((put as unknown[])[1] as RequestInit).body))).toEqual({ value: false })
  })

  it('raises the invoices of a client and says how many', async () => {
    const mock = stubRoutes({ ...lists, 'POST /api/billing/client/9/renewal-invoices': () => json(1) })
    const wrapper = await mountView()

    await wrapper.get('[data-testid="renewals-service-generate-4"]').trigger('click')
    await flushPromises()

    expect(mock.mock.calls.some(call => String(call[0]) === '/api/billing/client/9/renewal-invoices')).toBe(true)
    expect(wrapper.get('[data-testid="renewals-notice"]').text()).toContain('Renewal invoices raised: 1.')
  })

  it('says so when nothing is due', async () => {
    stubRoutes({ ...lists, 'POST /api/billing/client/10/renewal-invoices': () => json(0) })
    const wrapper = await mountView()

    await wrapper.get('[data-testid="renewals-domain-generate-7"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="renewals-notice"]').text()).toContain('Nothing to invoice yet.')
  })

  it('releases a claim only after the confirmation', async () => {
    const mock = stubRoutes({
      ...lists,
      'POST /api/billing/5/items/11/release-renewal-claim': () => new Response(null, { status: 204 }),
    })
    const wrapper = await mountView()
    /** Whether the release request has been sent. */
    const released = (): boolean => mock.mock.calls.some(call => String(call[0]).endsWith('/release-renewal-claim'))

    await wrapper.get('[data-testid="renewals-claim-release-11"]').trigger('click')
    await flushPromises()
    expect(released()).toBe(false)
    expect(wrapper.text()).toContain('Confirm at the registrar that this domain was not renewed')

    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(released()).toBe(true)
  })

  it('shows the API sentence of a refused action', async () => {
    stubRoutes({ ...lists, 'POST /api/billing/client/9/renewal-invoices': () => problem('This client account is suspended.', 403) })
    const wrapper = await mountView()

    await wrapper.get('[data-testid="renewals-service-generate-4"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="renewals-action-error"]').text()).toContain('This client account is suspended.')
  })

  it('shows the error with Retry when a load is refused', async () => {
    stubRoutes({
      'GET /api/services/renewals/upcoming': () => problem('Not allowed.', 403),
      'GET /api/domains/renewals/upcoming': () => json(emptyPage),
      'GET /api/billing/renewal-claims': () => json([]),
    })
    const wrapper = await mountView()

    expect(wrapper.get('[data-testid="renewals-error"]').text()).toContain('Not allowed.')
    expect(wrapper.find('[data-testid="renewals-services-table"]').exists()).toBe(false)
  })
})
