/**
 * Tests for `stores/account.ts`, through the real client, auth and billing API composables, the client and auth stores and the transport against a stub server.
 *
 * The store, not the account page, owns every loading flag and refusal sentence, so these
 * assertions pin the contract the page renders from. Nothing of our own is replaced: the stub plays the BFF, "the API was
 * called with X" is an assertion on the request that reached it, and only the QR renderer (a third-party package) is faked.
 *
 * @module tests/stores/account.test
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { AccountEndpoint, contactPath, userPath } from '../helpers/accountPaths'
import { makeContactInput } from '../helpers/accountContact'
import { makeClientUser } from '../helpers/clientUser'
import { bodyOf } from '../helpers/requestBody'
import { readOf, refusal, requestsTo, routeTable } from '../helpers/stubRoutes'
import type { StubRouteTable } from '../helpers/stubRoutes'
import type { StubServer } from '../helpers/stubServer'
import { startTransport } from '../helpers/stubTransport'
import { CLIENT_LOGIN_PATH } from '~/constants/clientRoutes'
import { useAccountStore } from '~/stores/account'
import { useClientStore } from '~/stores/client'
import { ActionOutcome } from '~/types/actionoutcome'
import { ContactSaveOutcome } from '~/types/contactsaveoutcome'
import { EmailConfirmationStatus } from '~/types/emailconfirmationstatus'
import { HttpMethod } from '~/types/httpmethod'
import { InvitePermissionMode } from '~/types/invitepermissionmode'
import type { InviteUserPayload } from '~/types/inviteuserpayload'
import { Permission } from '~/types/permission'
import { TwoFactorMode } from '~/types/twofactormode'

vi.mock('qrcode', () => ({
  default: {
    /**
     * Stands in for the QR renderer.
     *
     * @returns A fixed data URL.
     */
    toDataURL: async (): Promise<string> => 'data:image/png;base64,QR'
  }
}))

/** The stub playing the BFF. */
let stub: StubServer

/** The replies the stub gives, by `METHOD /path`; each test fills the ones it needs. */
let table: StubRouteTable

/**
 * The key of a write in a stub route table.
 *
 * @param method - The verb of the write.
 * @param path - The path, without query.
 * @returns The `METHOD /path` key.
 */
const writeOf = (method: HttpMethod, path: string): string => `${method} ${path}`

/** The refusal the API answers with. */
const REFUSED = refusal('Refused.')

/** A complete invitation granting every permission. */
const invite: InviteUserPayload = { email: 'a@b.c', firstName: 'A', lastName: 'B', permissionMode: InvitePermissionMode.All, permissions: [] }

/** The navigation helper the sign-out ends in, a Nuxt global the test replaces with a spy. */
const navigate = vi.fn()

/**
 * The methods of the requests that reached a path, oldest first.
 *
 * @param path - The path, without query.
 * @returns The methods.
 */
const methodsTo = (path: string): string[] => requestsTo(stub.requests, path).map(request => request.method)

beforeEach(async () => {
  setActivePinia(createPinia())
  navigate.mockReset()
  vi.stubGlobal('navigateTo', navigate)
  vi.stubGlobal('useRuntimeConfig', () => ({ public: { authMode: 'local' } }))
  table = {}
  stub = await startTransport(routeTable(table))
})

afterEach(async () => {
  vi.unstubAllGlobals()
  await stub.close()
})

describe('useAccountStore.saveProfile', () => {
  it('saves, reloads the profile and reports Succeeded', async () => {
    table[writeOf(HttpMethod.Patch, AccountEndpoint.Me)] = { body: {} }
    table[readOf(AccountEndpoint.Me)] = { body: makeClientUser() }
    const store = useAccountStore()

    const outcome = await store.saveProfile({ firstName: 'Ada' })

    expect(outcome).toBe(ActionOutcome.Succeeded)
    expect(methodsTo(AccountEndpoint.Me)).toEqual([HttpMethod.Patch, HttpMethod.Get])
    expect(bodyOf(requestsTo(stub.requests, AccountEndpoint.Me)[0])).toEqual({ firstName: 'Ada' })
    expect(store.saveError).toBe('')
    expect(store.saving).toBe(false)
  })

  it('keeps the API sentence and reports Failed', async () => {
    table[writeOf(HttpMethod.Patch, AccountEndpoint.Me)] = REFUSED
    const store = useAccountStore()

    const outcome = await store.saveProfile({})

    expect(outcome).toBe(ActionOutcome.Failed)
    expect(store.saveError).toBe('Refused.')
    expect(methodsTo(AccountEndpoint.Me)).toEqual([HttpMethod.Patch])
    expect(store.saving).toBe(false)
  })
})

