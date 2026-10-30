/**
 * Regression tests for the identity request in `stores/client.ts`.
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

import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { Permission } from '~/composables/usePermissions'
import { makeClientUser } from '~/tests/helpers/clientUser'

/** The mocked `/api/portal/client/me` call, re-created for every test. */
const fetchMe = vi.fn()

/** The mocked ticket list call, re-created for every test. */
const fetchTickets = vi.fn()

/** The mocked service list call, re-created for every test. */
const fetchServices = vi.fn()

/** The mocked invoice list call, re-created for every test. */
const fetchInvoices = vi.fn()

/** The mocked domain list call, re-created for every test. */
const fetchDomains = vi.fn()

/** The mocked `/api/portal/client/emails/{id}` call, re-created for every test. */
const fetchEmail = vi.fn()

vi.mock('~/composables/apis/useClientApi', () => ({
  /**
   * Stands in for the real API composable so the store can be exercised without transport.
   *
   * @returns Only the endpoint functions these tests reach for.
   */
  useClientApi: () => ({ fetchMe, fetchEmail, fetchTickets, fetchServices, fetchInvoices, fetchDomains }),
}))

const { useClientStore } = await import('~/stores/client')

/**
 * Builds a promise the test resolves or rejects by hand, so several callers can be observed
 * while the request is still in flight.
 *
 * @returns The pending promise and the two functions that settle it.
 */
const deferred = <T>() => {
  let resolve!: (value: T) => void
  let reject!: (reason: unknown) => void
  const promise = new Promise<T>((res, rej) => { resolve = res; reject = rej })
  return { promise, resolve, reject }
}

/** A minimal profile: only the fields the assertions read. */
const profile = makeClientUser({ email: 'ada@example.com', permissions: Permission.All })

describe('useClientStore.fetchUser', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    fetchMe.mockReset()
  })

  it('asks for the identity once when all three page-load callers ask at the same time', async () => {
    const gate = deferred<typeof profile>()
    fetchMe.mockReturnValue(gate.promise)

    const store = useClientStore()

    // The plugin, the layout and the page, all before the first answer comes back.
    const callers = Promise.all([store.fetchUser(), store.fetchUser(), store.fetchUser()])
    expect(fetchMe).toHaveBeenCalledTimes(1)

    gate.resolve(profile)
    await callers

    expect(fetchMe).toHaveBeenCalledTimes(1)
    expect(store.user).toEqual(profile)
    expect(store.userLoaded).toBe(true)
    expect(store.userLoading).toBe(false)
  })

  it('every concurrent caller waits for the answer rather than returning early and empty', async () => {
    const gate = deferred<typeof profile>()
    fetchMe.mockReturnValue(gate.promise)

    const store = useClientStore()
    const seen: Array<typeof profile | null> = []
    const callers = Promise.all([
      store.fetchUser().then(() => { seen.push(store.user) }),
      store.fetchUser().then(() => { seen.push(store.user) }),
    ])

    gate.resolve(profile)
    await callers

    expect(seen).toEqual([profile, profile])
  })

  it('does not ask again once the identity is loaded', async () => {
    fetchMe.mockResolvedValue(profile)
    const store = useClientStore()

    await store.fetchUser()
    await store.fetchUser()

    expect(fetchMe).toHaveBeenCalledTimes(1)
  })

  it('asks again when a caller forces a refresh after saving the profile', async () => {
    fetchMe.mockResolvedValue(profile)
    const store = useClientStore()

    await store.fetchUser()
    await store.fetchUser(true)

    expect(fetchMe).toHaveBeenCalledTimes(2)
  })

  it('asks once, not three times, for a staff identity with no client record', async () => {
    const gate = deferred<typeof profile>()
    fetchMe.mockReturnValue(gate.promise)

    const store = useClientStore()
    const callers = Promise.all([store.fetchUser(), store.fetchUser(), store.fetchUser()])

    gate.reject({ data: { type: 'CLIENT_PROFILE_NOT_FOUND' } })
    await callers

    expect(fetchMe).toHaveBeenCalledTimes(1)
    expect(store.clientProfileMissing).toBe(true)
    // Not a fault, so nothing renders it in red.
    expect(store.userError).toBeNull()
    // And not remembered as loaded either: a later navigation may ask again, so the answer
    // is never cached as a permanent "no".
    expect(store.userLoaded).toBe(false)
  })

  it('does not let a signed-out session’s answer land on the next visitor', async () => {
    const gate = deferred<typeof profile>()
    fetchMe.mockReturnValue(gate.promise)

    const store = useClientStore()
    const pending = store.fetchUser()

    // Signing out while the identity request is still on the wire.
    store.reset()

    gate.resolve(profile)
    await pending

    expect(store.user).toBeNull()
    expect(store.userLoaded).toBe(false)
    expect(store.userLoading).toBe(false)
  })

  it('lets the next sign-in fetch a fresh identity rather than joining the old request', async () => {
    const first = deferred<typeof profile>()
    fetchMe.mockReturnValueOnce(first.promise)

    const store = useClientStore()
    const stale = store.fetchUser()
    store.reset()

    const next = { ...profile, id: 2, firstName: 'Grace', email: 'grace@example.com' }
    fetchMe.mockResolvedValueOnce(next)
    await store.fetchUser()

    first.resolve(profile)
    await stale

    expect(fetchMe).toHaveBeenCalledTimes(2)
    expect(store.user).toEqual(next)
  })
})

