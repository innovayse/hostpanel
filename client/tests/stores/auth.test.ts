/**
 * Tests for the actions in `stores/auth.ts`, through the real auth API composable, the real client, cart and
 * plugins stores and the transport against a stub server.
 *
 * The store, not the login page, owns the refusal sentence and the in-flight flag, so these
 * assertions pin the contract the page renders from. Nothing of our own is replaced: the stub plays the BFF.
 *
 * @module tests/stores/auth.test
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { requestsTo, routeTable } from '../helpers/stubRoutes'
import type { StubRouteTable } from '../helpers/stubRoutes'
import type { StubReply, StubServer } from '../helpers/stubServer'
import { startTransport } from '../helpers/stubTransport'
import { PortalErrorCode } from '~/constants/portalErrorCode'
import { useAuthStore } from '~/stores/auth'
import { useCartStore } from '~/stores/cart'
import { usePluginsStore } from '~/stores/plugins'
import { ActionOutcome } from '~/types/actionoutcome'
import { AuthMode } from '~/types/authmode'
import { EmailConfirmationStatus } from '~/types/emailconfirmationstatus'
import { HttpMethod } from '~/types/httpmethod'
import { InviteOutcome } from '~/types/inviteoutcome'
import { PricingCycle } from '~/types/pricingcycle'
import { SignInOutcome } from '~/types/signinoutcome'
import { hasCheckoutSave, rememberCheckoutSave } from '~/utils/pendingCheckoutSave'

/** The stub playing the BFF. */
let stub: StubServer

/** The replies the stub gives, by `METHOD /path`; each test fills the ones it needs. */
let table: StubRouteTable

/** Spy standing in for the auto-imported navigation helper. */
const navigate = vi.fn()

/** The password sign-in. */
const LOGIN_PATH = '/api/portal/auth/login'

/** The password sign-in, as a table key. */
const LOGIN_ROUTE = `${HttpMethod.Post} ${LOGIN_PATH}`

/** The second-factor sign-in. */
const TWO_FACTOR_PATH = '/api/portal/auth/2fa-login'

/** The sign-out request. */
const LOGOUT_PATH = '/api/portal/auth/logout'

/** The sign-out request, as a table key. */
const LOGOUT_ROUTE = `${HttpMethod.Post} ${LOGOUT_PATH}`

/** The challenge the sign-in answers with when the account has a second factor. */
const CHALLENGE: StubReply = { body: { twoFactorRequired: true, pendingToken: 't' } }

/** A line to put in a cart. */
const PLAN = { pid: 1, name: 'Plan', billingcycle: PricingCycle.Monthly, cycleLabel: 'Monthly', amount: 5, currency: 'USD' }

/**
 * The JSON body of the first request the stub received for a path.
 *
 * @param path - The path, without query.
 * @returns The parsed body.
 */
const bodyTo = (path: string): unknown => JSON.parse(requestsTo(stub.requests, path)[0]?.body ?? '')

/**
 * Stands in for the runtime config the sign-out reads its auth mode from.
 *
 * @param public_ - The public half of the config.
 */
const configure = (public_: Record<string, string>): void => {
  vi.stubGlobal('useRuntimeConfig', () => ({ public: public_ }))
}

beforeEach(async () => {
  setActivePinia(createPinia())
  navigate.mockReset()
  vi.stubGlobal('navigateTo', navigate)
  window.sessionStorage.clear()
  table = { [LOGOUT_ROUTE]: { body: {} } }
  stub = await startTransport(routeTable(table))
})

afterEach(async () => {
  vi.unstubAllGlobals()
  await stub.close()
})

