/**
 * Tests for the support store: loads fill state, a refusal puts the API's sentence into `error`,
 * an unreachable API puts the shared no-answer sentence, and a refused create exposes the field
 * messages. The requests go through the real `useApi` transport.
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { translate } from '@/lib/translate'
import { useSupportStore } from '@/stores/support'
import { json, problem, stubFetch, stubOffline, callOf, invalid } from '../helpers/fetch'
import { SupportOverviewPeriod } from '@/types/supportoverviewstats'
import { TicketBulkAction, TicketPriority, TicketStatus } from '@/types/ticket'

describe('supportStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('fetchAll sends the page and the filters it is given and fills the list', async () => {
    const mock = stubFetch(json({ items: [{ id: 1 }], totalCount: 41 }))
    const store = useSupportStore()

    expect(await store.fetchAll(2, {
      search: ' dns ',
      status: TicketStatus.Open,
      priority: TicketPriority.High,
      departmentId: 3,
      flagged: true,
    })).toBe(true)
    expect(callOf(mock).url).toBe('/api/tickets?page=2&pageSize=20&search=dns&status=Open&priority=High&departmentId=3&flagged=true')
    expect(store.tickets).toEqual([{ id: 1 }])
    expect(store.totalCount).toBe(41)
    expect(store.loading).toBe(false)
  })

  it('fetchAll reports the API sentence on refusal', async () => {
    stubFetch(problem('Not allowed.', 403))
    const store = useSupportStore()

    expect(await store.fetchAll(1)).toBe(false)
    expect(store.error).toBe('Not allowed.')
    expect(store.loading).toBe(false)
  })

  it('fetchAll reports the no-answer sentence offline and clears it on the next success', async () => {
    const store = useSupportStore()
    stubOffline()
    expect(await store.fetchAll(1)).toBe(false)
    expect(store.error).toBe(translate('errors.noAnswer'))

    stubFetch(json({ items: [], totalCount: 0 }))
    expect(await store.fetchAll(1)).toBe(true)
    expect(store.error).toBeNull()
  })

  it('fetchById clears the previous ticket and stores the new one', async () => {
    const store = useSupportStore()
    store.currentTicket = { id: 1 } as never
    stubFetch(problem('No such ticket.', 404))
    expect(await store.fetchById(2)).toBe(false)
    expect(store.currentTicket).toBeNull()
    expect(store.error).toBe('No such ticket.')

    stubFetch(json({ id: 2 }))
    expect(await store.fetchById(2)).toBe(true)
    expect(store.currentTicket).toEqual({ id: 2 })
  })

  it('fetchOverview stores the figures', async () => {
    const mock = stubFetch(json({ openTickets: 3 }))
    const store = useSupportStore()

    expect(await store.fetchOverview(SupportOverviewPeriod.Today)).toBe(true)
    expect(callOf(mock).url).toBe('/api/tickets/overview?period=today')
    expect(store.overviewStats).toEqual({ openTickets: 3 })
  })

  it('fetchDepartments fills the list and keeps the sentence in departmentsError when it fails', async () => {
    const store = useSupportStore()
    stubFetch(json([{ id: 1, name: 'Sales' }]))
    expect(await store.fetchDepartments()).toBe(true)
    expect(store.departments).toEqual([{ id: 1, name: 'Sales' }])

    stubOffline()
    expect(await store.fetchDepartments()).toBe(false)
    expect(store.error).toBeNull()
    expect(store.departmentsError).not.toBeNull()
    expect(store.departments).toHaveLength(1)
  })

  it('createTicket returns the id, and exposes the refused fields', async () => {
    const store = useSupportStore()
    const payload = { clientId: 1, subject: '', message: 'Help', departmentId: 2, priority: TicketPriority.High }

    stubFetch(json(7))
    expect(await store.createTicket(payload)).toBe(7)

    stubFetch(invalid('Check the form.', { Subject: ['Subject is required.'] }))
    expect(await store.createTicket(payload)).toBeNull()
    expect(store.actionError).toBe('Check the form.')
    expect(store.fieldErrors).toEqual({ subject: 'Subject is required.' })
  })

  it('createTicket sends a missing department as null and exposes the department field error', async () => {
    const store = useSupportStore()
    const payload = { clientId: 1, subject: 'Hi', message: 'Help', departmentId: null, priority: TicketPriority.High }

    const mock = stubFetch(invalid('Check the form.', { DepartmentId: ['Select a department.'] }))
    expect(await store.createTicket(payload)).toBeNull()
    expect(callOf(mock).body).toMatchObject({ departmentId: null })
    expect(store.fieldErrors).toEqual({ departmentId: 'Select a department.' })
  })

  it('mutations report the outcome as a boolean and keep the sentence', async () => {
    const store = useSupportStore()
    const mock = stubFetch(new Response(null, { status: 204 }))

    expect(await store.updateTicket(5, { status: TicketStatus.Closed })).toBe(true)
    expect(await store.replyToTicket(5, 'Done')).toBe(true)
    expect(await store.toggleFlag(5)).toBe(true)
    expect(await store.bulkAction([1, 2], TicketBulkAction.Close)).toBe(true)
    expect(await store.deleteTicket(5)).toBe(true)
    expect(callOf(mock, 1)).toMatchObject({ url: '/api/tickets/5/reply', body: { message: 'Done' } })
    expect(callOf(mock, 3)).toMatchObject({ url: '/api/tickets/bulk', body: { ticketIds: [1, 2], action: 'close' } })

    stubFetch(problem('Ticket is locked.', 409))
    expect(await store.deleteTicket(5)).toBe(false)
    expect(store.actionError).toBe('Ticket is locked.')
    expect(store.error).toBeNull()
  })
})
