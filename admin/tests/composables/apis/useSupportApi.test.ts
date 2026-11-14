/**
 * Tests for the support API composable: paths, verbs, query strings and bodies through the real
 * `useApi` transport with `fetch` stubbed.
 */

import { afterEach, describe, expect, it, vi } from 'vitest'
import { useSupportApi } from '@/composables/apis/useSupportApi'
import { json, problem, stubFetch, callOf } from '../../helpers/fetch'
import { SupportOverviewPeriod } from '@/types/supportoverviewstats'
import { TicketBulkAction, TicketPriority, TicketStatus } from '@/types/ticket'

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('useSupportApi', () => {
  it('listTickets sends paging, search, status, priority, department and flag, dropping empty filters', async () => {
    const mock = stubFetch(json({ items: [], totalCount: 0 }))

    await useSupportApi().listTickets({
      page: 2,
      pageSize: 20,
      search: 'dns',
      status: TicketStatus.Open,
      priority: TicketPriority.High,
      departmentId: 3,
      flagged: true,
    })
    expect(callOf(mock)).toMatchObject({
      url: '/api/tickets?page=2&pageSize=20&search=dns&status=Open&priority=High&departmentId=3&flagged=true',
      method: 'GET',
    })

    await useSupportApi().listTickets({ page: 1, pageSize: 20, search: '' })
    expect(callOf(mock, 1).url).toBe('/api/tickets?page=1&pageSize=20')
  })

  it('getTicket, getOverview and listDepartments read their paths', async () => {
    const mock = stubFetch(json({}))
    const api = useSupportApi()

    await api.getTicket(5)
    await api.getOverview(SupportOverviewPeriod.Last7Days)
    await api.listDepartments()
    expect(callOf(mock, 0).url).toBe('/api/tickets/5')
    expect(callOf(mock, 1).url).toBe('/api/tickets/overview?period=last7days')
    expect(callOf(mock, 2).url).toBe('/api/tickets/departments')
  })

  it('createTicket POSTs the payload and returns the id', async () => {
    const mock = stubFetch(json(9))
    const payload = { clientId: 1, subject: 'Hi', message: 'Help', departmentId: 2, priority: TicketPriority.High }

    expect(await useSupportApi().createTicket(payload)).toBe(9)
    expect(callOf(mock)).toMatchObject({ url: '/api/tickets', method: 'POST', body: payload })
  })

  it('updateTicket PUTs, deleteTicket DELETEs', async () => {
    const mock = stubFetch(new Response(null, { status: 204 }))
    const api = useSupportApi()

    await api.updateTicket(5, { status: TicketStatus.Closed })
    await api.deleteTicket(5)
    expect(callOf(mock, 0)).toMatchObject({ url: '/api/tickets/5', method: 'PUT', body: { status: 'Closed' } })
    expect(callOf(mock, 1)).toMatchObject({ url: '/api/tickets/5', method: 'DELETE' })
  })

  it('replyToTicket, toggleFlag and bulkAction POST to their paths', async () => {
    const mock = stubFetch(new Response(null, { status: 204 }))
    const api = useSupportApi()

    await api.replyToTicket(5, { message: 'Done' })
    await api.toggleFlag(5)
    await api.bulkAction({ ticketIds: [1, 2], action: TicketBulkAction.Close })
    expect(callOf(mock, 0)).toMatchObject({ url: '/api/tickets/5/reply', method: 'POST', body: { message: 'Done' } })
    expect(callOf(mock, 1)).toMatchObject({ url: '/api/tickets/5/toggle-flag', method: 'POST' })
    expect(callOf(mock, 2)).toMatchObject({ url: '/api/tickets/bulk', method: 'POST', body: { ticketIds: [1, 2], action: 'close' } })
  })

  it('rejects with the problem body on a refusal', async () => {
    stubFetch(problem('No such ticket.', 404))

    await expect(useSupportApi().getTicket(5)).rejects.toMatchObject({ status: 404 })
  })
})
