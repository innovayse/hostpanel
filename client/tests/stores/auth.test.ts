/**
 * Tests for the sign-in action in `stores/auth.ts`.
 *
 * The store, not the login page, owns the refusal sentence and the in-flight flag, so these
 * assertions pin the contract the page renders from.
 *
 * @module tests/stores/auth.test
 */

import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { ref, computed } from 'vue'

/** The mocked password sign-in call, re-created for every test. */
const loginMock = vi.fn()

/** The mocked second-factor sign-in call, re-created for every test. */
const loginWithTwoFactorMock = vi.fn()

/** The mocked email-confirmation call. */
const confirmEmailMock = vi.fn()

/** The mocked email-status read. */
const fetchEmailVerifiedMock = vi.fn()

/** The mocked verification-mail resend. */
const resendVerificationMock = vi.fn()

/** The mocked password-reset request call. */
const requestPasswordResetMock = vi.fn()

/** The mocked accept-invitation call. */
const acceptInviteMock = vi.fn()

/** The mocked registration call. */
const registerMock = vi.fn()

/** The mocked sign-out call. */
const logoutMock = vi.fn()

/** The mocked password-reset call. */
const resetPasswordMock = vi.fn()

vi.stubGlobal('ref', ref)
vi.stubGlobal('computed', computed)
vi.stubGlobal('useCookie', () => ref(null))

vi.mock('~/composables/apis/useAuthApi', () => ({
  /**
   * Stands in for the real API composable so the store runs without transport.
   *
   * @returns Only the endpoint the tests reach for.
   */
  useAuthApi: () => ({
    login: loginMock,
    loginWithTwoFactor: loginWithTwoFactorMock,
    confirmEmail: confirmEmailMock,
    fetchEmailVerified: fetchEmailVerifiedMock,
    resendVerification: resendVerificationMock,
    requestPasswordReset: requestPasswordResetMock,
    acceptInvite: acceptInviteMock,
    register: registerMock,
    resetPassword: resetPasswordMock,
    logout: logoutMock
  })
}))

vi.mock('~/stores/client', () => ({
  /**
   * Stands in for the client store, which sign-in does not touch.
   *
   * @returns An inert store.
   */
  useClientStore: () => ({ reset: vi.fn() })
}))

const { useAuthStore } = await import('~/stores/auth')
const { useCartStore } = await import('~/stores/cart')
const { PricingCycle } = await import('~/types/pricingcycle')
const { SignInOutcome } = await import('~/types/signinoutcome')
const { ActionOutcome } = await import('~/types/actionoutcome')
const { EmailConfirmationStatus } = await import('~/types/emailconfirmationstatus')
const { InviteOutcome } = await import('~/types/inviteoutcome')

