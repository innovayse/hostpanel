/**
 * Tests for the actions in `stores/auth.ts`, through the real auth API composable, the real stores a sign-out resets
 * and the transport, against a stub BFF.
 *
 * The store, not the login page, owns the refusal sentence and the in-flight flag, so these
 * assertions pin the contract the page renders from. Nothing of our own is replaced: the stub plays the BFF and a
 * request's method, path and body are asserted on what reached it. Only the Nuxt framework globals a sign-out reaches
 * (`navigateTo`, `useRuntimeConfig`) are stood in for.
 *
 * @module tests/stores/auth.test
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { AccountEndpoint } from '~/tests/helpers/accountPaths'
import { bodyOf } from '~/tests/helpers/requestBody'
import { readOf, refusal, requestsTo, routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubReply, StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import { CLIENT_LOGIN_PATH } from '~/constants/clientRoutes'
import { useAuthStore } from '~/stores/auth'
import { useCartStore } from '~/stores/cart'
import { usePluginsStore } from '~/stores/plugins'
import { ActionOutcome } from '~/types/actionoutcome'
import { EmailConfirmationStatus } from '~/types/emailconfirmationstatus'
import { HttpMethod } from '~/types/httpmethod'
import { InviteOutcome } from '~/types/inviteoutcome'
import { PricingCycle } from '~/types/pricingcycle'
import { SignInOutcome } from '~/types/signinoutcome'

/** The auth endpoints the store reaches; sign-in and sign-out are in {@link AccountEndpoint}. */
const AuthPath = {
  TwoFactorLogin: '/api/portal/auth/2fa-login',
  Register: '/api/portal/auth/register',
  ConfirmEmail: '/api/portal/auth/confirm-email',
  EmailVerified: '/api/portal/auth/email-verified',
  ResendVerification: '/api/portal/auth/resend-verification',
  ForgotPassword: '/api/portal/auth/forgot-password',
  ResetPassword: '/api/portal/auth/reset-password',
  AcceptInvite: '/api/portal/auth/accept-invite'
} as const

/** The stub playing the BFF. */
let stub: StubServer

/** The replies the stub gives, by `METHOD /path`; each test fills the ones it needs. */
let table: StubRouteTable

/**
 * The key of a write in a stub route table.
 *
 * @param path - The path of the write.
 * @returns The `POST` key for the route table.
 */
const postOf = (path: string): string => `${HttpMethod.Post} ${path}`

/** The key of the sign-in write. */
const LOGIN = postOf(AccountEndpoint.Login)

/** The key of the sign-out write. */
const LOGOUT = postOf(AccountEndpoint.Logout)

/**
 * The reply of a backend that refuses a form with a sentence and per-field messages.
 *
 * @param detail - The sentence.
 * @param errors - The messages, by field.
 * @returns The failure reply, in the shape the backend answers a validation failure with.
 */
const fieldRefusal = (detail: string, errors: Record<string, string[]>): StubReply => ({
  status: 422,
  body: { type: 'VALIDATION_ERROR', detail, errors }
})

beforeEach(async () => {
  setActivePinia(createPinia())
  table = { [LOGOUT]: { body: {} } }
  stub = await startTransport(routeTable(table))
})

afterEach(async () => {
  vi.unstubAllGlobals()
  await stub.close()
})

