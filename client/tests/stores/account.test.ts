/**
 * Tests for `stores/account.ts`.
 *
 * The store, not the account page, owns every loading flag and refusal sentence, so these
 * assertions pin the contract the page renders from.
 *
 * @module tests/stores/account.test
 */

import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { ref } from 'vue'
import { makeContactInput } from '~/tests/helpers/accountContact'
import { Permission } from '~/composables/usePermissions'
import { InvitePermissionMode } from '~/types/invitepermissionmode'
import type { InviteUserPayload } from '~/types/inviteuserpayload'

/** Mocked client endpoints, re-created for every test. */
const clientApi = {
  updateMe: vi.fn(),
  startEmailChange: vi.fn(),
  confirmEmailChange: vi.fn(),
  inviteUser: vi.fn(),
  removeUser: vi.fn(),
  createContact: vi.fn(),
  updateContact: vi.fn(),
  removeContact: vi.fn(),
  fetchUsers: vi.fn(),
  fetchContacts: vi.fn(),
  fetchEmails: vi.fn()
}

/** Mocked billing endpoints. */
const billingApi = {
  setDefaultPaymentMethod: vi.fn(),
  removePaymentMethod: vi.fn(),
  fetchPaymentMethods: vi.fn(),
  fetchGatewayMethods: vi.fn()
}

/** Mocked two-factor endpoints. */
const authApi = {
  setupTwoFactor: vi.fn(),
  enableTwoFactor: vi.fn(),
  disableTwoFactor: vi.fn()
}

/** Mocked profile reload on the client store. */
const fetchUser = vi.fn()

/** Mocked sign-out on the auth store, which ends the session and leaves for the sign-in page. */
const signOut = vi.fn()

/** Mocked local session end on the auth store, which keeps the page. */
const endLocalSession = vi.fn()

/** Mocked permission check on the client store; every permission is held unless a test says otherwise. */
const mayDo = vi.fn()

vi.stubGlobal('ref', ref)

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

vi.mock('~/composables/apis/useClientApi', () => ({
  /**
   * Stands in for the client API composable.
   *
   * @returns The mocked endpoints.
   */
  useClientApi: () => clientApi
}))

vi.mock('~/composables/apis/useBillingApi', () => ({
  /**
   * Stands in for the billing API composable.
   *
   * @returns The mocked endpoints.
   */
  useBillingApi: () => billingApi
}))

vi.mock('~/composables/apis/useAuthApi', () => ({
  /**
   * Stands in for the auth API composable.
   *
   * @returns The mocked endpoints.
   */
  useAuthApi: () => authApi
}))

vi.mock('~/stores/client', () => ({
  /**
   * Stands in for the client store.
   *
   * @returns A store exposing only the profile reload and the permission check.
   */
  useClientStore: () => ({ fetchUser, mayDo })
}))

vi.mock('~/stores/auth', () => ({
  /**
   * Stands in for the auth store.
   *
   * @returns A store exposing only the two ways of ending the session.
   */
  useAuthStore: () => ({ signOut, endLocalSession })
}))

vi.mock('~/utils/translate', () => ({
  /**
   * Stands in for the out-of-component translator.
   *
   * @param key - The message key.
   * @returns The key itself.
   */
  translate: (key: string): string => key
}))

const { useAccountStore } = await import('~/stores/account')
const { EmailConfirmationStatus } = await import('~/types/emailconfirmationstatus')
const { ActionOutcome } = await import('~/types/actionoutcome')
const { ContactSaveOutcome } = await import('~/types/contactsaveoutcome')
const { TwoFactorMode } = await import('~/types/twofactormode')

/** A refusal shaped like the API's problem body. */
const refused = { data: { detail: 'Refused.' } }

/** A complete invitation granting every permission. */
const invite: InviteUserPayload = { email: 'a@b.c', firstName: 'A', lastName: 'B', permissionMode: InvitePermissionMode.All, permissions: [] }

beforeEach(() => {
  setActivePinia(createPinia())
  for (const api of [clientApi, billingApi, authApi]) {
    for (const fn of Object.values(api)) {
      fn.mockReset()
    }
  }
  fetchUser.mockReset()
  signOut.mockReset()
  endLocalSession.mockReset()
  mayDo.mockReset()
  mayDo.mockReturnValue(true)
})