describe('useAuthStore.signIn', () => {
  it('reports SignedIn, stores the account and leaves no error', async () => {
    table[LOGIN_ROUTE] = { body: { id: 1, email: 'ada@example.com' } }
    const store = useAuthStore()

    const outcome = await store.signIn('ada@example.com', 'pw')

    expect(outcome).toBe(SignInOutcome.SignedIn)
    expect(bodyTo(LOGIN_PATH)).toEqual({ email: 'ada@example.com', password: 'pw' })
    expect(store.error).toBe('')
    expect(store.loading).toBe(false)
    expect(store.user).toMatchObject({ id: 1 })
  })

  it('does not store the { success: true } acknowledgement as the account, so the profile is fetched later', async () => {
    table[LOGIN_ROUTE] = { body: { success: true } }
    const store = useAuthStore()

    const outcome = await store.signIn('ada@example.com', 'pw')

    expect(outcome).toBe(SignInOutcome.SignedIn)
    expect(store.user).toBeNull()
  })

  it('keeps the API sentence as error and reports Failed when the credentials are refused', async () => {
    table[LOGIN_ROUTE] = { status: 401, body: { detail: 'Wrong email or password.' } }
    const store = useAuthStore()

    const outcome = await store.signIn('ada@example.com', 'bad')

    expect(outcome).toBe(SignInOutcome.Failed)
    expect(store.error).toBe('Wrong email or password.')
    expect(store.loading).toBe(false)
    expect(store.user).toBeNull()
  })

  it('keeps the field messages of a refused sign-in and clears them on the next attempt', async () => {
    table[LOGIN_ROUTE] = { status: 400, body: { type: 'VALIDATION_ERROR', detail: 'Invalid.', errors: { email: ['Email is invalid.'] } } }
    const store = useAuthStore()

    expect(await store.signIn('nope', 'x')).toBe(SignInOutcome.Failed)
    expect(store.fieldErrors).toEqual({ email: 'Email is invalid.' })

    table[LOGIN_ROUTE] = { body: { id: 1 } }
    await store.signIn('ada@example.com', 'ok')
    expect(store.fieldErrors).toEqual({})
  })

  it('reports TwoFactorRequired, keeps the challenge and leaves the account unset', async () => {
    table[LOGIN_ROUTE] = CHALLENGE
    const store = useAuthStore()

    const outcome = await store.signIn('ada@example.com', 'pw')

    expect(outcome).toBe(SignInOutcome.TwoFactorRequired)
    expect(store.twoFactorToken).toBe('t')
    expect(store.error).toBe('')
    expect(store.user).toBeNull()
  })

  it('finishes a second-factor sign-in with the open challenge and the typed code', async () => {
    table[LOGIN_ROUTE] = CHALLENGE
    table[`${HttpMethod.Post} ${TWO_FACTOR_PATH}`] = { body: { id: 7 } }
    const store = useAuthStore()
    await store.signIn('ada@example.com', 'pw')

    const outcome = await store.confirmTwoFactor('123456')

    expect(bodyTo(TWO_FACTOR_PATH)).toEqual({ pendingToken: 't', code: '123456' })
    expect(outcome).toBe(SignInOutcome.SignedIn)
    expect(store.twoFactorToken).toBe('')
    expect(store.user).toEqual({ id: 7 })
  })

  it('keeps the challenge open and shows the sentence when the code is refused', async () => {
    table[LOGIN_ROUTE] = CHALLENGE
    table[`${HttpMethod.Post} ${TWO_FACTOR_PATH}`] = { status: 400, body: { detail: 'Invalid code.' } }
    const store = useAuthStore()
    await store.signIn('ada@example.com', 'pw')

    const outcome = await store.confirmTwoFactor('000000')

    expect(outcome).toBe(SignInOutcome.Failed)
    expect(store.error).toBe('Invalid code.')
    expect(store.twoFactorToken).toBe('t')
    expect(store.loading).toBe(false)
  })

  it('drops the challenge when the visitor goes back to the password form', async () => {
    table[LOGIN_ROUTE] = CHALLENGE
    const store = useAuthStore()
    await store.signIn('ada@example.com', 'pw')

    store.cancelTwoFactor()

    expect(store.twoFactorToken).toBe('')
  })

  it('clears a previous error when the next attempt starts', async () => {
    table[LOGIN_ROUTE] = { status: 400, body: { detail: 'No.' } }
    const store = useAuthStore()

    await store.signIn('a', 'b')
    table[LOGIN_ROUTE] = { body: { id: 2 } }
    await store.signIn('a', 'b')

    expect(store.error).toBe('')
  })
})