describe('useAuthStore.signIn', () => {
  it('reports SignedIn, stores the account and leaves no error', async () => {
    table[LOGIN] = { body: { id: 1, email: 'ada@example.com' } }
    const store = useAuthStore()

    const outcome = await store.signIn('ada@example.com', 'pw')

    expect(outcome).toBe(SignInOutcome.SignedIn)
    expect(store.error).toBe('')
    expect(store.loading).toBe(false)
    expect(store.user).toMatchObject({ id: 1 })
    expect(stub.requests).toHaveLength(1)
    expect(stub.requests[0]?.method).toBe(HttpMethod.Post)
    expect(bodyOf(stub.requests[0])).toEqual({ email: 'ada@example.com', password: 'pw' })
  })

  it('does not store the { success: true } acknowledgement as the account, so the profile is fetched later', async () => {
    table[LOGIN] = { body: { success: true } }
    const store = useAuthStore()

    const outcome = await store.signIn('ada@example.com', 'pw')

    expect(outcome).toBe(SignInOutcome.SignedIn)
    expect(store.user).toBeNull()
  })

  it('keeps the API sentence as error and reports Failed when the credentials are refused', async () => {
    table[LOGIN] = refusal('Wrong email or password.')
    const store = useAuthStore()

    const outcome = await store.signIn('ada@example.com', 'bad')

    expect(outcome).toBe(SignInOutcome.Failed)
    expect(store.error).toBe('Wrong email or password.')
    expect(store.loading).toBe(false)
    expect(store.user).toBeNull()
  })

  it('keeps the field messages of a refused sign-in and clears them on the next attempt', async () => {
    table[LOGIN] = fieldRefusal('Invalid.', { email: ['Email is invalid.'] })
    const store = useAuthStore()

    expect(await store.signIn('nope', 'x')).toBe(SignInOutcome.Failed)
    expect(store.fieldErrors).toEqual({ email: 'Email is invalid.' })

    table[LOGIN] = { body: { id: 1 } }
    await store.signIn('ada@example.com', 'ok')
    expect(store.fieldErrors).toEqual({})
  })

  it('reports TwoFactorRequired, keeps the challenge and leaves the account unset', async () => {
    table[LOGIN] = { body: { twoFactorRequired: true, pendingToken: 't' } }
    const store = useAuthStore()

    const outcome = await store.signIn('ada@example.com', 'pw')

    expect(outcome).toBe(SignInOutcome.TwoFactorRequired)
    expect(store.twoFactorToken).toBe('t')
    expect(store.error).toBe('')
    expect(store.user).toBeNull()
  })

  it('finishes a second-factor sign-in with the open challenge and the typed code', async () => {
    table[LOGIN] = { body: { twoFactorRequired: true, pendingToken: 't' } }
    table[postOf(AuthPath.TwoFactorLogin)] = { body: { id: 7 } }
    const store = useAuthStore()
    await store.signIn('ada@example.com', 'pw')

    const outcome = await store.confirmTwoFactor('123456')

    const finished = requestsTo(stub.requests, AuthPath.TwoFactorLogin)
    expect(finished.map(request => request.method)).toEqual([HttpMethod.Post])
    expect(bodyOf(finished[0])).toEqual({ pendingToken: 't', code: '123456' })
    expect(outcome).toBe(SignInOutcome.SignedIn)
    expect(store.twoFactorToken).toBe('')
    expect(store.user).toEqual({ id: 7 })
  })

  it('keeps the challenge open and shows the sentence when the code is refused', async () => {
    table[LOGIN] = { body: { twoFactorRequired: true, pendingToken: 't' } }
    table[postOf(AuthPath.TwoFactorLogin)] = refusal('Invalid code.')
    const store = useAuthStore()
    await store.signIn('ada@example.com', 'pw')

    const outcome = await store.confirmTwoFactor('000000')

    expect(outcome).toBe(SignInOutcome.Failed)
    expect(store.error).toBe('Invalid code.')
    expect(store.twoFactorToken).toBe('t')
    expect(store.loading).toBe(false)
  })

  it('drops the challenge when the visitor goes back to the password form', async () => {
    table[LOGIN] = { body: { twoFactorRequired: true, pendingToken: 't' } }
    const store = useAuthStore()
    await store.signIn('ada@example.com', 'pw')

    store.cancelTwoFactor()

    expect(store.twoFactorToken).toBe('')
  })

  it('clears a previous error when the next attempt starts', async () => {
    table[LOGIN] = refusal('No.')
    const store = useAuthStore()

    await store.signIn('a', 'b')
    expect(store.error).toBe('No.')
    table[LOGIN] = { body: { id: 2 } }
    await store.signIn('a', 'b')

    expect(store.error).toBe('')
  })
})

