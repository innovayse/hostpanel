/**
 * Tests for `stores/support.ts`, through the real API composable and transport against a stub
 * server: the submission actions and the reads.
 *
 * The store, not the pages, owns the refusal sentence and the in-flight flag, so these
 * assertions pin the contract the pages render from.
 *
 * @module tests/stores/support.test
 */

import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { startTransport } from '../helpers/stubTransport'
import type { StubServer } from '../helpers/stubServer'
import { useSupportStore } from '~/stores/support'
import { SubmitOutcome } from '~/types/submitoutcome'
import { TicketPriority } from '~/types/ticketpriority'

/** The stub playing the BFF. */
let stub: StubServer

/** A valid new-ticket body. */
const ticket = { departmentId: 1, subject: 's', message: 'm', priority: TicketPriority.Medium }

/** A valid contact-form body. */
const contact = { name: 'Ada', email: 'ada@example.com', message: 'hi' }

/** The answer of a call nobody answered: no body at all, so the store speaks with its own sentence. */
const NO_ANSWER = { status: 502 }

beforeEach(async () => {
  setActivePinia(createPinia())
  stub = await startTransport(() => ({ body: {} }))
})

afterEach(async () => {
  await stub.close()
})

describe('useSupportStore', () => {
  it('createTicket reports Sent and leaves no error', async () => {
    const store = useSupportStore()

    expect(await store.createTicket(ticket)).toBe(SubmitOutcome.Sent)
    expect(stub.requests[0]).toMatchObject({ method: 'POST', url: '/api/portal/client/tickets' })
    expect(JSON.parse(stub.requests[0]?.body ?? '')).toEqual(ticket)
    expect(store.ticketError).toBe('')
    expect(store.ticketLoading).toBe(false)
  })

  it('createTicket reports Failed with the API sentence', async () => {
    stub.respondWith(() => ({ status: 400, body: { detail: 'Department closed.' } }))
    const store = useSupportStore()

    expect(await store.createTicket(ticket)).toBe(SubmitOutcome.Failed)
    expect(store.ticketError).toBe('Department closed.')
    expect(store.ticketLoading).toBe(false)
  })

  it('replyToTicket reports Sent and Failed', async () => {
    const store = useSupportStore()

    expect(await store.replyToTicket('7', 'hello')).toBe(SubmitOutcome.Sent)
    expect(stub.requests[0]).toMatchObject({ method: 'POST', url: '/api/portal/client/tickets/7/reply' })
    expect(JSON.parse(stub.requests[0]?.body ?? '')).toEqual({ message: 'hello' })
    expect(store.replyError).toBe('')

    stub.respondWith(() => ({ status: 400, body: { detail: 'Ticket closed.' } }))
    expect(await store.replyToTicket('7', 'hello')).toBe(SubmitOutcome.Failed)
    expect(store.replyError).toBe('Ticket closed.')
    expect(store.replyLoading).toBe(false)
  })

  it('sendContactMessage reports Sent and Failed', async () => {
    const store = useSupportStore()

    expect(await store.sendContactMessage(contact)).toBe(SubmitOutcome.Sent)
    expect(stub.requests[0]).toMatchObject({ method: 'POST', url: '/api/contact' })
    expect(JSON.parse(stub.requests[0]?.body ?? '')).toEqual(contact)
    expect(store.contactError).toBe('')

    // A refusal with no body is "no answer": the one client sentence, never `err.message`
    // (which would embed the upstream address).
    stub.respondWith(() => NO_ANSWER)
    expect(await store.sendContactMessage(contact)).toBe(SubmitOutcome.Failed)
    expect(store.contactError).toBe('errors.noAnswer')
    expect(store.contactLoading).toBe(false)
  })

  it('keeps the field messages of a refused ticket and clears them on the next attempt', async () => {
    const store = useSupportStore()
    stub.respondWith(() => ({
      status: 400,
      body: { statusMessage: 'One or more validation errors occurred.', data: { code: 'VALIDATION_ERROR', errors: { subject: ['Subject is required.'] } } }
    }))

    expect(await store.createTicket(ticket)).toBe(SubmitOutcome.Failed)
    expect(store.fieldErrors).toEqual({ subject: 'Subject is required.' })
    expect(store.ticketError).toBe('Subject is required.')

    stub.respondWith(() => ({ body: {} }))
    expect(await store.createTicket(ticket)).toBe(SubmitOutcome.Sent)
    expect(store.fieldErrors).toEqual({})
  })

  it('keeps the field messages of a refused contact form', async () => {
    stub.respondWith(() => ({ status: 400, body: { type: 'VALIDATION_ERROR', errors: { email: ['Email is invalid.'] } } }))
    const store = useSupportStore()

    expect(await store.sendContactMessage(contact)).toBe(SubmitOutcome.Failed)
    expect(store.fieldErrors).toEqual({ email: 'Email is invalid.' })
  })

  it('clears the previous error when a new attempt starts', async () => {
    const store = useSupportStore()
    stub.respondWith(() => NO_ANSWER)
    await store.createTicket(ticket)
    stub.respondWith(() => ({ body: {} }))
    await store.createTicket(ticket)

    expect(store.ticketError).toBe('')
  })
})

