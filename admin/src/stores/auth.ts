/**
 * The auth store: the signed-in user, the sign-in mode, the local and SSO sign-in steps (including
 * two-factor and first-account setup), email verification and sign-out.
 */

import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { useAuthApi } from '@/composables/apis/useAuthApi'
import { AuthMode } from '@/types/auth'
import type { SignInResult } from '@/types/auth'
import { errorSentence } from '@/lib/errorHandler'
import { runAction } from '@/lib/runAction'
import { translate } from '@/lib/translate'
import { getRuntimeConfig } from '@/lib/runtimeConfig'

/** Query parameter the SSO end-session endpoint reads the address to send the browser back to. */
const POST_LOGOUT_PARAM = 'post_logout_redirect_uri'

/**
 * What a sign-in attempt ended in. The store reports an expected failure as a return
 * value rather than throwing: the page decides what to render from it.
 */
export type SignInOutcome = SignInResult

/**
 * Pinia store managing admin authentication state, in both deployment shapes.
 *
 * Under `AUTH_MODE=sso` authentication happens against single sign-on, but not in this
 * browser: the Hostpanel API performs the OIDC exchange and holds the tokens, and this
 * store only tracks the resulting session by calling /auth/me. Roles are assigned locally
 * in Hostpanel and cannot be read off any SSO token anyway.
 *
 * Under `AUTH_MODE=local` (the standalone, open-source path) there is no SSO to
 * redirect to. The store posts credentials to the API and drives the TOTP step when the
 * account has one. It holds no credential either way: the API writes the local-mode
 * token into an httpOnly cookie, so this store tracks a session it cannot read, exactly
 * as it does under SSO. Which of the two applies is not guessed: {@link loadMode} asks
 * the API.
 *
 * It also owns first-run bootstrap, which on a standalone install is three calls and not
 * one: create the first account, sign in with it, then claim the Admin role with the
 * setup token the installation printed to its log.
 */