describe('useAuthStore.signIn', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    loginMock.mockReset()
    loginWithTwoFactorMock.mockReset()
  })

  it('reports SignedIn, stores the account and leaves no error', async () => {
    loginMock.mockResolvedValue({ id: 1, email: 'ada@example.com' })
    const store = useAuthStore()

    const outcome = await store.signIn('ada@example.com', 'pw')

    expect(outcome).toBe(SignInOutcome.SignedIn)
    expect(store.error).toBe('')
    expect(store.loading).toBe(false)
    expect(store.user).toMatchObject({ id: 1 })
  })

  it('does not store the { success: true } acknowledgement as the account, so the profile is fetched later', async () => {
    loginMock.mockResolvedValue({ success: true })
    const store = useAuthStore()

    const outcome = await store.signIn('ada@example.com', 'pw')

    expect(outcome).toBe(SignInOutcome.SignedIn)
    expect(store.user).toBeNull()
  })

  it('keeps the API sentence as error and reports Failed when the credentials are refused', async () => {
    loginMock.mockRejectedValue({ data: { detail: 'Wrong email or password.' } })
    const store = useAuthStore()

    const outcome = await store.signIn('ada@example.com', 'bad')

    expect(outcome).toBe(SignInOutcome.Failed)
    expect(store.error).toBe('Wrong email or password.')
    expect(store.loading).toBe(false)
    expect(store.user).toBeNull()
  })

  it('keeps the field messages of a refused sign-in and clears them on the next attempt', async () => {
    loginMock.mockRejectedValueOnce({ data: { type: 'VALIDATION_ERROR', detail: 'Invalid.', errors: { email: ['Email is invalid.'] } } })
    const store = useAuthStore()

    expect(await store.signIn('nope', 'x')).toBe(SignInOutcome.Failed)
    expect(store.fieldErrors).toEqual({ email: 'Email is invalid.' })

    loginMock.mockResolvedValueOnce({ id: 1 })
    await store.signIn('ada@example.com', 'ok')
    expect(store.fieldErrors).toEqual({})
  })

  it('reports TwoFactorRequired, keeps the challenge and leaves the account unset', async () => {
    loginMock.mockResolvedValue({ twoFactorRequired: true, pendingToken: 't' })
    const store = useAuthStore()

    const outcome = await store.signIn('ada@example.com', 'pw')

    expect(outcome).toBe(SignInOutcome.TwoFactorRequired)
    expect(store.twoFactorToken).toBe('t')
    expect(store.error).toBe('')
    expect(store.user).toBeNull()
  })

  it('finishes a second-factor sign-in with the open challenge and the typed code', async () => {
    loginMock.mockResolvedValue({ twoFactorRequired: true, pendingToken: 't' })
    loginWithTwoFactorMock.mockResolvedValue({ id: 7 })
    const store = useAuthStore()
    await store.signIn('ada@example.com', 'pw')

    const outcome = await store.confirmTwoFactor('123456')

    expect(loginWithTwoFactorMock).toHaveBeenCalledWith('t', '123456')
    expect(outcome).toBe(SignInOutcome.SignedIn)
    expect(store.twoFactorToken).toBe('')
    expect(store.user).toEqual({ id: 7 })
  })

  it('keeps the challenge open and shows the sentence when the code is refused', async () => {
    loginMock.mockResolvedValue({ twoFactorRequired: true, pendingToken: 't' })
    loginWithTwoFactorMock.mockRejectedValue({ data: { detail: 'Invalid code.' } })
    const store = useAuthStore()
    await store.signIn('ada@example.com', 'pw')

    const outcome = await store.confirmTwoFactor('000000')

    expect(outcome).toBe(SignInOutcome.Failed)
    expect(store.error).toBe('Invalid code.')
    expect(store.twoFactorToken).toBe('t')
    expect(store.loading).toBe(false)
  })

  it('drops the challenge when the visitor goes back to the password form', async () => {
    loginMock.mockResolvedValue({ twoFactorRequired: true, pendingToken: 't' })
    const store = useAuthStore()
    await store.signIn('ada@example.com', 'pw')

    store.cancelTwoFactor()

    expect(store.twoFactorToken).toBe('')
  })

  it('clears a previous error when the next attempt starts', async () => {
    loginMock.mockRejectedValueOnce({ data: { detail: 'No.' } })
    loginMock.mockResolvedValueOnce({ id: 2 })
    const store = useAuthStore()

    await store.signIn('a', 'b')
    await store.signIn('a', 'b')

    expect(store.error).toBe('')
  })
})

describe('useAuthStore.confirmEmail', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    confirmEmailMock.mockReset()
  })

  it('fails without a request when the link is missing its email or token', async () => {
    const store = useAuthStore()

    const status = await store.confirmEmail('', 'tok')

    expect(status).toBe(EmailConfirmationStatus.Failed)
    expect(store.confirmError).not.toBe('')
    expect(confirmEmailMock).not.toHaveBeenCalled()
  })

  it('reports Confirmed when the API accepts the link', async () => {
    confirmEmailMock.mockResolvedValue(undefined)
    const store = useAuthStore()

    const status = await store.confirmEmail('ada@example.com', 'tok')

    expect(status).toBe(EmailConfirmationStatus.Confirmed)
    expect(store.confirmStatus).toBe(EmailConfirmationStatus.Confirmed)
    expect(store.confirmError).toBe('')
  })

  it('keeps the API sentence and reports Failed when the link is refused', async () => {
    confirmEmailMock.mockRejectedValue({ data: { detail: 'Link expired.' } })
    const store = useAuthStore()

    const status = await store.confirmEmail('ada@example.com', 'tok')

    expect(status).toBe(EmailConfirmationStatus.Failed)
    expect(store.confirmError).toBe('Link expired.')
  })
})

describe('useAuthStore email verification', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    fetchEmailVerifiedMock.mockReset()
    resendVerificationMock.mockReset()
  })

  it('keeps the three-valued status the API answers with', async () => {
    fetchEmailVerifiedMock.mockResolvedValue({ verified: false })
    const store = useAuthStore()

    await store.loadEmailVerified()

    expect(store.emailVerified).toBe(false)
  })

  it('leaves the status unknown when the read fails', async () => {
    fetchEmailVerifiedMock.mockRejectedValue(new Error('down'))
    const store = useAuthStore()

    await store.loadEmailVerified()

    expect(store.emailVerified).toBeNull()
  })

  it('reports Succeeded and a local sentence when the mail is sent', async () => {
    resendVerificationMock.mockResolvedValue(undefined)
    const store = useAuthStore()

    const outcome = await store.resendVerification()

    expect(outcome).toBe(ActionOutcome.Succeeded)
    expect(store.resendMessage).not.toBe('')
    expect(store.resending).toBe(false)
  })

  it('keeps the API sentence and reports Failed when the resend is refused', async () => {
    resendVerificationMock.mockRejectedValue({ data: { detail: 'Too many requests.' } })
    const store = useAuthStore()

    const outcome = await store.resendVerification()

    expect(outcome).toBe(ActionOutcome.Failed)
    expect(store.resendMessage).toBe('Too many requests.')
  })
})