describe('useAccountStore.saveProfile', () => {
  it('saves, reloads the profile and reports Succeeded', async () => {
    clientApi.updateMe.mockResolvedValue({})
    const store = useAccountStore()

    const outcome = await store.saveProfile({ firstName: 'Ada' })

    expect(outcome).toBe(ActionOutcome.Succeeded)
    expect(fetchUser).toHaveBeenCalledWith(true)
    expect(store.saveError).toBe('')
    expect(store.saving).toBe(false)
  })

  it('keeps the API sentence and reports Failed', async () => {
    clientApi.updateMe.mockRejectedValue(refused)
    const store = useAccountStore()

    const outcome = await store.saveProfile({})

    expect(outcome).toBe(ActionOutcome.Failed)
    expect(store.saveError).toBe('Refused.')
    expect(fetchUser).not.toHaveBeenCalled()
    expect(store.saving).toBe(false)
  })
})

describe('useAccountStore field errors', () => {
  it('keeps the field messages of a refused profile save and clears them when the next save starts', async () => {
    clientApi.updateMe.mockRejectedValueOnce({ data: { type: 'VALIDATION_ERROR', errors: { phone: ['Phone is invalid.'] } } })
    const store = useAccountStore()

    expect(await store.saveProfile({})).toBe(ActionOutcome.Failed)
    expect(store.fieldErrors).toEqual({ phone: 'Phone is invalid.' })

    clientApi.updateMe.mockResolvedValueOnce({})
    await store.saveProfile({})
    expect(store.fieldErrors).toEqual({})
  })

})

describe('useAccountStore team members', () => {
  it('reports Succeeded for an invitation', async () => {
    clientApi.inviteUser.mockResolvedValue({})
    const store = useAccountStore()

    expect(await store.inviteUser(invite)).toBe(ActionOutcome.Succeeded)
    expect(store.inviting).toBe(false)
  })

  it('keeps the sentence when an invitation is refused', async () => {
    clientApi.inviteUser.mockRejectedValue(refused)
    const store = useAccountStore()

    expect(await store.inviteUser(invite)).toBe(ActionOutcome.Failed)
    expect(store.inviteError).toBe('Refused.')
  })

  it('keeps the sentence when a removal is refused and clears the in-flight id', async () => {
    clientApi.removeUser.mockRejectedValue(refused)
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
    expect(clientApi.createContact).not.toHaveBeenCalled()
  })

  it('creates when the id is null and updates when it is set', async () => {
    clientApi.createContact.mockResolvedValue(5)
    clientApi.updateContact.mockResolvedValue({})
    const store = useAccountStore()
    const body = makeContactInput({ firstName: 'A', lastName: 'L' })

    expect(await store.saveContact(null, body)).toBe(ContactSaveOutcome.Saved)
    expect(await store.saveContact(5, body)).toBe(ContactSaveOutcome.Saved)

    expect(clientApi.createContact).toHaveBeenCalledWith(body)
    expect(clientApi.updateContact).toHaveBeenCalledWith(5, body)
    expect(store.contactSaving).toBe(false)
  })

  it('keeps the sentence when saving is refused', async () => {
    clientApi.createContact.mockRejectedValue(refused)
    const store = useAccountStore()

    expect(await store.saveContact(null, makeContactInput({ firstName: 'A', lastName: 'L' }))).toBe(ContactSaveOutcome.Failed)
    expect(store.contactError).toBe('Refused.')
  })

  it('reports Failed for a refused removal', async () => {
    clientApi.removeContact.mockRejectedValue(refused)
    const store = useAccountStore()

    expect(await store.removeContact(5)).toBe(ActionOutcome.Failed)
  })
})

