/**
 * Tests for `stores/account.ts`, through the real client, billing and auth API composables, the real client and
 * auth stores and the transport against a stub server.
 *
 * The store, not the account page, owns every loading flag and refusal sentence, so these
 * assertions pin the contract the page renders from. Nothing of our own is replaced: the stub plays the
 * BFF, ending the session is the real sign-out request, and only the QR renderer (a third-party package) is faked.
 *
 * @module tests/stores/account.test
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { requestsTo, routeTable } from '../helpers/stubRoutes'
import type { StubRouteTable } from '../helpers/stubRoutes'
import type { StubReply, StubServer } from '../helpers/stubServer'
import { startTransport } from '../helpers/stubTransport'
import { makeContactInput } from '~/tests/helpers/accountContact'
import { makeClientUser } from '~/tests/helpers/clientUser'
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

/** Spy standing in for the auto-imported navigation helper, which the sign-out ends in. */
const navigate = vi.fn()

/** The identity read. */
const ME_PATH = '/api/portal/client/me'

/** The sign-out request that ends this browser's session. */
const LOGOUT_PATH = '/api/portal/auth/logout'

/** A refusal shaped like the API's problem body. */
const refused: StubReply = { status: 400, body: { detail: 'Refused.' } }

/** A complete invitation granting every permission. */
const invite: InviteUserPayload = { email: 'a@b.c', firstName: 'A', lastName: 'B', permissionMode: InvitePermissionMode.All, permissions: [] }

/**
 * The JSON body of the first request the stub received for a path.
 *
 * @param path - The path, without query.
 * @returns The parsed body.
 */
const bodyTo = (path: string): unknown => JSON.parse(requestsTo(stub.requests, path)[0]?.body ?? '')

beforeEach(async () => {
  setActivePinia(createPinia())
  navigate.mockReset()
  vi.stubGlobal('navigateTo', navigate)
  table = {
    [`${HttpMethod.Get} ${ME_PATH}`]: { body: makeClientUser() },
    [`${HttpMethod.Post} ${LOGOUT_PATH}`]: { body: {} }
  }
  stub = await startTransport(routeTable(table))
})

afterEach(async () => {
  vi.unstubAllGlobals()
  await stub.close()
})

describe('useAccountStore.saveProfile', () => {
  it('saves, reloads the profile and reports Succeeded', async () => {
    table[`${HttpMethod.Patch} ${ME_PATH}`] = { body: {} }
    const store = useAccountStore()

    const outcome = await store.saveProfile({ firstName: 'Ada' })

    expect(outcome).toBe(ActionOutcome.Succeeded)
    expect(stub.requests.map(request => `${request.method} ${request.url}`)).toEqual([`PATCH ${ME_PATH}`, `GET ${ME_PATH}`])
    expect(bodyTo(ME_PATH)).toEqual({ firstName: 'Ada' })
    expect(useClientStore().userLoaded).toBe(true)
    expect(store.saveError).toBe('')
    expect(store.saving).toBe(false)
  })

  it('keeps the API sentence and reports Failed', async () => {
    table[`${HttpMethod.Patch} ${ME_PATH}`] = refused
    const store = useAccountStore()

    const outcome = await store.saveProfile({})

    expect(outcome).toBe(ActionOutcome.Failed)
    expect(store.saveError).toBe('Refused.')
    expect(stub.requests.filter(request => request.method === HttpMethod.Get)).toHaveLength(0)
    expect(store.saving).toBe(false)
  })
})

describe('useAccountStore field errors', () => {
  it('keeps the field messages of a refused profile save and clears them when the next save starts', async () => {
    table[`${HttpMethod.Patch} ${ME_PATH}`] = { status: 400, body: { type: 'VALIDATION_ERROR', errors: { phone: ['Phone is invalid.'] } } }
    const store = useAccountStore()

    expect(await store.saveProfile({})).toBe(ActionOutcome.Failed)
    expect(store.fieldErrors).toEqual({ phone: 'Phone is invalid.' })

    table[`${HttpMethod.Patch} ${ME_PATH}`] = { body: {} }
    await store.saveProfile({})
    expect(store.fieldErrors).toEqual({})
  })
})

