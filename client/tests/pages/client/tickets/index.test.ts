/**
 * Tests for `pages/client/tickets/index.vue` against the real client store, API composable and
 * transport, with a stub BFF answering on a real socket (nothing of ours is mocked): the states of
 * the list and the filter tabs.
 *
 * @module tests/pages/client/tickets/index.test
 */
import { createPinia } from 'pinia'
import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { mountPage, stubClientRuntime } from '~/tests/helpers/mountClient'
import TicketsPage from '~/pages/client/tickets/index.vue'
import { useClientStore } from '~/stores/client'
import { makeClientUser } from '~/tests/helpers/clientUser'
import { requestsTo, routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import { Permission } from '~/types/permission'
import type { ClientTicket } from '~/types/clientticket'
import { TicketPriority } from '~/types/ticketpriority'
import { TicketStatus } from '~/types/ticketstatus'
import type { Pinia } from 'pinia'
import { waitUntil } from '~/tests/helpers/waitUntil'

/** The BFF paths the page reads. */
const PATHS = {
  me: '/api/portal/client/me',
  tickets: '/api/portal/client/tickets',
} as const

/** The stub playing the BFF. */
let stub: StubServer

/** The replies of the current test; a test changes them before it mounts. */
let table: StubRouteTable

/**
 * Builds a ticket with the fields the row reads.
 *
 * @param id - Ticket id.
 * @param status - Ticket status.
 * @returns A ticket.
 */
const ticket = (id: number, status: TicketStatus): ClientTicket =>
  ({
    id, subject: `Subject ${id}`, departmentName: 'Support', createdAt: '2026-01-01T10:00:00Z', lastReplyAt: null,
    priority: TicketPriority.Medium, status, replyCount: 0
  })

/**
 * Mounts the page and waits until it has rendered: the person and the tickets are read over a real
 * socket, so the page's async setup ends after the mount's own promise flush.
 *
 * @param pinia - A Pinia the test filled before mounting; a fresh one when omitted.
 * @returns The mounted page.
 */
const mountTickets = async (pinia?: Pinia): Promise<Awaited<ReturnType<typeof mountPage>>> => {
  const wrapper = await mountPage(TicketsPage, { pinia })
  await waitUntil(() => expect(wrapper.find('[data-testid="tickets-open-new"]').exists()).toBe(true))
  return wrapper
}

describe('tickets list page', () => {
  beforeEach(async () => {
    stubClientRuntime()
    table = { [`GET ${PATHS.me}`]: { body: makeClientUser({ permissions: Permission.All }) } }
    stub = await startTransport(routeTable(table))
  })

  afterEach(async () => {
    await stub.close()
  })

  it('reads the tickets through the store and lists them', async () => {
    table[`GET ${PATHS.tickets}`] = { body: [ticket(1, TicketStatus.Open), ticket(2, TicketStatus.Closed)] }

    const wrapper = await mountTickets()

    expect(requestsTo(stub.requests, PATHS.tickets)).toHaveLength(1)
    expect(wrapper.findAll('[data-testid^="ticket-row-"]')).toHaveLength(2)
  })

  it('reads the tickets again when the list was loaded on an earlier visit', async () => {
    table[`GET ${PATHS.tickets}`] = { body: [] }
    const pinia = createPinia()
    useClientStore(pinia).ticketsLoaded = true
    await mountTickets(pinia)
    expect(requestsTo(stub.requests, PATHS.tickets)).toHaveLength(1)
  })

  it('filters to the open tickets and to the closed ones', async () => {
    table[`GET ${PATHS.tickets}`] = { body: [ticket(1, TicketStatus.Open), ticket(2, TicketStatus.Closed), ticket(3, TicketStatus.AwaitingReply)] }
    const wrapper = await mountTickets()

    await wrapper.find('[data-testid="ui-tab-current"]').trigger('click')
    expect(wrapper.findAll('[data-testid^="ticket-row-"]').map((row) => row.attributes('data-testid'))).toEqual(['ticket-row-1', 'ticket-row-3'])

    await wrapper.find('[data-testid="ui-tab-finished"]').trigger('click')
    expect(wrapper.findAll('[data-testid^="ticket-row-"]').map((row) => row.attributes('data-testid'))).toEqual(['ticket-row-2'])
  })

  it('shows the empty state with a way to open a ticket', async () => {
    table[`GET ${PATHS.tickets}`] = { body: [] }

    const wrapper = await mountTickets()

    expect(wrapper.find('[data-testid="ui-empty-state"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="tickets-open-cta"]').attributes('href')).toBe('/client/tickets/new')
  })

  it('shows the failure, not the empty state, when the read failed', async () => {
    table[`GET ${PATHS.tickets}`] = { status: 400, body: { detail: 'Support is down.' } }

    const wrapper = await mountTickets()

    expect(wrapper.find('[data-testid="tickets-error"]').text()).toBe('Support is down.')
    expect(wrapper.find('[data-testid="ui-empty-state"]').exists()).toBe(false)
  })

  it('shows the no-profile notice instead of the list for a staff account', async () => {
    table[`GET ${PATHS.tickets}`] = { body: [ticket(1, TicketStatus.Open)] }
    const wrapper = await mountTickets()

    useClientStore().clientProfileMissing = true
    await wrapper.vm.$nextTick()

    expect(wrapper.find('[data-testid="no-profile-notice"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="tickets-list"]').exists()).toBe(false)
  })
})