export const useAuthStore = defineStore('auth', () => {
  const api = useAuthApi()

  /** Currently authenticated admin user, null when unauthenticated. */
  const user = ref<{ email: string; roles: string[] } | null>(null)

  /** Whether the current user's email has been verified. Null means not yet checked. */
  const emailVerified = ref<boolean | null>(null)

  /** How this deployment signs people in. Null until {@link loadMode} has answered. */
  const mode = ref<AuthMode | null>(null)

  /**
   * True once the server has said whether a session exists, signed in or not.
   *
   * {@link user} alone cannot tell "signed out" from "not asked yet", so without this flag the guard
   * would ask again on every navigation of a signed-out visitor. Set by {@link fetchMe} on an
   * answer; cleared when the session may have changed behind this store's back.
   */
  const sessionChecked = ref(false)

  /** True while nobody holds the Admin role, so first-run bootstrap is outstanding. */
  const setupRequired = ref(false)

  /** True while a sign-in or bootstrap request is in flight. */
  const loading = ref(false)

  /**
   * The failure to show the operator, always taken from the API's own response body.
   * Null when there is nothing to report.
   */
  const error = ref<string | null>(null)

  /**
   * Continuation token for the TOTP step, held only in memory for the life of the
   * attempt. It deliberately never reaches the URL, session storage, or the token
   * slot in {@link useApi}: it is not a credential for the API, only for the second
   * half of this one sign-in.
   */
  const pendingToken = ref<string | null>(null)

  /** True when a user session is active. */
  const isAuthenticated = computed(() => user.value !== null)

  /**
   * True when the signed-in account holds the Admin role. Any customer can sign in through the
   * shared sign-on, so a session alone says nothing about access to this panel.
   */
  const isAdmin = computed(() => user.value?.roles.includes('Admin') === true)

  /**
   * Identifies the signed-in account and its roles as one string, or null when signed out. It
   * changes on sign-out, on another account and on any role change, so stores whose data depends
   * on who is asking can watch it.
   */
  const accountKey = computed<string | null>(() =>
    user.value === null ? null : `${user.value.email}|${user.value.roles.join(',')}`,
  )

  /** True when the sign-in is mid-flight and waiting on an authenticator code. */
  const awaitingTwoFactor = computed(() => pendingToken.value !== null)

  /**
   * Loads the deployment's auth mode and whether first-run setup is outstanding.
   *
   * Both are anonymous endpoints and both are needed before the login page can draw
   * anything, so they go out together rather than in series.
   *
   * @returns Promise resolving once both answers are in; sets {@link error} on failure.
   */
  const loadMode = async (): Promise<void> => {
    error.value = null
    try {
      const [modeResult, setupResult] = await Promise.all([
        api.fetchMode(),
        api.fetchSetupRequired(),
      ])
      mode.value = modeResult.mode
      setupRequired.value = setupResult.required
    } catch (cause) {
      // Without the mode there is no correct control to draw, so this failure is
      // surfaced rather than defaulted: guessing 'sso' here is what produced a
      // sign-in button with nothing behind it in the first place.
      error.value = errorSentence(cause)
    }
  }

  /** Sends the browser to sign in via the SSO; the SSO returns to the API's own callback. */
  const login = (): void => {
    location.href = api.ssoLoginUrl(location.pathname + location.search)
  }

  /**
   * Signs in with email and password against a local-mode deployment.
   *
   * @param email - The operator's email address.
   * @param password - The operator's password.
   * @returns 'done' when a session now exists, 'totp' when an authenticator code is
   * still needed, 'failed' when the API rejected the attempt, in which case
   * {@link error} carries the API's own explanation.
   */
  const signInLocal = async (email: string, password: string): Promise<SignInOutcome> => {
    loading.value = true
    error.value = null
    pendingToken.value = null
    try {
      const result = await api.localLogin(email, password)

      if (result.twoFactorRequired === true && result.pendingToken) {
        pendingToken.value = result.pendingToken
        return 'totp'
      }

      if (!result.accessToken) {
        // A 200 with neither a token nor a 2FA continuation is a contract the client
        // cannot act on. Reporting success here would sign nobody in and say it worked.
        error.value = translate('errors.signInLoop')
        return 'failed'
      }

      // Nothing is stored here. The API set the session cookie on the response this
      // call just read, so the very next request carries it.
      await fetchMe()
      // fetchMe clears the user on any failure, so this also catches a session the API
      // will not accept back: signalling success on it would strand the operator.
      if (!isAuthenticated.value) {
        error.value = translate('errors.signInLoop')
        return 'failed'
      }
      return 'done'
    } catch (cause) {
      error.value = errorSentence(cause)
      return 'failed'
    } finally {
      loading.value = false
    }
  }

  /**
   * Completes a sign-in that asked for a second factor.
   *
   * @param code - The digits the operator's authenticator app currently shows.
   * @returns 'done' on success, 'failed' otherwise with {@link error} set.
   */
  const submitTwoFactor = async (code: string): Promise<SignInOutcome> => {
    loading.value = true
    error.value = null
    try {
      // With no pending token the request goes out empty and the API refuses it in its own
      // words; the client does not write an "attempt expired" sentence of its own.
      await api.twoFactorLogin(pendingToken.value ?? '', code)
      await fetchMe()
      if (!isAuthenticated.value) {
        error.value = translate('errors.signInLoop')
        return 'failed'
      }
      pendingToken.value = null
      return 'done'
    } catch (cause) {
      error.value = errorSentence(cause)
      return 'failed'
    } finally {
      loading.value = false
    }
  }

  /** Abandons a half-finished 2FA step and returns the page to the credential form. */
  const cancelTwoFactor = (): void => {
    pendingToken.value = null
    error.value = null
  }

  /**
   * Creates the very first account on a standalone install.
   *
   * On a genuinely fresh local-mode deployment there are no accounts at all, so there is nobody to sign in as and
   * therefore nobody who can claim the Admin role. Nothing is validated here: password
   * rules, duplicate addresses and malformed input are all the server's answer to give,
   * and it gives them in the operator's own language.
   *
   * @param firstName - Given name.
   * @param lastName - Family name.
   * @param email - Address the account will sign in with.
   * @param password - Chosen password.
   * @returns True when the account was created, false otherwise with {@link error} set.
   */
  const registerFirstAccount = async (
    firstName: string,
    lastName: string,
    email: string,
    password: string,
  ): Promise<boolean> => {
    loading.value = true
    error.value = null
    try {
      await api.register(email, password, firstName, lastName)
      return true
    } catch (cause) {
      error.value = errorSentence(cause)
      return false
    } finally {
      loading.value = false
    }
  }

  /**
   * Claims the Admin role for the signed-in account: the first-run bootstrap.
   *
   * Only succeeds while nobody holds Admin; a second caller gets a 409 whose body
   * says so, and that body is what the operator is shown. It also needs the setup
   * token the installation printed to its log, which is what stops whoever registers
   * first on a publicly reachable box from owning it.
   *
   * @param setupToken - The token from the server log.
   * @returns True when the role was granted, false otherwise with {@link error} set.
   */
  const claimAdminRole = async (setupToken: string): Promise<boolean> => {
    loading.value = true
    error.value = null
    try {
      await api.claimAdmin(setupToken)
      setupRequired.value = false
      // Roles changed server-side, so the copy this store holds is now stale.
      await fetchMe()
      return true
    } catch (cause) {
      error.value = errorSentence(cause)
      return false
    } finally {
      loading.value = false
    }
  }

  /**
   * Loads the current user from the API. There is no token to check first: whether a
   * session exists is a question only the server can answer, in either mode.
   *
   * @returns Promise that resolves when the fetch completes; clears state on failure.
   */
  const fetchMe = async (): Promise<void> => {
    try {
      const data = await api.fetchMe()
      // Signed in or signed out, this is the server's answer, so the guard need not ask again.
      sessionChecked.value = true
      if (data === null) {
        // 401 is the expected answer of the session probe when signed out, not an error.
        user.value = null
        emailVerified.value = null
        return
      }
      user.value = { email: data.email, roles: data.roles }
      emailVerified.value = data.emailVerified
    } catch {
      // No answer (offline, a 5xx): the session state is still unknown, so the flag is left as it
      // was and the next navigation asks again rather than treating the visitor as signed out for good.
      user.value = null
      emailVerified.value = null
    }
  }

  /**
   * Asks the server whether a session exists, unless it has already answered since the page loaded
   * or since the session last changed.
   *
   * @returns Promise that resolves once the session state is known, or the probe has failed.
   */
  const checkSession = async (): Promise<void> => {
    if (sessionChecked.value) {
      return
    }
    await fetchMe()
  }

  /**
   * Confirms the address behind a mailed verification link.
   *
   * On success the local session state is reset so the next navigation re-asks the
   * server (a session, if any, is unaffected server-side).
   *
   * @param token - Verification token from the link.
   * @param email - Address being verified.
   * @returns True when verified; false when refused (see `error`).
   */
  const verifyEmail = async (token: string, email: string): Promise<boolean> => {
    const result = await runAction(error, () =>
      api.verifyEmail(token, email),
    )
    if (result.ok) {
      user.value = null
      emailVerified.value = null
      sessionChecked.value = false
    }
    return result.ok
  }

  /**
   * Asks the API to mail the signed-in account its verification link again.
   *
   * @returns True when the API accepted the request; false when refused (see `error`).
   */
  const resendVerification = async (): Promise<boolean> => {
    const result = await runAction(error, () => api.resendVerification())
    return result.ok
  }

  /**
   * Ends the session.
   *
   * In local mode the credential is an httpOnly cookie this page cannot read and
   * therefore cannot delete, so signing out is a request rather than a local erase. In
   * SSO mode the server session and the SSO session both have to go, so nothing
   * survives to revive it.
   *
   * @returns Promise resolving once the session has been ended.
   */
  const logout = async (): Promise<void> => {
    user.value = null
    emailVerified.value = null
    pendingToken.value = null
    // Signed out is now the known state: asking the server again would only earn a 401, or, when
    // the sign-out request below fails, revive the session the operator just ended.
    sessionChecked.value = true

    if (mode.value === AuthMode.Local) {
      // Failure is swallowed on purpose, and only here: the local state above is
      // already cleared, so the operator is signed out of this app whatever the server
      // says, and there is no screen left to show a message on. An expired session
      // answering 401 must not look like a sign-out that did not happen.
      await api.localLogout().catch(() => undefined)
      return
    }

    // Tell the SSO where to send the browser afterwards. Without this the end-session endpoint
    // has nowhere to go and answers 200 with an empty body, which is a blank white page: the
    // sign-out worked, but it looked like the app had died. The address must be one registered
    // for this client on the SSO side, which the origin plus the deployment's base path is.
    const endSessionUrl = await api.endSessionUrl()
    if (!endSessionUrl) {
      location.href = '/'
      return
    }
    const url = new URL(endSessionUrl)
    url.searchParams.set(POST_LOGOUT_PARAM, new URL(getRuntimeConfig().basePath, location.origin).href)
    location.href = url.toString()
  }

  return {
    user,
    isAdmin,
    accountKey,
    emailVerified,
    sessionChecked,
    mode,
    setupRequired,
    loading,
    error,
    isAuthenticated,
    awaitingTwoFactor,
    loadMode,
    login,
    signInLocal,
    submitTwoFactor,
    cancelTwoFactor,
    registerFirstAccount,
    claimAdminRole,
    logout,
    verifyEmail,
    resendVerification,
    fetchMe,
    checkSession,
  }
})