describe('useAuthStore.confirmEmail', () => {
  /** The email confirmation, as a table key. */
  const CONFIRM_ROUTE = `${HttpMethod.Post} /api/portal/auth/confirm-email`

  it('fails without a request when the link is missing its email or token', async () => {
    const store = useAuthStore()

    const status = await store.confirmEmail('', 'tok')

    expect(status).toBe(EmailConfirmationStatus.Failed)
    expect(store.confirmError).not.toBe('')
    expect(stub.requests).toHaveLength(0)
  })

  it('reports Confirmed when the API accepts the link', async () => {
    table[CONFIRM_ROUTE] = { body: {} }
    const store = useAuthStore()

    const status = await store.confirmEmail('ada@example.com', 'tok')

    expect(status).toBe(EmailConfirmationStatus.Confirmed)
    expect(bodyTo('/api/portal/auth/confirm-email')).toEqual({ email: 'ada@example.com', token: 'tok' })
    expect(store.confirmStatus).toBe(EmailConfirmationStatus.Confirmed)
    expect(store.confirmError).toBe('')
  })

  it('keeps the API sentence and reports Failed when the link is refused', async () => {
    table[CONFIRM_ROUTE] = { status: 400, body: { detail: 'Link expired.' } }
    const store = useAuthStore()

    const status = await store.confirmEmail('ada@example.com', 'tok')

    expect(status).toBe(EmailConfirmationStatus.Failed)
    expect(store.confirmError).toBe('Link expired.')
  })
})

describe('useAuthStore email verification', () => {
  /** The read of the confirmed flag, as a table key. */
  const VERIFIED_ROUTE = `${HttpMethod.Get} /api/portal/auth/email-verified`
  /** The verification mail resend, as a table key. */
  const RESEND_ROUTE = `${HttpMethod.Post} /api/portal/auth/resend-verification`

  it('keeps the three-valued status the API answers with', async () => {
    table[VERIFIED_ROUTE] = { body: { verified: false } }
    const store = useAuthStore()

    await store.loadEmailVerified()

    expect(store.emailVerified).toBe(false)
  })

  it('leaves the status unknown when the read fails', async () => {
    table[VERIFIED_ROUTE] = { status: 400 }
    const store = useAuthStore()

    await store.loadEmailVerified()

    expect(store.emailVerified).toBeNull()
  })

  it('reports Succeeded and a local sentence when the mail is sent', async () => {
    table[RESEND_ROUTE] = { body: {} }
    const store = useAuthStore()

    const outcome = await store.resendVerification()

    expect(outcome).toBe(ActionOutcome.Succeeded)
    expect(store.resendMessage).not.toBe('')
    expect(store.resending).toBe(false)
  })

  it('keeps the API sentence and reports Failed when the resend is refused', async () => {
    table[RESEND_ROUTE] = { status: 429, body: { detail: 'Too many requests.' } }
    const store = useAuthStore()

    const outcome = await store.resendVerification()

    expect(outcome).toBe(ActionOutcome.Failed)
    expect(store.resendMessage).toBe('Too many requests.')
  })
})

describe('useAuthStore.requestPasswordReset', () => {
  /** The password-reset request, as a table key. */
  const FORGOT_ROUTE = `${HttpMethod.Post} /api/portal/auth/forgot-password`

  it('reports Succeeded and leaves no error', async () => {
    table[FORGOT_ROUTE] = { body: {} }
    const store = useAuthStore()

    const outcome = await store.requestPasswordReset('ada@example.com')

    expect(outcome).toBe(ActionOutcome.Succeeded)
    expect(bodyTo('/api/portal/auth/forgot-password')).toEqual({ email: 'ada@example.com' })
    expect(store.resetRequestError).toBe('')
    expect(store.resetRequesting).toBe(false)
  })

  it('keeps the API sentence and reports Failed when the request is refused', async () => {
    table[FORGOT_ROUTE] = { status: 429, body: { detail: 'Too many requests.' } }
    const store = useAuthStore()

    const outcome = await store.requestPasswordReset('ada@example.com')

    expect(outcome).toBe(ActionOutcome.Failed)
    expect(store.resetRequestError).toBe('Too many requests.')
    expect(store.resetRequesting).toBe(false)
  })
})