describe('useAccountStore two-factor', () => {
  it('returns the setup with a locally rendered QR code', async () => {
    authApi.setupTwoFactor.mockResolvedValue({ secret: 'S', qrCodeUri: 'otpauth://x' })
    const store = useAccountStore()

    const setup = await store.startTwoFactorSetup()

    expect(setup).toEqual({ secret: 'S', qrCodeUri: 'otpauth://x', qrDataUrl: 'data:image/png;base64,QR' })
    expect(store.tfaLoading).toBe(false)
  })

  it('returns null and keeps the sentence when enrolment cannot start', async () => {
    authApi.setupTwoFactor.mockRejectedValue(refused)
    const store = useAccountStore()

    expect(await store.startTwoFactorSetup()).toBeNull()
    expect(store.tfaError).toBe('Refused.')
  })

  it('enables with the code and reloads the profile', async () => {
    authApi.enableTwoFactor.mockResolvedValue({})
    const store = useAccountStore()

    expect(await store.submitTwoFactor(TwoFactorMode.Enable, '123456')).toBe(ActionOutcome.Succeeded)

    expect(authApi.enableTwoFactor).toHaveBeenCalledWith('123456')
    expect(fetchUser).toHaveBeenCalledTimes(1)
    expect(signOut).not.toHaveBeenCalled()
  })

  it('disables with the code and the password, then signs this browser out', async () => {
    authApi.disableTwoFactor.mockResolvedValue({})
    const store = useAccountStore()

    expect(await store.submitTwoFactor(TwoFactorMode.Disable, '654321', 'secret-pw')).toBe(ActionOutcome.Succeeded)

    expect(authApi.disableTwoFactor).toHaveBeenCalledWith('654321', 'secret-pw')
    expect(signOut).toHaveBeenCalledTimes(1)
    expect(fetchUser).not.toHaveBeenCalled()
  })

  it('stays signed in when switching off is refused', async () => {
    authApi.disableTwoFactor.mockRejectedValue(refused)
    const store = useAccountStore()

    expect(await store.submitTwoFactor(TwoFactorMode.Disable, '654321', 'wrong')).toBe(ActionOutcome.Failed)

    expect(store.tfaModalError).toBe('Refused.')
    expect(signOut).not.toHaveBeenCalled()
  })

  it('keeps the sentence when a code is refused', async () => {
    authApi.enableTwoFactor.mockRejectedValue(refused)
    const store = useAccountStore()

    expect(await store.submitTwoFactor(TwoFactorMode.Enable, 'x')).toBe(ActionOutcome.Failed)
    expect(store.tfaModalError).toBe('Refused.')
    expect(store.tfaSaving).toBe(false)
  })
})

describe('useAccountStore sign-in address', () => {
  it('asks for the change and remembers where the link went', async () => {
    clientApi.startEmailChange.mockResolvedValue(undefined)
    const store = useAccountStore()

    const outcome = await store.startEmailChange({ newEmail: 'new@example.com', currentPassword: 'pw' })

    expect(outcome).toBe(ActionOutcome.Succeeded)
    expect(clientApi.startEmailChange).toHaveBeenCalledWith({ newEmail: 'new@example.com', currentPassword: 'pw' })
    expect(store.emailChangeSentTo).toBe('new@example.com')
    expect(store.emailChangeSaving).toBe(false)
  })

  it('keeps the sentence and sends nobody to an inbox when the change is refused', async () => {
    clientApi.startEmailChange.mockRejectedValue(refused)
    const store = useAccountStore()

    const outcome = await store.startEmailChange({ newEmail: 'new@example.com', currentPassword: 'wrong' })

    expect(outcome).toBe(ActionOutcome.Failed)
    expect(store.emailChangeError).toBe('Refused.')
    expect(store.emailChangeSentTo).toBeNull()
  })

  it('confirms the change and ends this browser session', async () => {
    clientApi.confirmEmailChange.mockResolvedValue(undefined)
    const store = useAccountStore()

    const status = await store.confirmEmailChange('new@example.com', 'tok')

    expect(status).toBe(EmailConfirmationStatus.Confirmed)
    expect(clientApi.confirmEmailChange).toHaveBeenCalledWith('new@example.com', 'tok')
    expect(endLocalSession).toHaveBeenCalledTimes(1)
  })

  it('refuses a link with nothing in it without a request', async () => {
    const store = useAccountStore()

    const status = await store.confirmEmailChange('', '')

    expect(status).toBe(EmailConfirmationStatus.Failed)
    expect(store.emailChangeConfirmError).toBe('client.confirmEmail.invalidLink')
    expect(clientApi.confirmEmailChange).not.toHaveBeenCalled()
  })

  it('keeps the sentence and the session when the token is refused', async () => {
    clientApi.confirmEmailChange.mockRejectedValue(refused)
    const store = useAccountStore()

    const status = await store.confirmEmailChange('new@example.com', 'bad')

    expect(status).toBe(EmailConfirmationStatus.Failed)
    expect(store.emailChangeConfirmError).toBe('Refused.')
    expect(endLocalSession).not.toHaveBeenCalled()
  })
})