describe('useAccountStore team members', () => {
  it('reports Succeeded for an invitation', async () => {
    table[`${HttpMethod.Post} /api/portal/client/users/invite`] = { body: {} }
    const store = useAccountStore()

    expect(await store.inviteUser(invite)).toBe(ActionOutcome.Succeeded)
    expect(bodyTo('/api/portal/client/users/invite')).toEqual(invite)
    expect(store.inviting).toBe(false)
  })

  it('keeps the sentence when an invitation is refused', async () => {
    table[`${HttpMethod.Post} /api/portal/client/users/invite`] = refused
    const store = useAccountStore()

    expect(await store.inviteUser(invite)).toBe(ActionOutcome.Failed)
    expect(store.inviteError).toBe('Refused.')
  })

  it('keeps the sentence when a removal is refused and clears the in-flight id', async () => {
    table[`${HttpMethod.Delete} /api/portal/client/users/7`] = refused
    const store = useAccountStore()

    expect(await store.removeUser('7')).toBe(ActionOutcome.Failed)
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
    table[`${HttpMethod.Post} /api/portal/client/contacts`] = { body: 5 }
    table[`${HttpMethod.Put} /api/portal/client/contacts/5`] = { body: {} }
    const store = useAccountStore()
    const body = makeContactInput({ firstName: 'A', lastName: 'L' })

    expect(await store.saveContact(null, body)).toBe(ContactSaveOutcome.Saved)
    expect(await store.saveContact(5, body)).toBe(ContactSaveOutcome.Saved)

    expect(bodyTo('/api/portal/client/contacts')).toEqual(body)
    expect(bodyTo('/api/portal/client/contacts/5')).toEqual(body)
    expect(store.contactSaving).toBe(false)
  })

  it('keeps the sentence when saving is refused', async () => {
    table[`${HttpMethod.Post} /api/portal/client/contacts`] = refused
    const store = useAccountStore()

    expect(await store.saveContact(null, makeContactInput({ firstName: 'A', lastName: 'L' }))).toBe(ContactSaveOutcome.Failed)
    expect(store.contactError).toBe('Refused.')
  })

  it('reports Failed for a refused removal', async () => {
    table[`${HttpMethod.Delete} /api/portal/client/contacts/5`] = refused
    const store = useAccountStore()

    expect(await store.removeContact(5)).toBe(ActionOutcome.Failed)
  })
})

describe('useAccountStore two-factor', () => {
  it('returns the setup with a locally rendered QR code', async () => {
    table[`${HttpMethod.Post} /api/portal/auth/2fa-setup`] = { body: { secret: 'S', qrCodeUri: 'otpauth://x' } }
    const store = useAccountStore()

    const setup = await store.startTwoFactorSetup()

    expect(setup).toEqual({ secret: 'S', qrCodeUri: 'otpauth://x', qrDataUrl: 'data:image/png;base64,QR' })
    expect(store.tfaLoading).toBe(false)
  })

  it('returns null and keeps the sentence when enrolment cannot start', async () => {
    table[`${HttpMethod.Post} /api/portal/auth/2fa-setup`] = refused
    const store = useAccountStore()

    expect(await store.startTwoFactorSetup()).toBeNull()
    expect(store.tfaError).toBe('Refused.')
  })

  it('enables with the code and reloads the profile', async () => {
    table[`${HttpMethod.Post} /api/portal/auth/2fa-enable`] = { body: {} }
    const store = useAccountStore()

    expect(await store.submitTwoFactor(TwoFactorMode.Enable, '123456')).toBe(ActionOutcome.Succeeded)

    expect(bodyTo('/api/portal/auth/2fa-enable')).toEqual({ code: '123456' })
    expect(requestsTo(stub.requests, ME_PATH)).toHaveLength(1)
    expect(requestsTo(stub.requests, LOGOUT_PATH)).toHaveLength(0)
    expect(navigate).not.toHaveBeenCalled()
  })

  it('disables with the code and the password, then signs this browser out', async () => {
    table[`${HttpMethod.Post} /api/portal/auth/2fa-disable`] = { body: {} }
    const store = useAccountStore()

    expect(await store.submitTwoFactor(TwoFactorMode.Disable, '654321', 'secret-pw')).toBe(ActionOutcome.Succeeded)

    expect(bodyTo('/api/portal/auth/2fa-disable')).toEqual({ code: '654321', currentPassword: 'secret-pw' })
    expect(requestsTo(stub.requests, LOGOUT_PATH)).toHaveLength(1)
    expect(navigate).toHaveBeenCalledWith('/client/login')
    expect(requestsTo(stub.requests, ME_PATH)).toHaveLength(0)
  })

  it('stays signed in when switching off is refused', async () => {
    table[`${HttpMethod.Post} /api/portal/auth/2fa-disable`] = refused
    const store = useAccountStore()

    expect(await store.submitTwoFactor(TwoFactorMode.Disable, '654321', 'wrong')).toBe(ActionOutcome.Failed)

    expect(store.tfaModalError).toBe('Refused.')
    expect(requestsTo(stub.requests, LOGOUT_PATH)).toHaveLength(0)
    expect(navigate).not.toHaveBeenCalled()
  })

  it('keeps the sentence when a code is refused', async () => {
    table[`${HttpMethod.Post} /api/portal/auth/2fa-enable`] = refused
    const store = useAccountStore()

    expect(await store.submitTwoFactor(TwoFactorMode.Enable, 'x')).toBe(ActionOutcome.Failed)
    expect(store.tfaModalError).toBe('Refused.')
    expect(store.tfaSaving).toBe(false)
  })
})