describe('useAuthStore.acceptInvite', () => {
  /** The invitation acceptance, as a table key. */
  const INVITE_ROUTE = `${HttpMethod.Post} /api/portal/auth/accept-invite`

  it('reports Accepted when the API accepts the token', async () => {
    table[INVITE_ROUTE] = { body: {} }
    const store = useAuthStore()

    const outcome = await store.acceptInvite('tok')

    expect(outcome).toBe(InviteOutcome.Accepted)
    expect(bodyTo('/api/portal/auth/accept-invite')).toEqual({ token: 'tok' })
    expect(store.inviteError).toBe('')
    expect(store.inviting).toBe(false)
  })

  it('reports SignInRequired when the refusal carries the sign-in-required code', async () => {
    table[INVITE_ROUTE] = { status: 403, body: { type: PortalErrorCode.InviteSignInRequired } }
    const store = useAuthStore()

    const outcome = await store.acceptInvite('tok')

    expect(outcome).toBe(InviteOutcome.SignInRequired)
    expect(store.inviteOutcome).toBe(InviteOutcome.SignInRequired)
  })

  it('keeps the API sentence and reports Failed for any other refusal', async () => {
    table[INVITE_ROUTE] = { status: 400, body: { type: 'INVITE_EXPIRED', detail: 'Invitation expired.' } }
    const store = useAuthStore()

    const outcome = await store.acceptInvite('tok')

    expect(outcome).toBe(InviteOutcome.Failed)
    expect(store.inviteError).toBe('Invitation expired.')
  })

  it('forgets the last outcome and sentence on clearInvite', async () => {
    table[INVITE_ROUTE] = { status: 403, body: { type: PortalErrorCode.InviteSignInRequired, detail: 'Sign in first.' } }
    const store = useAuthStore()
    await store.acceptInvite('tok')

    store.clearInvite()

    expect(store.inviteOutcome).toBeNull()
    expect(store.inviteError).toBe('')
  })
})

describe('useAuthStore.register', () => {
  /** The registration, as a table key. */
  const REGISTER_ROUTE = `${HttpMethod.Post} /api/portal/auth/register`

  it('reports Succeeded and leaves no error', async () => {
    table[REGISTER_ROUTE] = { body: {} }
    const store = useAuthStore()
    const payload = { firstname: 'A', lastname: 'B', email: 'a@b.c', password: 'pw' }

    const outcome = await store.register(payload)

    expect(outcome).toBe(ActionOutcome.Succeeded)
    expect(bodyTo('/api/portal/auth/register')).toEqual(payload)
    expect(store.registerError).toBe('')
    expect(store.registering).toBe(false)
  })

  it('keeps the API sentence and the field messages when the registration is refused', async () => {
    table[REGISTER_ROUTE] = {
      status: 400,
      body: { detail: 'Fix the form.', type: 'VALIDATION_ERROR', errors: { email: ['Email is taken.'] } }
    }
    const store = useAuthStore()

    const outcome = await store.register({ firstname: 'A', lastname: 'B', email: 'a@b.c', password: 'pw' })

    expect(outcome).toBe(ActionOutcome.Failed)
    expect(store.registerError).toBe('Email is taken.')
    expect(store.fieldErrors).toEqual({ email: 'Email is taken.' })
    expect(store.registering).toBe(false)
  })

  it('clears the previous refusal when the next attempt starts', async () => {
    table[REGISTER_ROUTE] = { status: 400, body: { detail: 'No.' } }
    const store = useAuthStore()

    await store.register({ firstname: 'A', lastname: 'B', email: 'a@b.c', password: 'pw' })
    table[REGISTER_ROUTE] = { body: {} }
    await store.register({ firstname: 'A', lastname: 'B', email: 'a@b.c', password: 'pw' })

    expect(store.registerError).toBe('')
    expect(store.fieldErrors).toEqual({})
  })
})