describe('useAccountStore payment methods', () => {
  it('reports Succeeded for set-default and clears the in-flight id', async () => {
    billingApi.setDefaultPaymentMethod.mockResolvedValue({})
    const store = useAccountStore()

    expect(await store.setDefaultPaymentMethod('1')).toBe(ActionOutcome.Succeeded)
    expect(store.settingDefaultId).toBeNull()
  })

  it('keeps the sentence when a removal is refused', async () => {
    billingApi.removePaymentMethod.mockRejectedValue(refused)
    const store = useAccountStore()

    expect(await store.removePaymentMethod('1')).toBe(ActionOutcome.Failed)
    expect(store.paymentActionError).toBe('Refused.')
    expect(store.removingId).toBeNull()
  })

  it('clears every modal error on request', async () => {
    clientApi.updateMe.mockRejectedValue(refused)
    const store = useAccountStore()
    await store.saveProfile({})

    store.clearModalErrors()

    expect(store.saveError).toBe('')
  })
})

describe('useAccountStore reads', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    Object.values(clientApi).forEach(fn => fn.mockReset())
    Object.values(billingApi).forEach(fn => fn.mockReset())
  })

  it.each([
    ['loadUsers', 'fetchUsers', 'users', 'usersLoading'],
    ['loadContacts', 'fetchContacts', 'contacts', 'contactsLoading']
  ] as const)('%s keeps the list, and empties it when the read fails', async (action, endpoint, state, flag) => {
    clientApi[endpoint].mockResolvedValueOnce([{ id: '1' }]).mockRejectedValueOnce(new Error('down'))
    const store = useAccountStore()

    await store[action]()
    expect(store[state]).toEqual([{ id: '1' }])
    expect(store[flag]).toBe(false)

    await store[action]()
    expect(store[state]).toEqual([])
    expect(store[flag]).toBe(false)
  })

  it('keeps the rows of the sent-mail page, and empties them when the read fails', async () => {
    const row = { id: 1, to: 'a@b.test', subject: 'Invoice', sentAt: '2026-02-03T00:00:00Z', success: true }
    clientApi.fetchEmails
      .mockResolvedValueOnce({ items: [row], pageNumber: 1, pageSize: 100, totalCount: 1, totalPages: 1 })
      .mockRejectedValueOnce(new Error('down'))
    const store = useAccountStore()

    await store.loadEmails()
    expect(store.emails).toEqual([row])
    expect(store.emailsLoading).toBe(false)

    await store.loadEmails()
    expect(store.emails).toEqual([])
    expect(store.emailsLoading).toBe(false)
  })

  it('keeps the saved payment methods', async () => {
    billingApi.fetchPaymentMethods.mockResolvedValue([{ id: 'pm_1' }])
    const store = useAccountStore()

    await store.loadPaymentMethods()

    expect(store.paymentMethods).toEqual([{ id: 'pm_1' }])
    expect(store.paymentLoadError).toBeNull()
  })

  it('says it could not read the payment methods instead of showing an empty account', async () => {
    billingApi.fetchPaymentMethods.mockRejectedValue({ data: { detail: 'Cannot reach the gateway.' } })
    const store = useAccountStore()

    await store.loadPaymentMethods()

    expect(store.paymentMethods).toEqual([])
    expect(store.paymentLoadError).toBe('Cannot reach the gateway.')
    expect(store.paymentLoading).toBe(false)
  })

  it('keeps the gateways the profile can default to, and none when the read fails', async () => {
    billingApi.fetchGatewayMethods.mockResolvedValueOnce([{ module: 'stripe', displayName: 'Card' }]).mockRejectedValueOnce(new Error('down'))
    const store = useAccountStore()

    await store.loadGatewayMethods()
    expect(store.gatewayMethods).toHaveLength(1)

    await store.loadGatewayMethods()
    expect(store.gatewayMethods).toEqual([])
  })
})

describe('useAccountStore reads without the permission', () => {
  it.each([
    ['loadUsers', 'fetchUsers', 'users'],
    ['loadContacts', 'fetchContacts', 'contacts'],
    ['loadEmails', 'fetchEmails', 'emails'],
  ] as const)('%s sends no request and leaves the list empty', async (action, endpoint, list) => {
    mayDo.mockReturnValue(false)
    const store = useAccountStore()

    await store[action]()

    expect(clientApi[endpoint]).not.toHaveBeenCalled()
    expect(store[list]).toEqual([])
  })

  it('loadPaymentMethods sends no request and reports no failure', async () => {
    mayDo.mockReturnValue(false)
    const store = useAccountStore()

    await store.loadPaymentMethods()

    expect(mayDo).toHaveBeenCalledWith(Permission.ViewPayInvoices)
    expect(billingApi.fetchPaymentMethods).not.toHaveBeenCalled()
    expect(store.paymentMethods).toEqual([])
    expect(store.paymentLoadError).toBeNull()
  })
})