describe('useAuthStore.confirmEmail', () => {
  it('fails without a request when the link is missing its email or token', async () => {
    const store = useAuthStore()

    const status = await store.confirmEmail('', 'tok')

    expect(status).toBe(EmailConfirmationStatus.Failed)
    expect(store.confirmError).not.toBe('')
    expect(requestsTo(stub.requests, AuthPath.ConfirmEmail)).toHaveLength(0)
  })

  it('reports Confirmed when the API accepts the link', async () => {
    table[postOf(AuthPath.ConfirmEmail)] = { body: {} }
    const store = useAuthStore()

    const status = await store.confirmEmail('ada@example.com', 'tok')

    const confirmed = requestsTo(stub.requests, AuthPath.ConfirmEmail)
    expect(confirmed.map(request => request.method)).toEqual([HttpMethod.Post])
    expect(bodyOf(confirmed[0])).toEqual({ email: 'ada@example.com', token: 'tok' })
    expect(status).toBe(EmailConfirmationStatus.Confirmed)
    expect(store.confirmStatus).toBe(EmailConfirmationStatus.Confirmed)
    expect(store.confirmError).toBe('')
  })

  it('keeps the API sentence and reports Failed when the link is refused', async () => {
    table[postOf(AuthPath.ConfirmEmail)] = refusal('Link expired.')
    const store = useAuthStore()

    const status = await store.confirmEmail('ada@example.com', 'tok')

    expect(status).toBe(EmailConfirmationStatus.Failed)
    expect(store.confirmError).toBe('Link expired.')
  })
})

describe('useAuthStore email verification', () => {
  it('keeps the three-valued status the API answers with', async () => {
    table[readOf(AuthPath.EmailVerified)] = { body: { verified: false } }
    const store = useAuthStore()

    await store.loadEmailVerified()

    expect(store.emailVerified).toBe(false)
  })

  it('leaves the status unknown when the read fails', async () => {
    table[readOf(AuthPath.EmailVerified)] = { status: 503, body: {} }
    const store = useAuthStore()

    await store.loadEmailVerified()

    expect(store.emailVerified).toBeNull()
  })

  it('reports Succeeded and a local sentence when the mail is sent', async () => {
    table[postOf(AuthPath.ResendVerification)] = { body: {} }
    const store = useAuthStore()

    const outcome = await store.resendVerification()

    expect(requestsTo(stub.requests, AuthPath.ResendVerification).map(request => request.method)).toEqual([HttpMethod.Post])
    expect(outcome).toBe(ActionOutcome.Succeeded)
    expect(store.resendMessage).not.toBe('')
    expect(store.resending).toBe(false)
  })

  it('keeps the API sentence and reports Failed when the resend is refused', async () => {
    table[postOf(AuthPath.ResendVerification)] = refusal('Too many requests.')
    const store = useAuthStore()

    const outcome = await store.resendVerification()

    expect(outcome).toBe(ActionOutcome.Failed)
    expect(store.resendMessage).toBe('Too many requests.')
  })
})

describe('useAuthStore.requestPasswordReset', () => {
  it('reports Succeeded and leaves no error', async () => {
    table[postOf(AuthPath.ForgotPassword)] = { body: {} }
    const store = useAuthStore()

    const outcome = await store.requestPasswordReset('ada@example.com')

    const asked = requestsTo(stub.requests, AuthPath.ForgotPassword)
    expect(asked.map(request => request.method)).toEqual([HttpMethod.Post])
    expect(bodyOf(asked[0])).toEqual({ email: 'ada@example.com' })
    expect(outcome).toBe(ActionOutcome.Succeeded)
    expect(store.resetRequestError).toBe('')
    expect(store.resetRequesting).toBe(false)
  })

  it('keeps the API sentence and reports Failed when the request is refused', async () => {
    table[postOf(AuthPath.ForgotPassword)] = refusal('Too many requests.')
    const store = useAuthStore()

    const outcome = await store.requestPasswordReset('ada@example.com')

    expect(outcome).toBe(ActionOutcome.Failed)
    expect(store.resetRequestError).toBe('Too many requests.')
    expect(store.resetRequesting).toBe(false)
  })
})

