/**
 * Mount tests for ClientTicketDetailView: it renders against the real kit and settles on an API
 * refusal with a Retry, each change reports its outcome under the header, and a delete is confirmed
 * first and leaves for the tickets list.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import ClientTicketDetailView from '@/views/ClientTicketDetailView.vue'
import { mountClientView } from '../helpers/clientViews'
import { json, problem } from '../helpers/fetch'
import { mountRouted } from '../helpers/mount'
import { stubRoutes } from '../helpers/routes'

/** Ticket 4 of client 7, open, without replies. */
const ticket = () =>
  json({
    id: 4,
    clientId: 7,
    subject: 'Mail is down',
    message: 'Nothing arrives.',
    status: 'Open',
    priority: 'Medium',
    department: { id: null, name: null },
    assignee: { id: null, name: null },
    createdAt: '2026-10-01T00:00:00Z',
    replies: [],
  })

/**
 * Mounts the screen for ticket 4 of client 7 and waits for its first load.
 *
 * @returns The wrapper and its router.
 */
const mountTicket = async () => {
  const mounted = await mountRouted(ClientTicketDetailView, '/clients/:id/tickets/:ticketId', '/clients/7/tickets/4')
  await flushPromises()
  return mounted
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('ClientTicketDetailView', () => {
  it('draws the real layout with chat bubble and field placeholders while the ticket loads', async () => {
    vi.stubGlobal('fetch', vi.fn(() => new Promise<Response>(() => undefined)))
    const { wrapper } = await mountTicket()

    expect(wrapper.find('[data-testid="client-ticket-loading"]').exists()).toBe(true)
    expect(wrapper.findAll('[data-testid="client-ticket-message-loading"]')).toHaveLength(2)
    expect(wrapper.text()).toContain('Ticket Settings')
    expect(wrapper.findAll('[data-testid="ui-field-skeleton"]').length).toBeGreaterThan(0)
    expect(wrapper.get('[data-testid="client-ticket-save"]').attributes('disabled')).toBeDefined()
  })

  it('mounts without throwing', async () => {
    const wrapper = await mountClientView(ClientTicketDetailView)

    expect(wrapper.exists()).toBe(true)
  })

  it('shows the refusal sentence from the API', async () => {
    const wrapper = await mountClientView(ClientTicketDetailView, 'Server said no')

    expect(wrapper.text()).toContain('Server said no')
  })

  it('reports a closed ticket under the header', async () => {
    stubRoutes({
      'GET /api/tickets/departments': () => json([]),
      'PUT /api/tickets/4': () => new Response(null, { status: 204 }),
      'GET /api/tickets/4': ticket,
    })
    const { wrapper } = await mountTicket()

    await wrapper.get('[data-testid="client-ticket-close"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="client-ticket-feedback"]').text()).toBe('Ticket closed.')
  })

  it('deletes only after the confirmation and leaves for the tickets list', async () => {
    const mock = stubRoutes({
      'GET /api/tickets/departments': () => json([]),
      'DELETE /api/tickets/4': () => new Response(null, { status: 204 }),
      'GET /api/tickets/4': ticket,
    })
    const { wrapper, router } = await mountTicket()

    await wrapper.get('[data-testid="client-ticket-delete"]').trigger('click')
    expect(mock.mock.calls.some(([, init]) => (init as RequestInit | undefined)?.method === 'DELETE')).toBe(false)
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.path).toBe('/clients/7/tickets')
  })

  it('shows the API sentence when the delete is refused and stays', async () => {
    stubRoutes({
      'GET /api/tickets/departments': () => json([]),
      'DELETE /api/tickets/4': () => problem('The ticket is locked.', 409),
      'GET /api/tickets/4': ticket,
    })
    const { wrapper, router } = await mountTicket()

    await wrapper.get('[data-testid="client-ticket-delete"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="client-ticket-feedback"]').text()).toBe('The ticket is locked.')
    expect(router.currentRoute.value.path).toBe('/clients/7/tickets/4')
  })

  it('keeps the ticket when the confirmation is cancelled', async () => {
    const mock = stubRoutes({ 'GET /api/tickets/departments': () => json([]), 'GET /api/tickets/4': ticket })
    const { wrapper } = await mountTicket()

    await wrapper.get('[data-testid="client-ticket-delete"]').trigger('click')
    await wrapper.get('[data-testid="dialog-cancel"]').trigger('click')
    await flushPromises()

    expect(mock.mock.calls.some(([, init]) => (init as RequestInit | undefined)?.method === 'DELETE')).toBe(false)
  })

  it('shows the departments sentence with a Retry in place of the selector, then the selector again', async () => {
    let healthy = false
    const mock = stubRoutes({
      'GET /api/tickets/departments': () => (healthy ? json([{ id: 1, name: 'Support', email: 'support@example.test' }]) : problem('Departments unavailable.', 500)),
      'GET /api/tickets/4': ticket,
    })
    const { wrapper } = await mountTicket()

    expect(wrapper.get('[data-testid="client-ticket-departments-error"]').text()).toBe('Departments unavailable.')
    expect(wrapper.find('[data-testid="client-ticket-department"]').exists()).toBe(false)

    healthy = true
    await wrapper.get('[data-testid="client-ticket-departments-error-retry"]').trigger('click')
    await flushPromises()

    expect(mock.mock.calls.filter(([url]) => String(url).includes('/api/tickets/departments'))).toHaveLength(2)
    expect(wrapper.find('[data-testid="client-ticket-departments-error"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="client-ticket-department"]').exists()).toBe(true)
  })

  it('reloads the ticket from the Retry of a refused load', async () => {
    let refused = true
    stubRoutes({
      'GET /api/tickets/departments': () => json([]),
      'GET /api/tickets/4': () => (refused ? problem('Ticket is unavailable.', 503) : ticket()),
    })
    const { wrapper } = await mountTicket()

    refused = false
    await wrapper.get('[data-testid="client-ticket-error-retry"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="client-ticket-error"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="client-ticket-close"]').exists()).toBe(true)
  })
})
