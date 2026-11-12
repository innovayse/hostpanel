/**
 * Regression tests for the identity request in `stores/client.ts`, through the real client API composable
 * and transport against a stub server.
 *
 * The store is the layer that decides when `/api/portal/client/me` is asked for, so it is the
 * layer these assertions belong to. A client-area page load has three callers (the
 * `client-auth` plugin (through `stores/auth.ts`), the `client` layout's `onMounted`, and the
 * page's own fetch) and they all start before the first answer returns. Production served
 * three identical `/me` requests per page load for exactly that reason. Without a test naming
 * the invariant, the fourth caller someone adds brings the duplication straight back.
 *
 * @module tests/stores/client.test
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { heldReply, requestsTo, routeTable } from '../helpers/stubRoutes'
import type { HeldReply, StubRouteTable } from '../helpers/stubRoutes'
import type { StubServer } from '../helpers/stubServer'
import { startTransport } from '../helpers/stubTransport'
import { PortalErrorCode } from '~/constants/portalErrorCode'
import { useClientStore } from '~/stores/client'
import { HttpMethod } from '~/types/httpmethod'
import { Permission } from '~/types/permission'
import { makeClientUser } from '~/tests/helpers/clientUser'

/** The stub playing the BFF. */
let stub: StubServer

/** The replies the stub gives, by `METHOD /path`; each test fills the ones it needs. */
let table: StubRouteTable

/** The identity read. */
const ME_PATH = '/api/portal/client/me'

/** The identity read, as a table key. */
const ME_ROUTE = `${HttpMethod.Get} ${ME_PATH}`

/** The ticket list read. */
const TICKETS_PATH = '/api/portal/client/tickets'

/** The service list read. */
const SERVICES_PATH = '/api/portal/client/services'

/** The invoice list read. */
const INVOICES_PATH = '/api/portal/client/invoices'

/** The domain list read. */
const DOMAINS_PATH = '/api/portal/client/domains'

/** The paths of the customer-only lists. */
const SECTION_PATHS = [TICKETS_PATH, SERVICES_PATH, INVOICES_PATH, DOMAINS_PATH]

/** A minimal profile: only the fields the assertions read. */
const profile = makeClientUser({ email: 'ada@example.com', permissions: Permission.All })

/**
 * Waits until the identity request is on the wire and held, so a test can settle callers that
 * are demonstrably still in flight.
 *
 * @param held - The held identity reply.
 * @returns Resolves once one request waits on it.
 */
const identityOnTheWire = (held: HeldReply): Promise<void> =>
  vi.waitFor(() => {
    expect(held.waiting()).toBe(1)
  })

beforeEach(async () => {
  setActivePinia(createPinia())
  table = {}
  stub = await startTransport(routeTable(table))
})

afterEach(async () => {
  await stub.close()
})