describe('useAuthStore.acceptInvite', () => {
  it('reports Accepted when the API accepts the token', async () => {
    table[postOf(AuthPath.AcceptInvite)] = { body: {} }
    const store = useAuthStore()

    const outcome = await store.acceptInvite('tok')

    const accepted = requestsTo(stub.requests, AuthPath.AcceptInvite)
    expect(accepted.map(request => request.method)).toEqual([HttpMethod.Post])
    expect(bodyOf(accepted[0])).toEqual({ token: 'tok' })
    expect(outcome).toBe(InviteOutcome.Accepted)
    expect(store.inviteError).toBe('')
    expect(store.inviting).toBe(false)
  })

  it('reports SignInRequired when the refusal carries the sign-in-required code', async () => {
    table[postOf(AuthPath.AcceptInvite)] = { status: 403, body: { type: 'INVITE_SIGN_IN_REQUIRED' } }
    const store = useAuthStore()

    const outcome = await store.acceptInvite('tok')

    expect(outcome).toBe(InviteOutcome.SignInRequired)
    expect(store.inviteOutcome).toBe(InviteOutcome.SignInRequired)
  })

  it('keeps the API sentence and reports Failed for any other refusal', async () => {
    table[postOf(AuthPath.AcceptInvite)] = { status: 410, body: { type: 'INVITE_EXPIRED', detail: 'Invitation expired.' } }
    const store = useAuthStore()

    const outcome = await store.acceptInvite('tok')

    expect(outcome).toBe(InviteOutcome.Failed)
    expect(store.inviteError).toBe('Invitation expired.')
  })

  it('forgets the last outcome and sentence on clearInvite', async () => {
    table[postOf(AuthPath.AcceptInvite)] = { status: 403, body: { type: 'INVITE_SIGN_IN_REQUIRED', detail: 'Sign in first.' } }
    const store = useAuthStore()
    await store.acceptInvite('tok')

    store.clearInvite()

    expect(store.inviteOutcome).toBeNull()
    expect(store.inviteError).toBe('')
  })
})

describe('useAuthStore.register', () => {
  it('reports Succeeded and leaves no error', async () => {
    table[postOf(AuthPath.Register)] = { body: {} }
    const store = useAuthStore()
    const payload = { firstname: 'A', lastname: 'B', email: 'a@b.c', password: 'pw' }

    const outcome = await store.register(payload)

    const registered = requestsTo(stub.requests, AuthPath.Register)
    expect(registered.map(request => request.method)).toEqual([HttpMethod.Post])
    expect(bodyOf(registered[0])).toEqual(payload)
    expect(outcome).toBe(ActionOutcome.Succeeded)
    expect(store.registerError).toBe('')
    expect(store.registering).toBe(false)
  })

  it('keeps the API sentence and the field messages when the registration is refused', async () => {
    table[postOf(AuthPath.Register)] = fieldRefusal('Fix the form.', { email: ['Email is taken.'] })
    const store = useAuthStore()

    const outcome = await store.register({ firstname: 'A', lastname: 'B', email: 'a@b.c', password: 'pw' })

    expect(outcome).toBe(ActionOutcome.Failed)
    expect(store.registerError).toBe('Email is taken.')
    expect(store.fieldErrors).toEqual({ email: 'Email is taken.' })
    expect(store.registering).toBe(false)
  })

  it('clears the previous refusal when the next attempt starts', async () => {
    table[postOf(AuthPath.Register)] = refusal('No.')
    const store = useAuthStore()

    await store.register({ firstname: 'A', lastname: 'B', email: 'a@b.c', password: 'pw' })
    expect(store.registerError).toBe('No.')
    table[postOf(AuthPath.Register)] = { body: {} }
    await store.register({ firstname: 'A', lastname: 'B', email: 'a@b.c', password: 'pw' })

    expect(store.registerError).toBe('')
    expect(store.fieldErrors).toEqual({})
  })
})

