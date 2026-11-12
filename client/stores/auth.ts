/**
 * Pinia store for who is signed in.
 *
 * Auth strategy:
 * - `portal_session`: httpOnly cookie holding only an opaque session id, set server-side after
 *   login; the tokens it names stay on the server (`server/utils/portal-session.ts`)
 * - `authed`: plain cookie (value "1"), readable by JS and middleware on both sides
 *
 * A setup store rather than an options store because {@link isLoggedIn} has to read a cookie
 * ref, and `useCookie` must be called once inside the Nuxt context rather than per getter
 * evaluation. Pinia hydrates the state across SSR the same way `useState` did, so there is no
 * cross-request leakage and no extra fetch on the client.
 *
 * URLs and transport belong to {@link useAuthApi}; this store owns only the state.
 *
 * @module stores/auth
 */

import { CLIENT_LOGIN_PATH } from '~/constants/clientRoutes'
import { defineStore } from 'pinia'
import { useAuthApi } from '~/composables/apis/useAuthApi'
import { useCartStore } from '~/stores/cart'
import { useClientStore } from '~/stores/client'
import { usePluginsStore } from '~/stores/plugins'
import type { ClientUser } from '~/types/clientuser'
import type { EmailVerifiedStatus } from '~/types/emailverifiedstatus'
import type { LoginResult } from '~/types/loginresult'
import type { RegisterPayload } from '~/types/registerpayload'
import { ActionOutcome } from '~/types/actionoutcome'
import { EmailConfirmationStatus } from '~/types/emailconfirmationstatus'
import { InviteOutcome } from '~/types/inviteoutcome'
import { SignInOutcome } from '~/types/signinoutcome'
import { PortalErrorCode, apiErrorCode, apiErrorMessage, apiFieldErrors } from '~/lib/errorHandler'
import { translate } from '~/utils/translate'
import { asClientUser } from '~/utils/asClientUser'
import { AuthMode } from '~/types/authmode'
import { SessionCookie } from '~/constants/sessionCookie'
import { clearPluginChatDrafts } from '~/composables/usePluginChatDraft'

/**
 * Central store for the signed-in identity.
 *
 * @returns The auth state and the actions that change it.
 */