describe('useAccountStore sign-in address', () => {
  it('asks for the change and remembers where the link went', async () => {
    table[`${HttpMethod.Post} /api/portal/client/email-change`] = { body: {} }
    const store = useAccountStore()

    const outcome = await store.startEmailChange({ newEmail: 'new@example.com', currentPassword: 'pw' })

    expect(outcome).toBe(ActionOutcome.Succeeded)
    expect(bodyTo('/api/portal/client/email-change')).toEqual({ newEmail: 'new@example.com', currentPassword: 'pw' })
    expect(store.emailChangeSentTo).toBe('new@example.com')
    expect(store.emailChangeSaving).toBe(false)
  })

  it('keeps the sentence and sends nobody to an inbox when the change is refused', async () => {
    table[`${HttpMethod.Post} /api/portal/client/email-change`] = refused
    const store = useAccountStore()

    const outcome = await store.startEmailChange({ newEmail: 'new@example.com', currentPassword: 'wrong' })

    expect(outcome).toBe(ActionOutcome.Failed)
    expect(store.emailChangeError).toBe('Refused.')
    expect(store.emailChangeSentTo).toBeNull()
  })

  it('confirms the change and ends this browser session', async () => {
    table[`${HttpMethod.Post} /api/portal/client/email-change/confirm`] = { body: {} }
    const store = useAccountStore()

    const status = await store.confirmEmailChange('new@example.com', 'tok')

    expect(status).toBe(EmailConfirmationStatus.Confirmed)
    expect(bodyTo('/api/portal/client/email-change/confirm')).toEqual({ email: 'new@example.com', token: 'tok' })
    expect(requestsTo(stub.requests, LOGOUT_PATH)).toHaveLength(1)
  })

  it('refuses a link with nothing in it without a request', async () => {
    const store = useAccountStore()

    const status = await store.confirmEmailChange('', '')

    expect(status).toBe(EmailConfirmationStatus.Failed)
    expect(store.emailChangeConfirmError).toBe('client.confirmEmail.invalidLink')
    expect(stub.requests).toHaveLength(0)
  })

  it('keeps the sentence and the session when the token is refused', async () => {
    table[`${HttpMethod.Post} /api/portal/client/email-change/confirm`] = refused
    const store = useAccountStore()

    const status = await store.confirmEmailChange('new@example.com', 'bad')

    expect(status).toBe(EmailConfirmationStatus.Failed)
    expect(store.emailChangeConfirmError).toBe('Refused.')
    expect(requestsTo(stub.requests, LOGOUT_PATH)).toHaveLength(0)
  })
})

describe('useAccountStore reads', () => {
  it.each([
    ['loadUsers', '/api/portal/client/users', 'users', 'usersLoading'],
    ['loadContacts', '/api/portal/client/contacts', 'contacts', 'contactsLoading']
  ] as const)('%s keeps the list, and empties it when the read fails', async (action, path, state, flag) => {
    table[`${HttpMethod.Get} ${path}`] = { body: [{ id: '1' }] }
    const store = useAccountStore()

    await store[action]()
    expect(store[state]).toEqual([{ id: '1' }])
    expect(store[flag]).toBe(false)

    table[`${HttpMethod.Get} ${path}`] = { status: 400 }
    await store[action]()
    expect(store[state]).toEqual([])
    expect(store[flag]).toBe(false)
  })

  it('keeps the rows of the sent-mail page, and empties them when the read fails', async () => {
    const emailsRoute = `${HttpMethod.Get} /api/portal/client/emails`
    const row = { id: 1, to: 'a@b.test', subject: 'Invoice', sentAt: '2026-02-03T00:00:00Z', success: true }
    table[emailsRoute] = { body: { items: [row], pageNumber: 1, pageSize: 100, totalCount: 1, totalPages: 1 } }
    const store = useAccountStore()

    await store.loadEmails()
    expect(store.emails).toEqual([row])
    expect(store.emailsLoading).toBe(false)

    table[emailsRoute] = { status: 400 }
    await store.loadEmails()
    expect(store.emails).toEqual([])
    expect(store.emailsLoading).toBe(false)
  })

  it('keeps the gateways the profile can default to, and none when the read fails', async () => {
    const methodsRoute = `${HttpMethod.Get} /api/portal/order/payment-methods`
    table[methodsRoute] = { body: [{ module: 'card_gateway', displayName: 'Card' }] }
    const store = useAccountStore()

    await store.loadGatewayMethods()
    expect(store.gatewayMethods).toHaveLength(1)

    table[methodsRoute] = { status: 400 }
    await store.loadGatewayMethods()
    expect(store.gatewayMethods).toEqual([])
  })
})

describe('useAccountStore modal errors', () => {
  it('clears every modal error on request', async () => {
    table[`${HttpMethod.Patch} ${ME_PATH}`] = refused
    const store = useAccountStore()
    await store.saveProfile({})

    store.clearModalErrors()

    expect(store.saveError).toBe('')
  })
})

describe('useAccountStore reads without the permission', () => {
  it.each([
    ['loadUsers', '/api/portal/client/users', 'users'],
    ['loadContacts', '/api/portal/client/contacts', 'contacts'],
    ['loadEmails', '/api/portal/client/emails', 'emails'],
  ] as const)('%s sends no request and leaves the list empty', async (action, path, list) => {
    useClientStore().user = makeClientUser({ permissions: Permission.None })
    const store = useAccountStore()

    await store[action]()

    expect(requestsTo(stub.requests, path)).toHaveLength(0)
    expect(store[list]).toEqual([])
  })
})