describe('useAuthStore.requestPasswordReset', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    requestPasswordResetMock.mockReset()
  })

  it('reports Succeeded and leaves no error', async () => {
    requestPasswordResetMock.mockResolvedValue(undefined)
    const store = useAuthStore()

    const outcome = await store.requestPasswordReset('ada@example.com')

    expect(outcome).toBe(ActionOutcome.Succeeded)
    expect(store.resetRequestError).toBe('')
    expect(store.resetRequesting).toBe(false)
  })

  it('keeps the API sentence and reports Failed when the request is refused', async () => {
    requestPasswordResetMock.mockRejectedValue({ data: { detail: 'Too many requests.' } })
    const store = useAuthStore()

    const outcome = await store.requestPasswordReset('ada@example.com')

    expect(outcome).toBe(ActionOutcome.Failed)
    expect(store.resetRequestError).toBe('Too many requests.')
    expect(store.resetRequesting).toBe(false)
  })
})

describe('useAuthStore.acceptInvite', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    acceptInviteMock.mockReset()
  })

  it('reports Accepted when the API accepts the token', async () => {
    acceptInviteMock.mockResolvedValue(undefined)
    const store = useAuthStore()

    const outcome = await store.acceptInvite('tok')

    expect(outcome).toBe(InviteOutcome.Accepted)
    expect(store.inviteError).toBe('')
    expect(store.inviting).toBe(false)
  })

  it('reports SignInRequired when the refusal carries the sign-in-required code', async () => {
    acceptInviteMock.mockRejectedValue({ data: { type: 'INVITE_SIGN_IN_REQUIRED' } })
    const store = useAuthStore()

    const outcome = await store.acceptInvite('tok')

    expect(outcome).toBe(InviteOutcome.SignInRequired)
    expect(store.inviteOutcome).toBe(InviteOutcome.SignInRequired)
  })

  it('keeps the API sentence and reports Failed for any other refusal', async () => {
    acceptInviteMock.mockRejectedValue({ data: { type: 'INVITE_EXPIRED', detail: 'Invitation expired.' } })
    const store = useAuthStore()

    const outcome = await store.acceptInvite('tok')

    expect(outcome).toBe(InviteOutcome.Failed)
    expect(store.inviteError).toBe('Invitation expired.')
  })

  it('forgets the last outcome and sentence on clearInvite', async () => {
    acceptInviteMock.mockRejectedValue({ data: { type: 'INVITE_SIGN_IN_REQUIRED', detail: 'Sign in first.' } })
    const store = useAuthStore()
    await store.acceptInvite('tok')

    store.clearInvite()

    expect(store.inviteOutcome).toBeNull()
    expect(store.inviteError).toBe('')
  })
})

describe('useAuthStore.register', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    registerMock.mockReset()
  })

  it('reports Succeeded and leaves no error', async () => {
    registerMock.mockResolvedValue(undefined)
    const store = useAuthStore()
    const payload = { firstname: 'A', lastname: 'B', email: 'a@b.c', password: 'pw' }

    const outcome = await store.register(payload)

    expect(outcome).toBe(ActionOutcome.Succeeded)
    expect(registerMock).toHaveBeenCalledWith(payload)
    expect(store.registerError).toBe('')
    expect(store.registering).toBe(false)
  })

  it('keeps the API sentence and the field messages when the registration is refused', async () => {
    registerMock.mockRejectedValue({
      data: { detail: 'Fix the form.', type: 'VALIDATION_ERROR', errors: { email: ['Email is taken.'] } }
    })
    const store = useAuthStore()

    const outcome = await store.register({ firstname: 'A', lastname: 'B', email: 'a@b.c', password: 'pw' })

    expect(outcome).toBe(ActionOutcome.Failed)
    expect(store.registerError).toBe('Email is taken.')
    expect(store.fieldErrors).toEqual({ email: 'Email is taken.' })
    expect(store.registering).toBe(false)
  })

  it('clears the previous refusal when the next attempt starts', async () => {
    registerMock.mockRejectedValueOnce({ data: { detail: 'No.' } }).mockResolvedValueOnce(undefined)
    const store = useAuthStore()

    await store.register({ firstname: 'A', lastname: 'B', email: 'a@b.c', password: 'pw' })
    await store.register({ firstname: 'A', lastname: 'B', email: 'a@b.c', password: 'pw' })

    expect(store.registerError).toBe('')
    expect(store.fieldErrors).toEqual({})
  })
})