describe('useClientStore.fetchUser', () => {
  it('asks for the identity once when all three page-load callers ask at the same time', async () => {
    const held = heldReply()
    table[ME_ROUTE] = held.reply

    const store = useClientStore()

    // The plugin, the layout and the page, all before the first answer comes back.
    const callers = Promise.all([store.fetchUser(), store.fetchUser(), store.fetchUser()])
    await identityOnTheWire(held)

    held.release({ body: profile })
    await callers

    expect(requestsTo(stub.requests, ME_PATH)).toHaveLength(1)
    expect(store.user).toEqual(profile)
    expect(store.userLoaded).toBe(true)
    expect(store.userLoading).toBe(false)
  })

  it('every concurrent caller waits for the answer rather than returning early and empty', async () => {
    const held = heldReply()
    table[ME_ROUTE] = held.reply

    const store = useClientStore()
    const seen: Array<typeof profile | null> = []
    const callers = Promise.all([
      store.fetchUser().then(() => { seen.push(store.user) }),
      store.fetchUser().then(() => { seen.push(store.user) }),
    ])
    await identityOnTheWire(held)

    held.release({ body: profile })
    await callers

    expect(seen).toEqual([profile, profile])
  })

  it('does not ask again once the identity is loaded', async () => {
    table[ME_ROUTE] = { body: profile }
    const store = useClientStore()

    await store.fetchUser()
    await store.fetchUser()

    expect(requestsTo(stub.requests, ME_PATH)).toHaveLength(1)
  })

  it('asks again when a caller forces a refresh after saving the profile', async () => {
    table[ME_ROUTE] = { body: profile }
    const store = useClientStore()

    await store.fetchUser()
    await store.fetchUser(true)

    expect(requestsTo(stub.requests, ME_PATH)).toHaveLength(2)
  })

  it('asks once, not three times, for a staff identity with no client record', async () => {
    const held = heldReply()
    table[ME_ROUTE] = held.reply

    const store = useClientStore()
    const callers = Promise.all([store.fetchUser(), store.fetchUser(), store.fetchUser()])
    await identityOnTheWire(held)

    held.release({ status: 404, body: { type: PortalErrorCode.ClientProfileNotFound } })
    await callers

    expect(requestsTo(stub.requests, ME_PATH)).toHaveLength(1)
    expect(store.clientProfileMissing).toBe(true)
    // Not a fault, so nothing renders it in red.
    expect(store.userError).toBeNull()
    // And not remembered as loaded either: a later navigation may ask again, so the answer
    // is never cached as a permanent "no".
    expect(store.userLoaded).toBe(false)
  })

  it('does not let a signed-out session’s answer land on the next visitor', async () => {
    const held = heldReply()
    table[ME_ROUTE] = held.reply

    const store = useClientStore()
    const pending = store.fetchUser()
    await identityOnTheWire(held)

    // Signing out while the identity request is still on the wire.
    store.reset()

    held.release({ body: profile })
    await pending

    expect(store.user).toBeNull()
    expect(store.userLoaded).toBe(false)
    expect(store.userLoading).toBe(false)
  })

  it('lets the next sign-in fetch a fresh identity rather than joining the old request', async () => {
    const first = heldReply()
    const next = { ...profile, id: 2, firstName: 'Grace', email: 'grace@example.com' }
    let reads = 0
    /**
     * Holds the first identity read (the previous session's) and answers the second at once.
     *
     * @returns The reply.
     */
    table[ME_ROUTE] = () => {
      reads += 1
      return reads === 1 ? first.reply() : { body: next }
    }

    const store = useClientStore()
    const stale = store.fetchUser()
    await identityOnTheWire(first)
    store.reset()

    await store.fetchUser()

    first.release({ body: profile })
    await stale

    expect(requestsTo(stub.requests, ME_PATH)).toHaveLength(2)
    expect(store.user).toEqual(next)
  })
})

describe('useClientStore customer-only sections for an identity that is not a customer', () => {
  it('sets the missing-profile state from the identity and never sends the refused requests', async () => {
    table[ME_ROUTE] = { body: { ...profile, hasClientProfile: false } }
    const store = useClientStore()

    await Promise.all([store.fetchTickets(), store.fetchServices(), store.fetchInvoices(), store.fetchDomains()])

    expect(store.clientProfileMissing).toBe(true)
    expect(store.clientProfileMessage).toBeNull()
    for (const path of SECTION_PATHS) {
      expect(requestsTo(stub.requests, path)).toHaveLength(0)
    }
    // One shared identity request served all four sections, and none is left spinning or red.
    expect(requestsTo(stub.requests, ME_PATH)).toHaveLength(1)
    expect(store.ticketsLoading).toBe(false)
    expect(store.ticketsError).toBeNull()
  })

  it('loads the tickets of a customer account after the identity confirms it', async () => {
    table[ME_ROUTE] = { body: { ...profile, hasClientProfile: true } }
    table[`${HttpMethod.Get} ${TICKETS_PATH}`] = { body: [{ id: 7 }] }
    const store = useClientStore()

    await store.fetchTickets()

    expect(store.clientProfileMissing).toBe(false)
    expect(requestsTo(stub.requests, TICKETS_PATH)).toHaveLength(1)
    expect(store.tickets).toEqual([{ id: 7 }])
  })

  it('still tries the section when the identity itself could not be read', async () => {
    table[ME_ROUTE] = { status: 500 }
    table[`${HttpMethod.Get} ${TICKETS_PATH}`] = { body: [] }
    const store = useClientStore()

    await store.fetchTickets()

    expect(requestsTo(stub.requests, TICKETS_PATH)).toHaveLength(1)
    expect(store.ticketsLoaded).toBe(true)
  })
})