describe('useSupportStore reads', () => {
  it('keeps the ticket with its thread', async () => {
    stub.respondWith(() => ({ body: { subject: 'Help', status: 'Open' } }))
    const store = useSupportStore()

    expect(await store.loadTicket('8')).toMatchObject({ subject: 'Help' })
    expect(stub.requests[0]).toMatchObject({ method: 'GET', url: '/api/portal/client/tickets/8' })
    expect(store.ticket).toMatchObject({ status: 'Open' })
    expect(store.ticketReading).toBe(false)
    expect(store.ticketReadError).toBe('')
  })

  it('keeps the API sentence and no ticket when the read is refused', async () => {
    stub.respondWith(() => ({ status: 404, body: { detail: 'Not your ticket.' } }))
    const store = useSupportStore()

    expect(await store.loadTicket('8')).toBeNull()
    expect(store.ticket).toBeNull()
    expect(store.ticketReadError).toBe('Not your ticket.')
  })

  it('keeps showing the ticket on screen while the same ticket is read again', async () => {
    stub.respondWith(() => ({ body: { subject: 'Help' } }))
    const store = useSupportStore()
    await store.loadTicket('8')

    let seenDuringRead: unknown = 'unset'
    stub.respondWith(() => {
      seenDuringRead = store.ticket
      return { body: { subject: 'Help', replies: [] } }
    })
    await store.loadTicket('8')

    expect(seenDuringRead).toMatchObject({ subject: 'Help' })
  })

  it('does not show the previous ticket while a different one is read', async () => {
    stub.respondWith(() => ({ body: { subject: 'First' } }))
    const store = useSupportStore()
    await store.loadTicket('8')

    let seenDuringRead: unknown = 'unset'
    stub.respondWith(() => {
      seenDuringRead = store.ticket
      return { body: { subject: 'Second' } }
    })
    await store.loadTicket('9')

    expect(seenDuringRead).toBeNull()
    expect(store.ticket).toMatchObject({ subject: 'Second' })
  })

  it('keeps the departments, and an empty list when they cannot be read', async () => {
    stub.respondWith(() => ({ body: [{ id: 1, name: 'Billing' }] }))
    const store = useSupportStore()

    await store.loadDepartments()
    expect(stub.requests[0]).toMatchObject({ method: 'GET', url: '/api/portal/public/departments' })
    expect(store.departments).toEqual([{ id: 1, name: 'Billing' }])

    stub.respondWith(() => ({ status: 404, body: { detail: 'Gone.' } }))
    await store.loadDepartments()
    expect(store.departments).toEqual([])
    expect(store.departmentsLoading).toBe(false)
  })
})
