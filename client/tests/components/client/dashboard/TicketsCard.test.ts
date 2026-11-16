/**
 * Unit tests for ClientDashboardTicketsCard: the latest tickets and the card's states.
 *
 * @module tests/components/client/dashboard/TicketsCard.test
 */

import { describe, expect, it } from 'vitest'
import { nextTick } from 'vue'
import { mountClient } from '~/tests/helpers/mountClient'
import TicketsCard from '~/components/client/dashboard/TicketsCard.vue'
import { useClientStore } from '~/stores/client'
import type { ClientTicket } from '~/types/clientticket'
import { TicketStatus } from '~/types/ticketstatus'
import { CLIENT_TICKET_NEW_PATH, clientTicketPath } from '~/constants/clientRoutes'

/**
 * Builds a ticket row.
 *
 * @param id - Ticket id.
 * @returns An open ticket in the Sales department.
 */
const ticket = (id: number): ClientTicket =>
  ({ id, subject: `Subject ${id}`, departmentName: 'Sales', status: TicketStatus.Open }) as unknown as ClientTicket

describe('ClientDashboardTicketsCard', () => {
  it('always offers to open a new ticket from the header', () => {
    const wrapper = mountClient(TicketsCard)

    expect(wrapper.find('[data-testid="dashboard-ticket-new"]').attributes('href')).toBe(CLIENT_TICKET_NEW_PATH)
  })

  it('shows skeletons while loading', async () => {
    const wrapper = mountClient(TicketsCard)
    useClientStore().ticketsLoading = true
    await nextTick()

    expect(wrapper.findAll('[data-testid="ui-skeleton"]')).toHaveLength(3)
  })

  it('invites the first ticket when there are none', () => {
    const wrapper = mountClient(TicketsCard)

    expect(wrapper.find('[data-testid="dashboard-tickets-empty"]').text()).toContain('client.dashboard.noTickets')
    expect(wrapper.find('[data-testid="dashboard-ticket-open"]').attributes('href')).toBe(CLIENT_TICKET_NEW_PATH)
  })

  it('lists at most four tickets with subject, department and status', async () => {
    const wrapper = mountClient(TicketsCard)
    useClientStore().tickets = [1, 2, 3, 4, 5].map(ticket)
    await nextTick()

    expect(wrapper.find('[data-testid="dashboard-ticket-5"]').exists()).toBe(false)
    const row = wrapper.find('[data-testid="dashboard-ticket-1"]')
    expect(row.attributes('href')).toBe(clientTicketPath(1))
    expect(row.text()).toContain('Subject 1')
    expect(row.text()).toContain('Sales')
    expect(row.find('[data-testid="status-badge"]').exists()).toBe(true)
  })
})
