/**
 * Tests for `pages/client/tickets/index.vue`: the states of the list and the filter tabs.
 */
import { createPinia } from 'pinia'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { mountPage, stubClientRuntime } from '~/tests/helpers/mountClient'
import TicketsPage from '~/pages/client/tickets/index.vue'
import { useClientStore } from '~/stores/client'
import type { ClientTicket } from '~/types/clientticket'
import { TicketPriority } from '~/types/ticketpriority'
import { TicketStatus } from '~/types/ticketstatus'

/** The mocked client endpoints the store reaches. */
const clientApi = vi.hoisted(() => ({ fetchTickets: vi.fn() }))

vi.mock('~/composables/apis/useClientApi', () => ({ useClientApi: () => clientApi }))

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

describe('tickets list page', () => {
  beforeEach(() => {
    clientApi.fetchTickets.mockReset()
    stubClientRuntime()
  })

  it('reads the tickets through the store and lists them', async () => {
    clientApi.fetchTickets.mockResolvedValue([ticket(1, TicketStatus.Open), ticket(2, TicketStatus.Closed)])

    const wrapper = await mountPage(TicketsPage)

    expect(clientApi.fetchTickets).toHaveBeenCalled()
    expect(wrapper.findAll('[data-testid^="ticket-row-"]')).toHaveLength(2)
  })

  it('reads the tickets again when the list was loaded on an earlier visit', async () => {
    clientApi.fetchTickets.mockResolvedValue([])
    const pinia = createPinia()
    useClientStore(pinia).ticketsLoaded = true
    await mountPage(TicketsPage, { pinia })
    expect(clientApi.fetchTickets).toHaveBeenCalled()
  })

  it('filters to the open tickets and to the closed ones', async () => {
    clientApi.fetchTickets.mockResolvedValue([ticket(1, TicketStatus.Open), ticket(2, TicketStatus.Closed), ticket(3, TicketStatus.Answered)])
    const wrapper = await mountPage(TicketsPage)

    await wrapper.find('[data-testid="ui-tab-current"]').trigger('click')
    expect(wrapper.findAll('[data-testid^="ticket-row-"]').map((row) => row.attributes('data-testid'))).toEqual(['ticket-row-1', 'ticket-row-3'])

    await wrapper.find('[data-testid="ui-tab-finished"]').trigger('click')
    expect(wrapper.findAll('[data-testid^="ticket-row-"]').map((row) => row.attributes('data-testid'))).toEqual(['ticket-row-2'])
  })

  it('shows the empty state with a way to open a ticket', async () => {
    clientApi.fetchTickets.mockResolvedValue([])

    const wrapper = await mountPage(TicketsPage)

    expect(wrapper.find('[data-testid="ui-empty-state"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="tickets-open-cta"]').attributes('href')).toBe('/client/tickets/new')
  })

  it('shows the failure, not the empty state, when the read failed', async () => {
    clientApi.fetchTickets.mockRejectedValue({ data: { detail: 'Support is down.' } })

    const wrapper = await mountPage(TicketsPage)

    expect(wrapper.find('[data-testid="tickets-error"]').text()).toBe('Support is down.')
    expect(wrapper.find('[data-testid="ui-empty-state"]').exists()).toBe(false)
  })

  it('shows the no-profile notice instead of the list for a staff account', async () => {
    clientApi.fetchTickets.mockResolvedValue([ticket(1, TicketStatus.Open)])
    const wrapper = await mountPage(TicketsPage)

    useClientStore().clientProfileMissing = true
    await wrapper.vm.$nextTick()

    expect(wrapper.find('[data-testid="no-profile-notice"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="tickets-list"]').exists()).toBe(false)
  })
})