describe('useAccountStore field errors', () => {
  it('keeps the field messages of a refused profile save and clears them when the next save starts', async () => {
    table[writeOf(HttpMethod.Patch, AccountEndpoint.Me)] = { status: 400, body: { type: 'VALIDATION_ERROR', errors: { phone: ['Phone is invalid.'] } } }
    table[readOf(AccountEndpoint.Me)] = { body: makeClientUser() }
    const store = useAccountStore()

    expect(await store.saveProfile({})).toBe(ActionOutcome.Failed)
    expect(store.fieldErrors).toEqual({ phone: 'Phone is invalid.' })

    table[writeOf(HttpMethod.Patch, AccountEndpoint.Me)] = { body: {} }
    await store.saveProfile({})
    expect(store.fieldErrors).toEqual({})
  })
})

describe('useAccountStore team members', () => {
  it('reports Succeeded for an invitation', async () => {
    table[writeOf(HttpMethod.Post, AccountEndpoint.InviteUser)] = { body: {} }
    const store = useAccountStore()

    expect(await store.inviteUser(invite)).toBe(ActionOutcome.Succeeded)
    expect(bodyOf(requestsTo(stub.requests, AccountEndpoint.InviteUser)[0])).toEqual(invite)
    expect(store.inviting).toBe(false)
  })

  it('keeps the sentence when an invitation is refused', async () => {
    table[writeOf(HttpMethod.Post, AccountEndpoint.InviteUser)] = REFUSED
    const store = useAccountStore()

    expect(await store.inviteUser(invite)).toBe(ActionOutcome.Failed)
    expect(store.inviteError).toBe('Refused.')
  })

  it('keeps the sentence when a removal is refused and clears the in-flight id', async () => {
    table[writeOf(HttpMethod.Delete, userPath('7'))] = REFUSED
    const store = useAccountStore()

    expect(await store.removeUser('7')).toBe(ActionOutcome.Failed)
    expect(methodsTo(userPath('7'))).toEqual([HttpMethod.Delete])
    expect(store.inviteError).toBe('Refused.')
    expect(store.removingUserId).toBeNull()
  })
})

describe('useAccountStore contacts', () => {
  it('rejects a contact without names and makes no request', async () => {
    const store = useAccountStore()

    expect(await store.saveContact(null, makeContactInput({ firstName: '' }))).toBe(ContactSaveOutcome.Invalid)
    expect(stub.requests).toHaveLength(0)
  })

  it('creates when the id is null and updates when it is set', async () => {
    table[writeOf(HttpMethod.Post, AccountEndpoint.Contacts)] = { body: 5 }
    table[writeOf(HttpMethod.Put, contactPath(5))] = { body: {} }
    const store = useAccountStore()
    const body = makeContactInput({ firstName: 'A', lastName: 'L' })

    expect(await store.saveContact(null, body)).toBe(ContactSaveOutcome.Saved)
    expect(await store.saveContact(5, body)).toBe(ContactSaveOutcome.Saved)

    expect(methodsTo(AccountEndpoint.Contacts)).toEqual([HttpMethod.Post])
    expect(bodyOf(requestsTo(stub.requests, AccountEndpoint.Contacts)[0])).toEqual(body)
    expect(methodsTo(contactPath(5))).toEqual([HttpMethod.Put])
    expect(bodyOf(requestsTo(stub.requests, contactPath(5))[0])).toEqual(body)
    expect(store.contactSaving).toBe(false)
  })

  it('keeps the sentence when saving is refused', async () => {
    table[writeOf(HttpMethod.Post, AccountEndpoint.Contacts)] = REFUSED
    const store = useAccountStore()

    expect(await store.saveContact(null, makeContactInput({ firstName: 'A', lastName: 'L' }))).toBe(ContactSaveOutcome.Failed)
    expect(store.contactError).toBe('Refused.')
  })

  it('reports Failed for a refused removal', async () => {
    table[writeOf(HttpMethod.Delete, contactPath(5))] = REFUSED
    const store = useAccountStore()

    expect(await store.removeContact(5)).toBe(ActionOutcome.Failed)
    expect(methodsTo(contactPath(5))).toEqual([HttpMethod.Delete])
  })
})