describe('useClientStore customer-only sections for an identity that is not a customer', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    fetchMe.mockReset()
    fetchTickets.mockReset()
    fetchServices.mockReset()
    fetchInvoices.mockReset()
    fetchDomains.mockReset()
  })

  it('sets the missing-profile state from the identity and never sends the refused requests', async () => {
    fetchMe.mockResolvedValue({ ...profile, hasClientProfile: false })
    const store = useClientStore()

    await Promise.all([store.fetchTickets(), store.fetchServices(), store.fetchInvoices(), store.fetchDomains()])

    expect(store.clientProfileMissing).toBe(true)
    expect(store.clientProfileMessage).toBeNull()
    expect(fetchTickets).not.toHaveBeenCalled()
    expect(fetchServices).not.toHaveBeenCalled()
    expect(fetchInvoices).not.toHaveBeenCalled()
    expect(fetchDomains).not.toHaveBeenCalled()
    // One shared identity request served all four sections, and none is left spinning or red.
    expect(fetchMe).toHaveBeenCalledTimes(1)
    expect(store.ticketsLoading).toBe(false)
    expect(store.ticketsError).toBeNull()
  })

  it('loads the tickets of a customer account after the identity confirms it', async () => {
    fetchMe.mockResolvedValue({ ...profile, hasClientProfile: true })
    fetchTickets.mockResolvedValue([{ id: 7 }])
    const store = useClientStore()

    await store.fetchTickets()

    expect(store.clientProfileMissing).toBe(false)
    expect(fetchTickets).toHaveBeenCalledTimes(1)
    expect(store.tickets).toEqual([{ id: 7 }])
  })

  it('still tries the section when the identity itself could not be read', async () => {
    fetchMe.mockRejectedValue({ statusCode: 500 })
    fetchTickets.mockResolvedValue([])
    const store = useClientStore()

    await store.fetchTickets()

    expect(fetchTickets).toHaveBeenCalledTimes(1)
    expect(store.ticketsLoaded).toBe(true)
  })
})

describe('useClientStore.loadEmail', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    fetchEmail.mockReset()
  })

  it('keeps the message', async () => {
    fetchEmail.mockResolvedValue({ id: 5, to: 'a@b.test', subject: 'Hello', body: '<p>x</p>', sentAt: '2026-01-01T00:00:00Z', success: true })
    const store = useClientStore()

    expect(await store.loadEmail('5')).toMatchObject({ subject: 'Hello' })
    expect(store.email).toMatchObject({ id: 5 })
    expect(store.emailLoading).toBe(false)
    expect(store.emailError).toBeNull()
  })

  it('keeps the API sentence and no message when the read is refused', async () => {
    fetchEmail.mockRejectedValue({ data: { detail: 'No such message.' } })
    const store = useClientStore()

    expect(await store.loadEmail('5')).toBeNull()
    expect(store.email).toBeNull()
    expect(store.emailError).toBe('No such message.')
  })

  it('stays quiet when the session is over, because the reader is already on the way out', async () => {
    fetchEmail.mockRejectedValue({ statusCode: 401 })
    const store = useClientStore()

    await store.loadEmail('5')

    expect(store.emailError).toBeNull()
  })

  it('forgets the message on reset', async () => {
    fetchEmail.mockResolvedValue({ id: '5' })
    const store = useClientStore()
    await store.loadEmail('5')

    store.reset()

    expect(store.email).toBeNull()
  })
})

describe('useClientStore sections the person has no permission for', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    fetchMe.mockReset()
    fetchTickets.mockReset()
    fetchServices.mockReset()
    fetchInvoices.mockReset()
    fetchDomains.mockReset()
  })

  it('never sends the request of a section the permissions do not cover', async () => {
    fetchMe.mockResolvedValue({ ...profile, permissions: Permission.ViewDomains })
    fetchDomains.mockResolvedValue([{ id: 3 }])
    const store = useClientStore()

    await store.fetchAll()

    expect(fetchDomains).toHaveBeenCalledTimes(1)
    expect(fetchServices).not.toHaveBeenCalled()
    expect(fetchInvoices).not.toHaveBeenCalled()
    expect(fetchTickets).not.toHaveBeenCalled()
    expect(store.servicesError).toBeNull()
    expect(store.invoicesError).toBeNull()
    expect(store.ticketsError).toBeNull()
  })

  it('answers mayDo from the loaded permissions, and yes while nobody is loaded', async () => {
    fetchMe.mockResolvedValue({ ...profile, permissions: Permission.ViewEmails })
    const store = useClientStore()

    expect(store.mayDo(Permission.ViewPayInvoices)).toBe(true)

    await store.fetchUser()

    expect(store.mayDo(Permission.ViewEmails)).toBe(true)
    expect(store.mayDo(Permission.ViewPayInvoices)).toBe(false)
    expect(store.mayDo(Permission.ViewEmails | Permission.ViewPayInvoices)).toBe(false)
  })

  it('answers mayDo yes for a signed-in person who acts for no account yet, who holds no flag', async () => {
    fetchMe.mockResolvedValue({ ...profile, permissions: Permission.None, actsForAccount: false })
    const store = useClientStore()

    await store.fetchUser()

    expect(store.mayDo(Permission.PlaceNewOrders)).toBe(true)
  })

  it('keeps the API sentence when the server refuses a section anyway, for instance after a grant was withdrawn', async () => {
    fetchMe.mockResolvedValue(profile)
    // The shape the browser gets through the BFF: the sentence in statusMessage, the code under data.
    fetchServices.mockRejectedValue({
      statusCode: 403,
      data: { statusMessage: 'Your account does not have permission to do this.', data: { code: 'CLIENT_PERMISSION_DENIED' } }
    })
    const store = useClientStore()

    await store.fetchServices()

    expect(store.servicesError).toBe('Your account does not have permission to do this.')
    expect(store.clientProfileMissing).toBe(false)
  })
})
