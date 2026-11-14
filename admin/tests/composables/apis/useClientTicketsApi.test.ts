/**
 * Tests for the client tickets API composable: paths, verbs, query strings and bodies through
 * the real `useApi` transport with `fetch` stubbed.
 */

import { afterEach, describe, expect, it, vi } from 'vitest'
import { useClientTicketsApi } from '@/composables/apis/useClientTicketsApi'
import { ApiError } from '@/composables/useApi'
import { json, problem, stubFetch, callOf } from '../../helpers/fetch'
import { TicketPriority, TicketStatus } from '@/types/ticket'

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('useClientTicketsApi', () => {
  it('list drops the filters that are not set and sends the ones that are', async () => {
    const mock = stubFetch(json({ items: [], totalCount: 0 }))
    const api = useClientTicketsApi()

    await api.list(7, { page: 1, pageSize: 20 })
    await api.list(7, {
      page: 2,
      pageSize: 20,
      search: 'refund',
      status: TicketStatus.Open,
      priority: TicketPriority.Medium,
      departmentId: 5,
    })
    expect(callOf(mock, 0).url).toBe('/api/tickets/client/7?page=1&pageSize=20')
    expect(callOf(mock, 1).url).toBe('/api/tickets/client/7?page=2&pageSize=20&search=refund&status=Open&priority=Medium&departmentId=5')
  })

  it('stats, get and departments read their endpoints', async () => {
    const mock = stubFetch(json({}))
    const api = useClientTicketsApi()

    await api.stats(7)
    await api.get(3)
    await api.departments()
    expect(callOf(mock, 0).url).toBe('/api/tickets/client/7/stats')
    expect(callOf(mock, 1).url).toBe('/api/tickets/3')
    expect(callOf(mock, 2).url).toBe('/api/tickets/departments')
  })

  it('create POSTs the payload and returns the id', async () => {
    const mock = stubFetch(json(15))
    const payload = { clientId: 7, subject: 's', message: 'm', departmentId: 1, priority: TicketPriority.High }

    expect(await useClientTicketsApi().create(payload)).toBe(15)
    expect(callOf(mock)).toMatchObject({ url: '/api/tickets', method: 'POST', body: payload })
  })

  it('update, remove and reply use the ticket paths', async () => {
    const mock = stubFetch(new Response(null, { status: 204 }))
    const api = useClientTicketsApi()

    await api.update(3, { status: TicketStatus.Closed })
    await api.remove(3)
    await api.reply(3, 'hello')
    expect(callOf(mock, 0)).toMatchObject({ url: '/api/tickets/3', method: 'PUT', body: { status: 'Closed' } })
    expect(callOf(mock, 1)).toMatchObject({ url: '/api/tickets/3', method: 'DELETE' })
    expect(callOf(mock, 2)).toMatchObject({ url: '/api/tickets/3/reply', method: 'POST', body: { message: 'hello' } })
  })

  it('rejects with an ApiError carrying the refusal body', async () => {
    stubFetch(problem('Ticket is closed.', 409))

    await expect(useClientTicketsApi().reply(3, 'x')).rejects.toBeInstanceOf(ApiError)
  })
})