describe('useAccountStore two-factor', () => {
  it('returns the setup with a locally rendered QR code', async () => {
    table[writeOf(HttpMethod.Post, AccountEndpoint.TwoFactorSetup)] = { body: { secret: 'S', qrCodeUri: 'otpauth://x' } }
    const store = useAccountStore()

    const setup = await store.startTwoFactorSetup()

    expect(setup).toEqual({ secret: 'S', qrCodeUri: 'otpauth://x', qrDataUrl: 'data:image/png;base64,QR' })
    expect(store.tfaLoading).toBe(false)
  })

  it('returns null and keeps the sentence when enrolment cannot start', async () => {
    table[writeOf(HttpMethod.Post, AccountEndpoint.TwoFactorSetup)] = REFUSED
    const store = useAccountStore()

    expect(await store.startTwoFactorSetup()).toBeNull()
    expect(store.tfaError).toBe('Refused.')
  })

  it('enables with the code and reloads the profile', async () => {
    table[writeOf(HttpMethod.Post, AccountEndpoint.TwoFactorEnable)] = { body: {} }
    table[readOf(AccountEndpoint.Me)] = { body: makeClientUser({ twoFactorEnabled: true }) }
    const store = useAccountStore()

    expect(await store.submitTwoFactor(TwoFactorMode.Enable, '123456')).toBe(ActionOutcome.Succeeded)

    expect(bodyOf(requestsTo(stub.requests, AccountEndpoint.TwoFactorEnable)[0])).toEqual({ code: '123456' })
    expect(methodsTo(AccountEndpoint.Me)).toEqual([HttpMethod.Get])
    expect(requestsTo(stub.requests, AccountEndpoint.Logout)).toHaveLength(0)
  })

  it('disables with the code and the password, then signs this browser out', async () => {
    table[writeOf(HttpMethod.Post, AccountEndpoint.TwoFactorDisable)] = { body: {} }
    table[writeOf(HttpMethod.Post, AccountEndpoint.Logout)] = { body: {} }
    const store = useAccountStore()

    expect(await store.submitTwoFactor(TwoFactorMode.Disable, '654321', 'secret-pw')).toBe(ActionOutcome.Succeeded)

    expect(bodyOf(requestsTo(stub.requests, AccountEndpoint.TwoFactorDisable)[0])).toEqual({ code: '654321', currentPassword: 'secret-pw' })
    expect(requestsTo(stub.requests, AccountEndpoint.Logout)).toHaveLength(1)
    expect(navigate).toHaveBeenCalledWith(CLIENT_LOGIN_PATH)
    expect(requestsTo(stub.requests, AccountEndpoint.Me)).toHaveLength(0)
  })

  it('stays signed in when switching off is refused', async () => {
    table[writeOf(HttpMethod.Post, AccountEndpoint.TwoFactorDisable)] = REFUSED
    const store = useAccountStore()

    expect(await store.submitTwoFactor(TwoFactorMode.Disable, '654321', 'wrong')).toBe(ActionOutcome.Failed)

    expect(store.tfaModalError).toBe('Refused.')
    expect(requestsTo(stub.requests, AccountEndpoint.Logout)).toHaveLength(0)
  })

  it('keeps the sentence when a code is refused', async () => {
    table[writeOf(HttpMethod.Post, AccountEndpoint.TwoFactorEnable)] = REFUSED
    const store = useAccountStore()

    expect(await store.submitTwoFactor(TwoFactorMode.Enable, 'x')).toBe(ActionOutcome.Failed)
    expect(store.tfaModalError).toBe('Refused.')
    expect(store.tfaSaving).toBe(false)
  })
})

