/**
 * Smoke tests for SupportTicketsView: the list, the status filter, the bulk bar and the empty and
 * error states, through the real store and transport with `fetch` stubbed.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import SupportTicketsView from '@/views/SupportTicketsView.vue'
import { TicketPriority, TicketStatus } from '@/types/ticket'
import { mountView } from '../helpers/mount'
import { json, problem, stubFetch, callOf } from '../helpers/fetch'
import { stubPathRoutes, stubRoutes } from '../helpers/routes'

/** One ticket row as the list endpoint returns it. */
const ticket = {
  id: 5,
  subject: 'DNS is down',
  status: TicketStatus.AwaitingReply,
  priority: TicketPriority.High,
  createdAt: '2026-10-01T00:00:00Z',
  replyCount: 1,
  departmentName: 'Support',
  lastReplyAt: null,
  isFlagged: false,
  clientId: 3,
  clientName: 'Ada Lovelace',
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('SupportTicketsView', () => {
  it('lists the tickets with subject, department and translated status', async () => {
    stubPathRoutes([{ path: '/tickets?', body: { items: [ticket], totalCount: 1 } }])

    const wrapper = await mountView(SupportTicketsView)
    const row = wrapper.get('[data-testid="support-tickets-row-5"]')

    expect(row.text()).toContain('DNS is down')
    expect(row.text()).toContain('Support')
    expect(row.text()).toContain('Awaiting Reply')
    expect(wrapper.get('[data-testid="support-tickets-count"]').text()).toContain('1')
  })

  it('reloads from the first page with the chosen status', async () => {
    const mock = stubPathRoutes([{ path: '/tickets?', body: { items: [ticket], totalCount: 1 } }])
    const wrapper = await mountView(SupportTicketsView)

    await wrapper.get('[data-testid="support-tickets-filter-closed"]').trigger('click')
    await flushPromises()

    expect(callOf(mock, 1).url).toBe('/api/tickets?page=1&pageSize=20&status=Closed')
  })

  it('offers an Awaiting Reply filter and no filter for a status that no longer exists', async () => {
    const mock = stubPathRoutes([{ path: '/tickets?', body: { items: [ticket], totalCount: 1 } }])
    const wrapper = await mountView(SupportTicketsView)

    expect(wrapper.find('[data-testid="support-tickets-filter-answered"]').exists()).toBe(false)

    await wrapper.get('[data-testid="support-tickets-filter-awaiting-reply"]').trigger('click')
    await flushPromises()

    expect(callOf(mock, 1).url).toBe('/api/tickets?page=1&pageSize=20&status=AwaitingReply')
  })

  it('shows the bulk bar for a selection and sends the chosen action', async () => {
    const mock = stubPathRoutes([
      { path: '/tickets?', body: { items: [ticket], totalCount: 1 } },
      { path: '/tickets/bulk', body: null },
    ])
    const wrapper = await mountView(SupportTicketsView)
    expect(wrapper.find('[data-testid="support-tickets-bulk"]').exists()).toBe(false)

    await wrapper.get('[data-testid="support-tickets-select-5"]').trigger('click')
    await wrapper.get('[data-testid="support-tickets-bulk-close"]').trigger('click')
    await flushPromises()

    expect(callOf(mock, 1)).toMatchObject({ url: '/api/tickets/bulk', method: 'POST', body: { ticketIds: [5], action: 'close' } })
    expect(wrapper.get('[data-testid="support-tickets-feedback"]').text()).toBe('Changes saved successfully.')
  })

  it('deletes the selection only after the danger confirmation', async () => {
    const mock = stubPathRoutes([
      { path: '/tickets?', body: { items: [ticket], totalCount: 1 } },
      { path: '/tickets/bulk', body: null },
    ])
    const wrapper = await mountView(SupportTicketsView)

    await wrapper.get('[data-testid="support-tickets-select-5"]').trigger('click')
    await wrapper.get('[data-testid="support-tickets-bulk-delete"]').trigger('click')
    await wrapper.get('[data-testid="dialog-cancel"]').trigger('click')
    await flushPromises()
    expect(mock.mock.calls.some(([url]) => String(url).includes('/tickets/bulk'))).toBe(false)

    await wrapper.get('[data-testid="support-tickets-bulk-delete"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(callOf(mock, 1)).toMatchObject({ url: '/api/tickets/bulk', method: 'POST', body: { ticketIds: [5], action: 'delete' } })
    expect(wrapper.get('[data-testid="support-tickets-feedback"]').text()).toBe('Record deleted.')
  })

  it('shows the API sentence when a bulk action is refused', async () => {
    stubRoutes({
      'POST /api/tickets/bulk': () => problem('Tickets are locked.', 409),
      'GET /api/tickets': () => json({ items: [ticket], totalCount: 1 }),
    })
    const wrapper = await mountView(SupportTicketsView)

    await wrapper.get('[data-testid="support-tickets-select-5"]').trigger('click')
    await wrapper.get('[data-testid="support-tickets-bulk-close"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="support-tickets-feedback"]').text()).toBe('Tickets are locked.')
    expect(wrapper.find('[data-testid="support-tickets-error"]').exists()).toBe(false)
  })

  it('shows the empty state when nothing matches', async () => {
    stubPathRoutes([{ path: '/tickets?', body: { items: [], totalCount: 0 } }])

    const wrapper = await mountView(SupportTicketsView)

    expect(wrapper.get('[data-testid="support-tickets-table"]').text()).toContain('No tickets found')
  })

  it('shows the API sentence when the list is refused', async () => {
    stubFetch(problem('Not allowed.', 403))

    const wrapper = await mountView(SupportTicketsView)

    expect(wrapper.get('[data-testid="support-tickets-error"]').text()).toBe('Not allowed.')
  })

  it('loads the list again from the retry of a refused load', async () => {
    let calls = 0
    stubRoutes({
      'GET /api/tickets': () => {
        calls += 1
        return calls === 1 ? problem('Not allowed.', 403) : json({ items: [ticket], totalCount: 1 })
      },
    })
    const wrapper = await mountView(SupportTicketsView)

    await wrapper.get('[data-testid="support-tickets-error-retry"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="support-tickets-error"]').exists()).toBe(false)
  })

  it('names the client who opened the ticket', async () => {
    stubPathRoutes([{ path: '/tickets?', body: { items: [ticket], totalCount: 1 } }])

    const wrapper = await mountView(SupportTicketsView)

    expect(wrapper.get('[data-testid="support-tickets-row-5"]').text()).toContain('Ada Lovelace')
  })
})