describe('useClientStore.loadEmail', () => {
  it('keeps the message', async () => {
    table[`${HttpMethod.Get} /api/portal/client/emails/5`] = { body: { id: 5, to: 'a@b.test', subject: 'Hello', body: '<p>x</p>', sentAt: '2026-01-01T00:00:00Z', success: true } }
    const store = useClientStore()

    expect(await store.loadEmail('5')).toMatchObject({ subject: 'Hello' })
    expect(store.email).toMatchObject({ id: 5 })
    expect(store.emailLoading).toBe(false)
    expect(store.emailError).toBeNull()
  })

  it('keeps the API sentence and no message when the read is refused', async () => {
    table[`${HttpMethod.Get} /api/portal/client/emails/5`] = { status: 404, body: { detail: 'No such message.' } }
    const store = useClientStore()

    expect(await store.loadEmail('5')).toBeNull()
    expect(store.email).toBeNull()
    expect(store.emailError).toBe('No such message.')
  })

  it('stays quiet when the session is over, because the reader is already on the way out', async () => {
    table[`${HttpMethod.Get} /api/portal/client/emails/5`] = { status: 401 }
    const store = useClientStore()

    await store.loadEmail('5')

    expect(store.emailError).toBeNull()
  })

  it('forgets the message on reset', async () => {
    table[`${HttpMethod.Get} /api/portal/client/emails/5`] = { body: { id: '5' } }
    const store = useClientStore()
    await store.loadEmail('5')

    store.reset()

    expect(store.email).toBeNull()
  })
})

describe('useClientStore sections the person has no permission for', () => {
  it('never sends the request of a section the permissions do not cover', async () => {
    table[ME_ROUTE] = { body: { ...profile, permissions: Permission.ViewDomains } }
    table[`${HttpMethod.Get} ${DOMAINS_PATH}`] = { body: [{ id: 3 }] }
    const store = useClientStore()

    await store.fetchAll()

    expect(requestsTo(stub.requests, DOMAINS_PATH)).toHaveLength(1)
    expect(requestsTo(stub.requests, SERVICES_PATH)).toHaveLength(0)
    expect(requestsTo(stub.requests, INVOICES_PATH)).toHaveLength(0)
    expect(requestsTo(stub.requests, TICKETS_PATH)).toHaveLength(0)
    expect(store.servicesError).toBeNull()
    expect(store.invoicesError).toBeNull()
    expect(store.ticketsError).toBeNull()
  })

  it('answers mayDo from the loaded permissions, and yes while nobody is loaded', async () => {
    table[ME_ROUTE] = { body: { ...profile, permissions: Permission.ViewEmails } }
    const store = useClientStore()

    expect(store.mayDo(Permission.ViewPayInvoices)).toBe(true)

    await store.fetchUser()

    expect(store.mayDo(Permission.ViewEmails)).toBe(true)
    expect(store.mayDo(Permission.ViewPayInvoices)).toBe(false)
    expect(store.mayDo(Permission.ViewEmails | Permission.ViewPayInvoices)).toBe(false)
  })

  it('answers mayDo yes for a signed-in person who acts for no account yet, who holds no flag', async () => {
    table[ME_ROUTE] = { body: { ...profile, permissions: Permission.None, actsForAccount: false } }
    const store = useClientStore()

    await store.fetchUser()

    expect(store.mayDo(Permission.PlaceNewOrders)).toBe(true)
  })

  it('keeps the API sentence when the server refuses a section anyway, for instance after a grant was withdrawn', async () => {
    table[ME_ROUTE] = { body: profile }
    // The shape the browser gets through the BFF: the sentence in statusMessage, the code under data.
    table[`${HttpMethod.Get} ${SERVICES_PATH}`] = {
      status: 403,
      body: { statusMessage: 'Your account does not have permission to do this.', data: { code: 'CLIENT_PERMISSION_DENIED' } }
    }
    const store = useClientStore()

    await store.fetchServices()

    expect(store.servicesError).toBe('Your account does not have permission to do this.')
    expect(store.clientProfileMissing).toBe(false)
  })
})
