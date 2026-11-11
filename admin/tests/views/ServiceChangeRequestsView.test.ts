/**
 * Mount tests of ServiceChangeRequestsView: it renders what the API sent, narrows to one service
 * from the route, confirms and sends a decision with its note, reports a refusal, and shows the
 * empty state and the load refusal.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import ServiceChangeRequestsView from '@/views/ServiceChangeRequestsView.vue'
import { mountRouted, mountRoutedSettled } from '../helpers/mount'
import { json, problem } from '../helpers/fetch'
import { stubRoutes } from '../helpers/routes'

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('ServiceChangeRequestsView', () => {
  /** One pending upgrade as the list endpoint returns it. */
  const pending = {
    id: 5,
    serviceId: 2,
    clientId: 9,
    clientName: 'Grace Hopper',
    fromProductId: 1,
    fromProductName: 'Basic',
    toProductId: 2,
    toProductName: 'Pro',
    type: 'Upgrade',
    status: 'Pending',
    prorationAmount: 4.5,
    currencyCode: 'USD',
    newRecurringAmount: 10,
    invoiceId: 77,
    note: null,
    createdAt: '2026-05-01T09:00:00Z',
    resolvedAt: null,
  }

  /** A resolved request: it offers no decision. */
  const completed = { ...pending, id: 6, status: 'Completed', invoiceId: null }

  /**
   * Whether the stubbed fetch saw a POST.
   *
   * @param mock - The stubbed fetch.
   * @returns True when a POST was made.
   */
  const posted = (mock: ReturnType<typeof vi.fn>): boolean =>
    mock.mock.calls.some(([, init]) => (init as RequestInit | undefined)?.method === 'POST')

  it('renders the request with its plans, type, status and links', async () => {
    stubRoutes({ 'GET /api/admin/service-change-requests': () => json({ items: [pending], totalCount: 1 }) })
    const wrapper = await mountRoutedSettled(ServiceChangeRequestsView, '/clients/plan-changes')

    const row = wrapper.get('[data-testid="plan-changes-row-5"]').text()
    expect(row).toContain('Basic → Pro')
    expect(row).toContain('Upgrade')
    expect(row).toContain('Pending')
    expect(row).toContain('$4.50 USD')
    expect(wrapper.get('[data-testid="plan-changes-client-5"]').attributes('href')).toBe('/clients/9')
    expect(wrapper.get('[data-testid="plan-changes-service-5"]').attributes('href')).toBe('/clients/9/services/2')
    expect(wrapper.get('[data-testid="plan-changes-invoice-5"]').attributes('href')).toBe('/billing/77')
  })

  it('offers no decision on a resolved request', async () => {
    stubRoutes({ 'GET /api/admin/service-change-requests': () => json({ items: [completed], totalCount: 1 }) })
    const wrapper = await mountRoutedSettled(ServiceChangeRequestsView, '/clients/plan-changes')

    expect(wrapper.find('[data-testid="plan-changes-approve-6"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="plan-changes-reject-6"]').exists()).toBe(false)
  })

  it('narrows to the service named in the route', async () => {
    const mock = stubRoutes({ 'GET /api/admin/service-change-requests': () => json({ items: [pending], totalCount: 1 }) })
    const { wrapper } = await mountRouted(ServiceChangeRequestsView, '/clients/plan-changes', '/clients/plan-changes?serviceId=2')
    await flushPromises()

    expect(mock.mock.calls[0]?.[0]).toBe('/api/admin/service-change-requests?serviceId=2&page=1&pageSize=20')
    expect(wrapper.find('[data-testid="plan-changes-clear-service"]').exists()).toBe(true)
  })

  it('asks first, then approves with the note and reports it', async () => {
    const mock = stubRoutes({
      'POST /api/admin/service-change-requests/5/approve': () => new Response(null, { status: 204 }),
      'GET /api/admin/service-change-requests': () => json({ items: [pending], totalCount: 1 }),
    })
    const wrapper = await mountRoutedSettled(ServiceChangeRequestsView, '/clients/plan-changes')

    // Two row actions sit behind the kebab menu, so it is opened first.
    await wrapper.get('[data-testid="plan-changes-actions-5"]').trigger('click')
    await wrapper.get('[data-testid="plan-changes-approve-5"]').trigger('click')
    expect(wrapper.find('[data-testid="plan-changes-dialog"]').exists()).toBe(true)
    expect(posted(mock)).toBe(false)

    await wrapper.get('[data-testid="plan-changes-note"]').setValue('Paid by phone')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    const post = mock.mock.calls.find(([, init]) => (init as RequestInit | undefined)?.method === 'POST')
    const [postedUrl, postedInit] = post ?? []
    expect(postedUrl).toBe('/api/admin/service-change-requests/5/approve')
    expect(JSON.parse((postedInit as RequestInit).body as string)).toEqual({ note: 'Paid by phone' })
    expect(wrapper.get('[data-testid="plan-changes-feedback"]').text()).toBe('Plan change approved.')
  })

  it('reports the sentence of a refused rejection', async () => {
    stubRoutes({
      'POST /api/admin/service-change-requests/5/reject': () => problem('Already resolved.', 409),
      'GET /api/admin/service-change-requests': () => json({ items: [pending], totalCount: 1 }),
    })
    const wrapper = await mountRoutedSettled(ServiceChangeRequestsView, '/clients/plan-changes')

    await wrapper.get('[data-testid="plan-changes-actions-5"]').trigger('click')
    await wrapper.get('[data-testid="plan-changes-reject-5"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="plan-changes-feedback"]').text()).toBe('Already resolved.')
    expect(wrapper.find('[data-testid="plan-changes-error"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="plan-changes-row-5"]').exists()).toBe(true)
  })

  it('shows the empty state and the refusal sentence', async () => {
    stubRoutes({ 'GET /api/admin/service-change-requests': () => json({ items: [], totalCount: 0 }) })
    const empty = await mountRoutedSettled(ServiceChangeRequestsView, '/clients/plan-changes')
    expect(empty.get('[data-testid="plan-changes-empty"]').text()).toContain('No plan change requests found')

    stubRoutes({ 'GET /api/admin/service-change-requests': () => problem('Not allowed.', 403) })
    const refused = await mountRoutedSettled(ServiceChangeRequestsView, '/clients/plan-changes')
    expect(refused.get('[data-testid="plan-changes-error"]').text()).toContain('Not allowed.')
  })
})
