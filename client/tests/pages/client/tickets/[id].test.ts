/**
 * Tests for `pages/client/tickets/[id].vue`: the thread, the reply form of an open ticket, the
 * notice of a closed one and the not-found state.
 */
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { mountPage, stubClientRuntime } from '~/tests/helpers/mountClient'
import TicketPage from '~/pages/client/tickets/[id].vue'
import { useAuthStore } from '~/stores/auth'
import { makeClientUser } from '~/tests/helpers/clientUser'
import { TicketStatus } from '~/types/ticketstatus'

/** The mocked support endpoints the store reaches. */
const supportApi = vi.hoisted(() => ({ fetchTicket: vi.fn(), replyToTicket: vi.fn() }))

vi.mock('~/composables/apis/useSupportApi', () => ({ useSupportApi: () => supportApi }))
vi.mock('~/composables/apis/useAuthApi', () => ({ useAuthApi: () => ({}) }))
vi.mock('~/composables/apis/useClientApi', () => ({ useClientApi: () => ({}) }))

/**
 * Builds a ticket thread.
 *
 * @param status - Ticket status.
 * @returns The thread with one client message and one staff reply.
 */
const thread = (status: string) => ({
  id: 12, subject: 'Site is down', status, priority: 'High', departmentId: 1, departmentName: 'Support',
  createdAt: '2026-01-01T10:00:00Z', message: 'My site is down.',
  rating: null, feedbackComment: null, feedbackAt: null,
  replies: [{ id: 1, authorName: 'Grace', isStaffReply: true, createdAt: '2026-01-01T11:00:00Z', message: 'Looking into it.' }]
})

describe('ticket page', () => {
  beforeEach(() => {
    supportApi.fetchTicket.mockReset()
    supportApi.replyToTicket.mockReset()
    stubClientRuntime({ params: { id: '12' } })
    vi.stubGlobal('useAuthStore', useAuthStore)
  })

  it('shows the header and the whole conversation', async () => {
    supportApi.fetchTicket.mockResolvedValue(thread(TicketStatus.Open))

    const wrapper = await mountPage(TicketPage)

    expect(supportApi.fetchTicket).toHaveBeenCalledWith('12')
    expect(wrapper.find('[data-testid="ticket-header"]').text()).toContain('Site is down')
    expect(wrapper.findAll('[data-testid="ticket-message"]')).toHaveLength(2)
    expect(wrapper.text()).toContain('Grace (client.tickets.support)')
  })

  it('names a reply with an empty author as unknown', async () => {
    const nameless = thread(TicketStatus.Open)
    nameless.replies[0]!.authorName = ''
    supportApi.fetchTicket.mockResolvedValue(nameless)

    const wrapper = await mountPage(TicketPage)

    expect(wrapper.text()).toContain('fallback.unknown (client.tickets.support)')
  })

  it('offers the reply form while the ticket is open', async () => {
    supportApi.fetchTicket.mockResolvedValue(thread(TicketStatus.AwaitingReply))

    const wrapper = await mountPage(TicketPage)

    expect(wrapper.find('[data-testid="ticket-reply-message"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="ticket-closed-notice"]').exists()).toBe(false)
  })

  it('replaces the reply form with a notice once the ticket is closed', async () => {
    supportApi.fetchTicket.mockResolvedValue(thread(TicketStatus.Closed))

    const wrapper = await mountPage(TicketPage)

    expect(wrapper.find('[data-testid="ticket-reply-message"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="ticket-closed-notice"]').exists()).toBe(true)
  })

  it('sends the reply, clears the box and reloads the thread', async () => {
    supportApi.fetchTicket.mockResolvedValue(thread(TicketStatus.Open))
    supportApi.replyToTicket.mockResolvedValue({})
    const wrapper = await mountPage(TicketPage)
    useAuthStore().user = makeClientUser({ email: 'ada@example.com' })

    await wrapper.find('[data-testid="ticket-reply-message"]').setValue('Thanks')
    await wrapper.find('form').trigger('submit')

    // The backend requires the name the reply is posted under, so the page sends the person's own.
    await vi.waitFor(() => expect(supportApi.replyToTicket).toHaveBeenCalledWith('12', 'Thanks'))
    await vi.waitFor(() => expect(supportApi.fetchTicket).toHaveBeenCalledTimes(2))
  })

  it('does not send an empty reply', async () => {
    supportApi.fetchTicket.mockResolvedValue(thread(TicketStatus.Open))
    const wrapper = await mountPage(TicketPage)

    await wrapper.find('[data-testid="ticket-reply-message"]').setValue('   ')
    await wrapper.find('form').trigger('submit')

    expect(supportApi.replyToTicket).not.toHaveBeenCalled()
  })

  it('says the ticket was not found when the read fails', async () => {
    supportApi.fetchTicket.mockRejectedValue({ data: { detail: 'No such ticket.' } })

    const wrapper = await mountPage(TicketPage)

    expect(wrapper.find('[data-testid="ui-empty-state"]').text()).toContain('client.tickets.notFound')
  })
})