describe('useAuthStore.resetPassword', () => {
  /** The password reset, as a table key. */
  const RESET_ROUTE = `${HttpMethod.Post} /api/portal/auth/reset-password`

  it('sends the link values and the new password, and reports Succeeded', async () => {
    table[RESET_ROUTE] = { body: {} }
    const store = useAuthStore()

    const outcome = await store.resetPassword('a@b.c', 'tok', 'newpass123')

    expect(outcome).toBe(ActionOutcome.Succeeded)
    expect(bodyTo('/api/portal/auth/reset-password')).toEqual({ email: 'a@b.c', token: 'tok', newPassword: 'newpass123' })
    expect(store.resetError).toBe('')
    expect(store.resetting).toBe(false)
  })

  it('keeps the API sentence and the field messages when the reset is refused', async () => {
    table[RESET_ROUTE] = {
      status: 400,
      body: { detail: 'Weak.', type: 'VALIDATION_ERROR', errors: { newPassword: ['Too short.'] } }
    }
    const store = useAuthStore()

    const outcome = await store.resetPassword('a@b.c', 'tok', 'x')

    expect(outcome).toBe(ActionOutcome.Failed)
    expect(store.resetError).toBe('Too short.')
    expect(store.fieldErrors).toEqual({ newPassword: 'Too short.' })
    expect(store.resetting).toBe(false)
  })
})

describe('useAuthStore.signOut', () => {
  it('leaves through the local sign-in page under SSO when no SSO address is configured', async () => {
    configure({ authMode: AuthMode.Sso, ssoPublicUrl: '' })
    const store = useAuthStore()

    await store.signOut()

    expect(requestsTo(stub.requests, LOGOUT_PATH)).toHaveLength(1)
    expect(navigate).toHaveBeenCalledWith('/client/login')
  })

  it('leaves through the local sign-in page in local mode', async () => {
    configure({ authMode: AuthMode.Local, ssoPublicUrl: 'https://sso.test' })
    const store = useAuthStore()

    await store.signOut()

    expect(requestsTo(stub.requests, LOGOUT_PATH)).toHaveLength(1)
    expect(navigate).toHaveBeenCalledWith('/client/login')
  })
})

describe('useAuthStore cart on sign-out', () => {
  beforeEach(() => {
    configure({ authMode: AuthMode.Local })
  })

  it('empties the cart when the client logs out', async () => {
    const cart = useCartStore()
    cart.items = [PLAN]

    await useAuthStore().logout()

    expect(cart.items).toEqual([])
  })

  it('empties the cart when the local session ends', async () => {
    const cart = useCartStore()
    cart.items = [PLAN]

    await useAuthStore().endLocalSession()

    expect(cart.items).toEqual([])
  })
})

describe('useAuthStore.endLocalSession', () => {
  it('clears the session cookies and the signed-in account without leaving the page', async () => {
    const store = useAuthStore()
    store.user = { id: 1, email: 'ada@example.com' } as never

    await store.endLocalSession()

    expect(requestsTo(stub.requests, LOGOUT_PATH)).toHaveLength(1)
    expect(store.user).toBeNull()
    expect(navigate).not.toHaveBeenCalled()
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
    table[LOGOUT_ROUTE] = { status: 503 }
    const store = useAuthStore()
    store.user = { id: 1, email: 'ada@example.com' } as never

    await store.endLocalSession()

    expect(store.user).toBeNull()
  })
})

describe('useAuthStore remembered checkout choices', () => {
  beforeEach(() => {
    configure({ authMode: AuthMode.Local })
  })

  it('removes them when the client logs out', async () => {
    rememberCheckoutSave(42, 'card_gateway', 'ada@example.com')

    await useAuthStore().logout()

    expect(hasCheckoutSave(42)).toBe(false)
  })

  it('removes them when the local session ends', async () => {
    rememberCheckoutSave(42, 'card_gateway', 'ada@example.com')

    await useAuthStore().endLocalSession()

    expect(hasCheckoutSave(42)).toBe(false)
  })

  it('removes them when the identity is reset after a 401', () => {
    rememberCheckoutSave(42, 'card_gateway', 'ada@example.com')

    useAuthStore().reset()

    expect(hasCheckoutSave(42)).toBe(false)
  })
})