describe('useAuthStore.resetPassword', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    resetPasswordMock.mockReset()
  })

  it('sends the link values and the new password, and reports Succeeded', async () => {
    resetPasswordMock.mockResolvedValue(undefined)
    const store = useAuthStore()

    const outcome = await store.resetPassword('a@b.c', 'tok', 'newpass123')

    expect(outcome).toBe(ActionOutcome.Succeeded)
    expect(resetPasswordMock).toHaveBeenCalledWith('a@b.c', 'tok', 'newpass123')
    expect(store.resetError).toBe('')
    expect(store.resetting).toBe(false)
  })

  it('keeps the API sentence and the field messages when the reset is refused', async () => {
    resetPasswordMock.mockRejectedValue({
      data: { detail: 'Weak.', type: 'VALIDATION_ERROR', errors: { newPassword: ['Too short.'] } }
    })
    const store = useAuthStore()

    const outcome = await store.resetPassword('a@b.c', 'tok', 'x')

    expect(outcome).toBe(ActionOutcome.Failed)
    expect(store.resetError).toBe('Too short.')
    expect(store.fieldErrors).toEqual({ newPassword: 'Too short.' })
    expect(store.resetting).toBe(false)
  })
})

describe('useAuthStore.signOut', () => {
  /** Spy standing in for the auto-imported navigation helper. */
  const navigate = vi.fn()

  beforeEach(() => {
    setActivePinia(createPinia())
    logoutMock.mockReset().mockResolvedValue(undefined)
    navigate.mockReset()
    vi.stubGlobal('navigateTo', navigate)
  })

  it('leaves through the local sign-in page under SSO when no SSO address is configured', async () => {
    vi.stubGlobal('useRuntimeConfig', () => ({ public: { authMode: 'sso', ssoPublicUrl: '' } }))
    const store = useAuthStore()

    await store.signOut()

    expect(navigate).toHaveBeenCalledWith('/client/login')
  })

  it('leaves through the local sign-in page in local mode', async () => {
    vi.stubGlobal('useRuntimeConfig', () => ({ public: { authMode: 'local', ssoPublicUrl: 'https://sso.test' } }))
    const store = useAuthStore()

    await store.signOut()

    expect(navigate).toHaveBeenCalledWith('/client/login')
  })
})

describe('useAuthStore cart on sign-out', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    logoutMock.mockReset().mockResolvedValue(undefined)
    vi.stubGlobal('useRuntimeConfig', () => ({ public: { authMode: 'local' } }))
  })

  it('empties the cart when the client logs out', async () => {
    const cart = useCartStore()
    cart.items = [{ pid: 1, name: 'Plan', billingcycle: PricingCycle.Monthly, cycleLabel: 'Monthly', amount: 5, currency: 'USD' }]

    await useAuthStore().logout()

    expect(cart.items).toEqual([])
  })

  it('empties the cart when the local session ends', async () => {
    const cart = useCartStore()
    cart.items = [{ pid: 1, name: 'Plan', billingcycle: PricingCycle.Monthly, cycleLabel: 'Monthly', amount: 5, currency: 'USD' }]

    await useAuthStore().endLocalSession()

    expect(cart.items).toEqual([])
  })
})

describe('useAuthStore.endLocalSession', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    logoutMock.mockReset()
  })

  it('clears the session cookies and the signed-in account without leaving the page', async () => {
    logoutMock.mockResolvedValue(undefined)
    const store = useAuthStore()
    store.user = { id: 1, email: 'ada@example.com' } as never

    await store.endLocalSession()

    expect(logoutMock).toHaveBeenCalledTimes(1)
    expect(store.user).toBeNull()
  })

  it('forgets the plugin menu and page of the person whose session ended', async () => {
    logoutMock.mockResolvedValue(undefined)
    const { usePluginsStore } = await import('~/stores/plugins')
    const plugins = usePluginsStore()
    plugins.menu = [{ label: 'My sites', icon: null, url: '/client/apps/acme-sites/sites' }]
    plugins.uiLoaded = true

    await useAuthStore().endLocalSession()

    expect(plugins.menu).toEqual([])
    expect(plugins.uiLoaded).toBe(false)
  })

  it('still ends the session on this side when the clear is refused', async () => {
    logoutMock.mockRejectedValue(new Error('offline'))
    const store = useAuthStore()
    store.user = { id: 1, email: 'ada@example.com' } as never

    await store.endLocalSession()

    expect(store.user).toBeNull()
  })
})