describe('useAuthStore.resetPassword', () => {
  it('sends the link values and the new password, and reports Succeeded', async () => {
    table[postOf(AuthPath.ResetPassword)] = { body: {} }
    const store = useAuthStore()

    const outcome = await store.resetPassword('a@b.c', 'tok', 'newpass123')

    const reset = requestsTo(stub.requests, AuthPath.ResetPassword)
    expect(reset.map(request => request.method)).toEqual([HttpMethod.Post])
    expect(bodyOf(reset[0])).toEqual({ email: 'a@b.c', token: 'tok', newPassword: 'newpass123' })
    expect(outcome).toBe(ActionOutcome.Succeeded)
    expect(store.resetError).toBe('')
    expect(store.resetting).toBe(false)
  })

  it('keeps the API sentence and the field messages when the reset is refused', async () => {
    table[postOf(AuthPath.ResetPassword)] = fieldRefusal('Weak.', { newPassword: ['Too short.'] })
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
    navigate.mockReset()
    vi.stubGlobal('navigateTo', navigate)
  })

  it('leaves through the local sign-in page under SSO when no SSO address is configured', async () => {
    vi.stubGlobal('useRuntimeConfig', () => ({ public: { authMode: 'sso', ssoPublicUrl: '' } }))
    const store = useAuthStore()

    await store.signOut()

    expect(requestsTo(stub.requests, AccountEndpoint.Logout)).toHaveLength(1)
    expect(navigate).toHaveBeenCalledWith(CLIENT_LOGIN_PATH)
  })

  it('leaves through the local sign-in page in local mode', async () => {
    vi.stubGlobal('useRuntimeConfig', () => ({ public: { authMode: 'local', ssoPublicUrl: 'https://sso.test' } }))
    const store = useAuthStore()

    await store.signOut()

    expect(requestsTo(stub.requests, AccountEndpoint.Logout)).toHaveLength(1)
    expect(navigate).toHaveBeenCalledWith(CLIENT_LOGIN_PATH)
  })
})

describe('useAuthStore cart on sign-out', () => {
  beforeEach(() => {
    vi.stubGlobal('useRuntimeConfig', () => ({ public: { authMode: 'local' } }))
  })

  it('empties the cart when the client logs out', async () => {
    const cart = useCartStore()
    cart.items = [{ pid: 1, name: 'Plan', billingcycle: PricingCycle.Monthly, cycleLabel: 'Monthly', amount: 5, currency: 'USD' }]

    await useAuthStore().logout()

    expect(requestsTo(stub.requests, AccountEndpoint.Logout)).toHaveLength(1)
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
  it('clears the session cookies and the signed-in account without leaving the page', async () => {
    const store = useAuthStore()
    store.user = { id: 1, email: 'ada@example.com' } as never

    await store.endLocalSession()

    const cleared = requestsTo(stub.requests, AccountEndpoint.Logout)
    expect(cleared.map(request => request.method)).toEqual([HttpMethod.Post])
    expect(store.user).toBeNull()
  })

  it('forgets the plugin menu and page of the person whose session ended', async () => {
    const plugins = usePluginsStore()
    plugins.menu = [{ label: 'My sites', icon: null, url: '/client/apps/acme-sites/sites' }]
    plugins.uiLoaded = true

    await useAuthStore().endLocalSession()

    expect(plugins.menu).toEqual([])
    expect(plugins.uiLoaded).toBe(false)
  })

  it('still ends the session on this side when the clear is refused', async () => {
    table[LOGOUT] = { status: 503, body: {} }
    const store = useAuthStore()
    store.user = { id: 1, email: 'ada@example.com' } as never

    await store.endLocalSession()

    expect(requestsTo(stub.requests, AccountEndpoint.Logout)).toHaveLength(1)
    expect(store.user).toBeNull()
  })
})