export const useAuthStore = defineStore('auth', () => {
  const api = useAuthApi()

  /** The signed-in account, or null when nobody is signed in or the profile is not loaded yet. */
  const user = ref<ClientUser | null>(null)

  /**
   * Readable auth flag cookie. Set to "1" on login, deleted on logout.
   * Not httpOnly so middleware and client JS can both read it.
   */
  const authedCookie = useCookie<string | null>(SessionCookie.Authed, { readonly: true })

  /** True when the user is logged in (checks the readable flag cookie). */
  const isLoggedIn = computed(() => !!(authedCookie.value || user.value))

  /**
   * Fetch and cache current user data from the server.
   * Safe to call multiple times: skips if user is already loaded.
   *
   * Delegates to {@link useClientStore} rather than calling `/client/me` itself: both stores
   * want the same record, and `apiFetch` is `$fetch`-based and dedupes nothing, so two
   * callers would ask for the identity twice per page load. The dedupe lives one layer down,
   * in the store that owns the record.
   *
   * @returns Nothing; a failure leaves {@link user} null rather than throwing: the client
   * store keeps the reason (or, for an identity with no client record, its own flag).
   */
  const fetchUser = async (): Promise<void> => {
    if (user.value) {
      return
    }
    const clientStore = useClientStore()
    await clientStore.fetchUser()
    user.value = clientStore.user
  }

  /**
   * Log in with email + password.
   *
   * @param email - Account email.
   * @param password - Account password.
   * @returns The second-factor challenge when TOTP is on, no cookies are set and {@link user}
   * is left alone in that case, otherwise the signed-in account.
   * @throws Whatever the API composable throws when the credentials are rejected.
   */
  const login = async (email: string, password: string): Promise<LoginResult> => {
    const data = await api.login(email, password)
    // The `in` check is the whole discriminant: `twoFactorRequired` is typed as the literal
    // `true`, so its presence is what tells the two answers apart. Reading the property as well
    // (`&& data.twoFactorRequired`) made the condition a conjunction, which TypeScript cannot
    // use to narrow the *else* branch: `user.value = data` below was then assigning a
    // `LoginResult`, challenge included, into a `ClientUser | null` ref.
    if ('twoFactorRequired' in data) {
      return data
    }
    user.value = asClientUser(data)
    return data
  }

  /** The sentence for the last refused sign-in; empty when there is none to show. */
  const error = ref('')

  /**
   * Field-level messages of the last refused form post (sign-in, registration or password reset), keyed by camelCase field name (`UiField` renders
   * `fieldErrors[name]`); empty when the refusal was not about fields or nothing was refused.
   */
  const fieldErrors = ref<Record<string, string>>({})

  /** Whether the address is confirmed; null until known or when the credential carries no answer. */
  const emailVerified = ref<boolean | null>(null)

  /** The sentence for the last resend attempt, the backend's on a refusal; empty when none. */
  const resendMessage = ref('')

  /** How the last resend attempt ended; null before any. */
  const resendOutcome = ref<ActionOutcome | null>(null)

  /** Whether a resend request is in flight. */
  const resending = ref(false)

  /**
   * Loads whether the signed-in address is confirmed into {@link emailVerified}.
   *
   * @returns Nothing; a failure leaves the value null, so no banner is shown for an unknown state.
   */
  const loadEmailVerified = async (): Promise<void> => {
    try {
      const status: EmailVerifiedStatus = await api.fetchEmailVerified()
      emailVerified.value = status.verified
    } catch {
      emailVerified.value = null
    }
  }

  /**
   * Asks for the verification mail again and keeps the sentence to show.
   *
   * @returns How the attempt ended; {@link resendMessage} holds the sentence either way.
   */
  const resendVerification = async (): Promise<ActionOutcome> => {
    resendMessage.value = ''
    resending.value = true
    try {
      await api.resendVerification()
      resendMessage.value = translate('client.verifyEmail.sent')
      resendOutcome.value = ActionOutcome.Succeeded
    } catch (e: unknown) {
      resendMessage.value = apiErrorMessage(e)
      resendOutcome.value = ActionOutcome.Failed
    } finally {
      resending.value = false
    }
    return resendOutcome.value
  }

  /** Whether a sign-in request is in flight. */
  const loading = ref(false)

  /**
   * The token a password sign-in answered with when the account has a second factor; empty while
   * no challenge is open. The sign-in page shows the code step for as long as this is set.
   */
  const twoFactorToken = ref('')

  /** Bumped whenever the code step is abandoned, so a late answer can tell it is stale. */
  let challengeEpoch = 0

  /**
   * Sign in for a form: runs {@link login} and keeps the failure as state instead of throwing.
   *
   * The store owns {@link error} and {@link loading} so a page only renders them and branches
   * on the returned outcome.
   *
   * @param email - Account email.
   * @param password - Account password.
   * @returns How the attempt ended; on {@link SignInOutcome.Failed} {@link error} holds the
   * sentence to show, and on {@link SignInOutcome.TwoFactorRequired} {@link twoFactorToken} holds
   * the challenge the code step finishes with {@link confirmTwoFactor}.
   */
  const signIn = async (email: string, password: string): Promise<SignInOutcome> => {
    error.value = ''
    fieldErrors.value = {}
    twoFactorToken.value = ''
    loading.value = true
    try {
      const result = await login(email, password)
      if ('twoFactorRequired' in result && result.twoFactorRequired) {
        twoFactorToken.value = result.pendingToken
        return SignInOutcome.TwoFactorRequired
      }
      return SignInOutcome.SignedIn
    } catch (e: unknown) {
      error.value = apiErrorMessage(e)
      fieldErrors.value = apiFieldErrors(e)
      return SignInOutcome.Failed
    } finally {
      loading.value = false
    }
  }

  /** Where the email-confirmation attempt stands; drives the landing page's three panels. */
  const confirmStatus = ref<EmailConfirmationStatus>(EmailConfirmationStatus.Pending)

  /** The sentence for a refused email confirmation; empty while pending or on success. */
  const confirmError = ref('')

  /**
   * Confirms an email address from the link's `email` and `token`.
   *
   * A link with nothing in it is the one guard allowed before a request is possible; every
   * other refusal is worded by the API.
   *
   * @param email - Address from the link, empty when missing.
   * @param token - Confirmation token from the link, empty when missing.
   * @returns The resulting {@link EmailConfirmationStatus}; on `Failed`, {@link confirmError}
   * holds the sentence to show.
   */
  const confirmEmail = async (email: string, token: string): Promise<EmailConfirmationStatus> => {
    confirmStatus.value = EmailConfirmationStatus.Pending
    confirmError.value = ''
    if (!email || !token) {
      confirmError.value = translate('client.confirmEmail.invalidLink')
      confirmStatus.value = EmailConfirmationStatus.Failed
      return confirmStatus.value
    }
    try {
      await api.confirmEmail(email, token)
      confirmStatus.value = EmailConfirmationStatus.Confirmed
    } catch (e: unknown) {
      confirmError.value = apiErrorMessage(e)
      confirmStatus.value = EmailConfirmationStatus.Failed
    }
    return confirmStatus.value
  }

  /** The sentence for a refused password-reset request; empty when there is none to show. */
  const resetRequestError = ref('')

  /** Whether a password-reset request is in flight. */
  const resetRequesting = ref(false)

  /**
   * Asks for a password-reset mail, keeping a failure as state instead of throwing.
   *
   * @param email - Address to send the reset link to.
   * @returns How the request ended; on `Failed`, {@link resetRequestError} holds the sentence.
   */
  const requestPasswordReset = async (email: string): Promise<ActionOutcome> => {
    resetRequestError.value = ''
    fieldErrors.value = {}
    resetRequesting.value = true
    try {
      await api.requestPasswordReset(email)
      return ActionOutcome.Succeeded
    } catch (e: unknown) {
      resetRequestError.value = apiErrorMessage(e)
      fieldErrors.value = apiFieldErrors(e)
      return ActionOutcome.Failed
    } finally {
      resetRequesting.value = false
    }
  }

  /** How the last invitation attempt ended; null before the first one. */
  const inviteOutcome = ref<InviteOutcome | null>(null)

  /** The API's sentence for the last refused invitation; empty when there is none. */
  const inviteError = ref('')

  /** Whether an accept-invitation request is in flight. */
  const inviting = ref(false)

  /**
   * Accepts a client invitation for the signed-in account, keeping a refusal as state.
   *
   * Branches on the API's code, never its sentence: `INVITE_SIGN_IN_REQUIRED` is not a failure
   * but the cue for the page to offer the sign-in step.
   *
   * @param token - Invitation token from the link.
   * @returns How the attempt ended; also kept in {@link inviteOutcome}.
   */
  const acceptInvite = async (token: string): Promise<InviteOutcome> => {
    inviteError.value = ''
    inviting.value = true
    try {
      await api.acceptInvite(token)
      inviteOutcome.value = InviteOutcome.Accepted
    } catch (e: unknown) {
      inviteError.value = apiErrorMessage(e)
      inviteOutcome.value = apiErrorCode(e) === PortalErrorCode.InviteSignInRequired
        ? InviteOutcome.SignInRequired
        : InviteOutcome.Failed
    } finally {
      inviting.value = false
    }
    return inviteOutcome.value
  }

  /**
   * Forgets how the last invitation attempt ended.
   *
   * The outcome lives in the store, which outlives the page: a visitor sent to sign in and back
   * to the same link by a router navigation would otherwise land on the sign-in step again
   * instead of the accept form.
   */
  const clearInvite = (): void => {
    inviteOutcome.value = null
    inviteError.value = ''
  }

  /**
   * Complete 2FA login with a TOTP code.
   * Sets auth cookies server-side and updates local user state.
   *
   * @param pendingToken - Token the challenge answered with.
   * @param code - TOTP code the visitor read off their authenticator.
   * @returns The signed-in account.
   * @throws Whatever the API composable throws when the code is rejected.
   */
  const loginWithTwoFactor = async (pendingToken: string, code: string): Promise<ClientUser> => {
    const data = await api.loginWithTwoFactor(pendingToken, code)
    user.value = asClientUser(data)
    return data
  }

  /**
   * Finishes a sign-in that stopped at the second factor, for the code step's form: runs
   * {@link loginWithTwoFactor} with the open challenge and keeps a refusal as state instead of
   * throwing.
   *
   * @param code - The 6-digit code the visitor read off their authenticator.
   * @returns {@link SignInOutcome.SignedIn} when the code was accepted, otherwise
   * {@link SignInOutcome.Failed} with the sentence in {@link error}; the challenge stays open so
   * the visitor can retype the code.
   */
  const confirmTwoFactor = async (code: string): Promise<SignInOutcome> => {
    error.value = ''
    loading.value = true
    const challenge = challengeEpoch
    try {
      await loginWithTwoFactor(twoFactorToken.value, code)
      twoFactorToken.value = ''
      return SignInOutcome.SignedIn
    } catch (e: unknown) {
      // The visitor left the code step while this was in flight: its refusal belongs to an
      // abandoned challenge and must not appear on the password form.
      if (challenge === challengeEpoch) {
        error.value = apiErrorMessage(e)
      }
      return SignInOutcome.Failed
    } finally {
      // An abandoned attempt must not clear the flag of a newer one (or of the password form).
      if (challenge === challengeEpoch) {
        loading.value = false
      }
    }
  }

  /**
   * Abandons the open second-factor challenge and returns the sign-in page to the password form.
   * An attempt still in flight is detached: the flag it held is released here, so the password
   * form's submit button is not left disabled behind a request nobody is waiting for.
   */
  const cancelTwoFactor = (): void => {
    challengeEpoch += 1
    loading.value = false
    twoFactorToken.value = ''
    error.value = ''
  }

  /**
   * Log out the current user.
   * Clears both cookies server-side and resets local state.
   * Mode-aware: SSO logout hits the SSO endsession endpoint; local logout invalidates the refresh token.
   *
   * @returns Nothing.
   * @throws Whatever the API composable throws; local state is only cleared on success, as before.
   */
  const logout = async (): Promise<void> => {
    const config = useRuntimeConfig()
    const authMode = config.public.authMode as string

    // First, so the SSO sign-out below, which leaves the page, cannot skip it: the drafts carry no account.
    clearPluginChatDrafts()

    if (authMode === AuthMode.Sso) {
      // SSO logout: clears cookies and redirects to SSO endsession
      await api.ssoLogout()
    } else {
      // Local logout: clears cookies and invalidates refresh token
      await api.logout()
    }
    user.value = null
    // Local mode leaves the browser on the same SPA instance, so the client store's cached
    // profile, services and invoices would otherwise outlive the session and greet whoever
    // signs in next. The cart is the same kind of leftover: its lines belong to the person who
    // chose them.
    useClientStore().reset()
    usePluginsStore().reset()
    useCartStore().clear()
  }

  /**
   * Ends this browser's session without leaving the page: clears the session cookies and every
   * cached profile, list and flag. For a page that has to stay up after the server already ended
   * the account's sessions (a confirmed address change, two-factor switched off) and for
   * {@link signOut}.
   *
   * The reset is unconditional: a failed clear must still end the session on this side rather
   * than leave a header that claims the visitor is still signed in.
   *
   * @returns Nothing.
   */
  const endLocalSession = async (): Promise<void> => {
    try {
      await api.logout()
    } catch { /* the session ends on this side either way */ }
    user.value = null
    useClientStore().reset()
    usePluginsStore().reset()
    useCartStore().clear()
    // An ended session takes the half-written chat messages with it: the drafts carry no account.
    clearPluginChatDrafts()
  }

  /**
   * Sign the visitor out of this product and, under SSO, out of the platform.
   *
   * Distinct from {@link logout}, which clears the local cookies with a `fetch`
   * and nothing more. That is not enough in `sso` mode: the end-session request
   * it triggers is a cross-origin redirect the browser follows *without* the
   * SSO's own cookies, so the identity provider keeps its session and the next
   * "Sign in" signs the same person straight back in without asking. The end
   * session endpoint has to be reached as a top-level navigation.
   *
   * Local cookies are cleared first and the reset is unconditional: a failed
   * clear must still end the session on this side rather than leave a header
   * that claims the visitor is still signed in.
   *
   * @returns Nothing; in SSO mode the browser leaves the page before it resolves.
   */
  const signOut = async (): Promise<void> => {
    const config = useRuntimeConfig()

    await endLocalSession()

    if ((config.public.authMode as string) !== AuthMode.Sso) {
      await navigateTo(CLIENT_LOGIN_PATH)
      return
    }

    const ssoPublicUrl = config.public.ssoPublicUrl as string | undefined
    // Without an SSO address there is no end-session endpoint to build; leave through the local sign-in.
    if (!ssoPublicUrl) {
      await navigateTo(CLIENT_LOGIN_PATH)
      return
    }
    const returnTo = encodeURIComponent(window.location.origin)
    window.location.href = `${ssoPublicUrl}/connect/endsession?post_logout_redirect_uri=${returnTo}`
  }

  /** The sentence for a refused registration; empty when there is none to show. */
  const registerError = ref('')

  /** Whether a registration request is in flight. */
  const registering = ref(false)

  /**
   * Register a new client account, keeping a refusal as state instead of throwing.
   * Does not log the user in: the page sends them to sign in after.
   *
   * @param payload - The registration fields.
   * @returns How the request ended; on `Failed`, {@link registerError} holds the sentence and
   * {@link fieldErrors} the per-field messages.
   */
  const register = async (payload: RegisterPayload): Promise<ActionOutcome> => {
    registerError.value = ''
    fieldErrors.value = {}
    registering.value = true
    try {
      await api.register(payload)
      return ActionOutcome.Succeeded
    } catch (e: unknown) {
      registerError.value = apiErrorMessage(e)
      fieldErrors.value = apiFieldErrors(e)
      return ActionOutcome.Failed
    } finally {
      registering.value = false
    }
  }

  /** The sentence for a refused password reset; empty when there is none to show. */
  const resetError = ref('')

  /** Whether a password-reset request is in flight. */
  const resetting = ref(false)

  /**
   * Sets a new password from a reset link, keeping a refusal as state instead of throwing.
   *
   * @param email - Address from the link.
   * @param token - Reset token from the link.
   * @param newPassword - The password to set.
   * @returns How the request ended; on `Failed`, {@link resetError} holds the sentence and
   * {@link fieldErrors} the per-field messages.
   */
  const resetPassword = async (email: string, token: string, newPassword: string): Promise<ActionOutcome> => {
    resetError.value = ''
    fieldErrors.value = {}
    resetting.value = true
    try {
      await api.resetPassword(email, token, newPassword)
      return ActionOutcome.Succeeded
    } catch (e: unknown) {
      resetError.value = apiErrorMessage(e)
      fieldErrors.value = apiFieldErrors(e)
      return ActionOutcome.Failed
    } finally {
      resetting.value = false
    }
  }

  /**
   * Clear the signed-in identity without calling the server.
   * Used by the auth-guard plugin when a 401 proves the session is already gone.
   *
   * @returns Nothing.
   */
  const reset = (): void => {
    user.value = null
    useClientStore().reset()
    usePluginsStore().reset()
  }

  return {
    user,
    isLoggedIn,
    fetchUser,
    error,
    fieldErrors,
    loading,
    login,
    signIn,
    confirmStatus,
    confirmError,
    confirmEmail,
    emailVerified,
    resendMessage,
    resendOutcome,
    resending,
    loadEmailVerified,
    resendVerification,
    resetRequestError,
    resetRequesting,
    requestPasswordReset,
    inviteOutcome,
    inviteError,
    inviting,
    acceptInvite,
    clearInvite,
    loginWithTwoFactor,
    twoFactorToken,
    confirmTwoFactor,
    cancelTwoFactor,
    logout,
    endLocalSession,
    signOut,
    registerError,
    registering,
    register,
    resetError,
    resetting,
    resetPassword,
    reset
  }
})
