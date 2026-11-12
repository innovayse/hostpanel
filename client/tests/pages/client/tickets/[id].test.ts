/**
 * Tests for `pages/client/tickets/[id].vue` against the real support and auth stores, API
 * composable and transport, with a stub BFF answering on a real socket (nothing of ours is mocked):
 * the thread, the reply form of an open ticket, the notice of a closed one and the not-found state.
 *
 * @module tests/pages/client/tickets/[id].test
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { mountPage, stubClientRuntime } from '~/tests/helpers/mountClient'
import TicketPage from '~/pages/client/tickets/[id].vue'
import { useAuthStore } from '~/stores/auth'
import { makeClientUser } from '~/tests/helpers/clientUser'
import { bodyOf } from '~/tests/helpers/requestBody'
import { requestsTo, routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import { TicketPriority } from '~/types/ticketpriority'
import { TicketStatus } from '~/types/ticketstatus'
import type { TicketThread } from '~/types/ticketthread'
import { waitUntil } from '~/tests/helpers/waitUntil'

/** The path of the ticket the route names. */
const TICKET_PATH = '/api/portal/client/tickets/12'

/** The path that posts a reply to that ticket. */
const REPLY_PATH = `${TICKET_PATH}/reply`

/** The stub playing the BFF. */
let stub: StubServer

/** The replies of the current test; a test changes them before it mounts. */
let table: StubRouteTable

/**
 * Builds a ticket thread.
 *
 * @param status - Ticket status.
 * @returns The thread with one client message and one staff reply.
 */
const thread = (status: TicketStatus): TicketThread => ({
  id: 12, subject: 'Site is down', status, priority: TicketPriority.High, departmentId: 1, departmentName: 'Support',
  createdAt: '2026-01-01T10:00:00Z', message: 'My site is down.',
  rating: null, feedbackComment: null, feedbackAt: null,
  replies: [{ id: 1, authorName: 'Grace', isStaffReply: true, createdAt: '2026-01-01T11:00:00Z', message: 'Looking into it.' }]
})

/**
 * Mounts the page and waits until it has rendered: the ticket is read over a real socket, so the page's
 * async setup ends after the mount's own promise flush.
 *
 * @returns The mounted page.
 */
const mountTicket = async (): Promise<Awaited<ReturnType<typeof mountPage>>> => {
  const wrapper = await mountPage(TicketPage)
  await waitUntil(() => expect(wrapper.find('[data-testid="ticket-back"]').exists()).toBe(true))
  return wrapper
}

describe('ticket page', () => {
  beforeEach(async () => {
    stubClientRuntime({ params: { id: '12' } })
    vi.stubGlobal('useAuthStore', useAuthStore)
    table = { [`GET ${TICKET_PATH}`]: { body: thread(TicketStatus.Open) } }
    stub = await startTransport(routeTable(table))
  })

  afterEach(async () => {
    await stub.close()
  })

  it('shows the header and the whole conversation', async () => {
    const wrapper = await mountTicket()

    expect(requestsTo(stub.requests, TICKET_PATH)).toHaveLength(1)
    expect(wrapper.find('[data-testid="ticket-header"]').text()).toContain('Site is down')
    expect(wrapper.findAll('[data-testid="ticket-message"]')).toHaveLength(2)
    expect(wrapper.text()).toContain('Grace (client.tickets.support)')
  })

  it('names a reply with an empty author as unknown', async () => {
    const nameless = thread(TicketStatus.Open)
    nameless.replies[0]!.authorName = ''
    table[`GET ${TICKET_PATH}`] = { body: nameless }

    const wrapper = await mountTicket()

    expect(wrapper.text()).toContain('fallback.unknown (client.tickets.support)')
  })

  it('offers the reply form while the ticket is open', async () => {
    table[`GET ${TICKET_PATH}`] = { body: thread(TicketStatus.AwaitingReply) }

    const wrapper = await mountTicket()

    expect(wrapper.find('[data-testid="ticket-reply-message"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="ticket-closed-notice"]').exists()).toBe(false)
  })

  it('replaces the reply form with a notice once the ticket is closed', async () => {
    table[`GET ${TICKET_PATH}`] = { body: thread(TicketStatus.Closed) }

    const wrapper = await mountTicket()

    expect(wrapper.find('[data-testid="ticket-reply-message"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="ticket-closed-notice"]').exists()).toBe(true)
  })

  it('sends the reply, clears the box and reloads the thread', async () => {
    table[`POST ${REPLY_PATH}`] = { body: {} }
    const wrapper = await mountTicket()
    useAuthStore().user = makeClientUser({ email: 'ada@example.com' })

    await wrapper.find('[data-testid="ticket-reply-message"]').setValue('Thanks')
    await wrapper.find('form').trigger('submit')

    // The backend requires the name the reply is posted under, so the page sends the person's own.
    await waitUntil(() => expect(requestsTo(stub.requests, REPLY_PATH)).toHaveLength(1))
    expect(bodyOf(requestsTo(stub.requests, REPLY_PATH)[0])).toEqual({ message: 'Thanks' })
    await waitUntil(() => expect(requestsTo(stub.requests, TICKET_PATH)).toHaveLength(2))
  })

  it('does not send an empty reply', async () => {
    const wrapper = await mountTicket()

    await wrapper.find('[data-testid="ticket-reply-message"]').setValue('   ')
    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(requestsTo(stub.requests, REPLY_PATH)).toHaveLength(0)
  })

  it('says the ticket was not found when the read fails', async () => {
    table[`GET ${TICKET_PATH}`] = { status: 404, body: { detail: 'No such ticket.' } }

    const wrapper = await mountTicket()

    expect(wrapper.find('[data-testid="ui-empty-state"]').text()).toContain('client.tickets.notFound')
  })
})