describe('useAccountStore sign-in address', () => {
  it('asks for the change and remembers where the link went', async () => {
    table[writeOf(HttpMethod.Post, AccountEndpoint.EmailChange)] = { body: {} }
    const store = useAccountStore()

    const outcome = await store.startEmailChange({ newEmail: 'new@example.com', currentPassword: 'pw' })

    expect(outcome).toBe(ActionOutcome.Succeeded)
    expect(bodyOf(requestsTo(stub.requests, AccountEndpoint.EmailChange)[0])).toEqual({ newEmail: 'new@example.com', currentPassword: 'pw' })
    expect(store.emailChangeSentTo).toBe('new@example.com')
    expect(store.emailChangeSaving).toBe(false)
  })

  it('keeps the sentence and sends nobody to an inbox when the change is refused', async () => {
    table[writeOf(HttpMethod.Post, AccountEndpoint.EmailChange)] = REFUSED
    const store = useAccountStore()

    const outcome = await store.startEmailChange({ newEmail: 'new@example.com', currentPassword: 'wrong' })

    expect(outcome).toBe(ActionOutcome.Failed)
    expect(store.emailChangeError).toBe('Refused.')
    expect(store.emailChangeSentTo).toBeNull()
  })

  it('confirms the change and ends this browser session', async () => {
    table[writeOf(HttpMethod.Post, `${AccountEndpoint.EmailChange}/confirm`)] = { body: {} }
    table[writeOf(HttpMethod.Post, AccountEndpoint.Logout)] = { body: {} }
    const store = useAccountStore()

    const status = await store.confirmEmailChange('new@example.com', 'tok')

    expect(status).toBe(EmailConfirmationStatus.Confirmed)
    expect(bodyOf(requestsTo(stub.requests, `${AccountEndpoint.EmailChange}/confirm`)[0])).toEqual({ email: 'new@example.com', token: 'tok' })
    expect(requestsTo(stub.requests, AccountEndpoint.Logout)).toHaveLength(1)
  })

  it('refuses a link with nothing in it without a request', async () => {
    const store = useAccountStore()

    const status = await store.confirmEmailChange('', '')

    expect(status).toBe(EmailConfirmationStatus.Failed)
    expect(store.emailChangeConfirmError).toBe('client.confirmEmail.invalidLink')
    expect(stub.requests).toHaveLength(0)
  })

  it('keeps the sentence and the session when the token is refused', async () => {
    table[writeOf(HttpMethod.Post, `${AccountEndpoint.EmailChange}/confirm`)] = REFUSED
    const store = useAccountStore()

    const status = await store.confirmEmailChange('new@example.com', 'bad')

    expect(status).toBe(EmailConfirmationStatus.Failed)
    expect(store.emailChangeConfirmError).toBe('Refused.')
    expect(requestsTo(stub.requests, AccountEndpoint.Logout)).toHaveLength(0)
  })
})

describe('useAccountStore reads', () => {
  it.each([
    ['loadUsers', AccountEndpoint.Users, 'users', 'usersLoading'],
    ['loadContacts', AccountEndpoint.Contacts, 'contacts', 'contactsLoading']
  ] as const)('%s keeps the list, and empties it when the read fails', async (action, path, state, flag) => {
    table[readOf(path)] = { body: [{ id: '1' }] }
    const store = useAccountStore()

    await store[action]()
    expect(store[state]).toEqual([{ id: '1' }])
    expect(store[flag]).toBe(false)

    table[readOf(path)] = refusal('down')
    await store[action]()
    expect(store[state]).toEqual([])
    expect(store[flag]).toBe(false)
  })

  it('keeps the rows of the sent-mail page, and empties them when the read fails', async () => {
    const row = { id: 1, to: 'a@b.test', subject: 'Invoice', sentAt: '2026-02-03T00:00:00Z', success: true }
    table[readOf(AccountEndpoint.Emails)] = { body: { items: [row], pageNumber: 1, pageSize: 100, totalCount: 1, totalPages: 1 } }
    const store = useAccountStore()

    await store.loadEmails()
    expect(store.emails).toEqual([row])
    expect(store.emailsLoading).toBe(false)

    table[readOf(AccountEndpoint.Emails)] = refusal('down')
    await store.loadEmails()
    expect(store.emails).toEqual([])
    expect(store.emailsLoading).toBe(false)
  })

  it('keeps the gateways the profile can default to, and none when the read fails', async () => {
    table[readOf(AccountEndpoint.PaymentMethods)] = { body: [{ module: 'card_gateway', displayName: 'Card', supportsStoredCredentials: false }] }
    const store = useAccountStore()

    await store.loadGatewayMethods()
    expect(store.gatewayMethods).toHaveLength(1)

    table[readOf(AccountEndpoint.PaymentMethods)] = refusal('down')
    await store.loadGatewayMethods()
    expect(store.gatewayMethods).toEqual([])
  })
})

describe('useAccountStore modal errors', () => {
  it('clears every modal error on request', async () => {
    table[writeOf(HttpMethod.Patch, AccountEndpoint.Me)] = REFUSED
    const store = useAccountStore()
    await store.saveProfile({})

    store.clearModalErrors()

    expect(store.saveError).toBe('')
  })
})

describe('useAccountStore reads without the permission', () => {
  it.each([
    ['loadUsers', 'users'],
    ['loadContacts', 'contacts'],
    ['loadEmails', 'emails'],
  ] as const)('%s sends no request and leaves the list empty', async (action, list) => {
    useClientStore().user = makeClientUser({ permissions: Permission.None })
    const store = useAccountStore()

    await store[action]()

    expect(stub.requests).toHaveLength(0)
    expect(store[list]).toEqual([])
  })
})
