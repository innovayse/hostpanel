import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { redirectToLogin, logoutSession } from '../../../composables/useApi'
import { useAuthApi, type AuthMode } from '../../../composables/apis/useAuthApi'
import { authErrorMessage } from '../../../utils/authErrorMessages'

/**
 * What a sign-in attempt ended in. The store reports an expected failure as a return
 * value rather than throwing — the page decides what to render from it.
 */
export type SignInOutcome = 'done' | 'totp' | 'failed'

/**
 * Pinia store managing admin authentication state, in both deployment shapes.
 *
 * Under `AUTH_MODE=sso` authentication happens against Innovayse SSO, but no longer in
 * this browser: the Hostpanel API performs the OIDC exchange and holds the tokens, and
 * this store only tracks the resulting session by calling /auth/me — roles are assigned
 * locally in Hostpanel and cannot be read off any SSO token anyway. The PKCE flow, the
 * callback view and the refresh loop that used to live beside this store are gone with
 * the tokens they existed to manage.
 *
 * Under `AUTH_MODE=local` — the standalone, open-source path — there is no SSO to
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

  /** True while nobody holds the Admin role, so first-run bootstrap is outstanding. */
  const setupRequired = ref(false)

  /**
   * True when claiming Admin also needs the setup token this installation printed to
   * its log. Answered by the API, never inferred from {@link mode}: it is the server
   * that decides whether the gate applies, and a screen that guessed would either hide
   * a field the request is refused without or ask an SSO operator for a token that does
   * not exist.
   */
  const setupTokenRequired = ref(false)

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
   * slot in {@link useApi} — it is not a credential for the API, only for the second
   * half of this one sign-in.
   */
  const pendingToken = ref<string | null>(null)

  /** True when a user session is active. */
  const isAuthenticated = computed(() => user.value !== null)

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
      setupTokenRequired.value = setupResult.tokenRequired
    } catch (cause) {
      // Without the mode there is no correct control to draw, so this failure is
      // surfaced rather than defaulted — guessing 'sso' here is what produced a
      // sign-in button with nothing behind it in the first place.
      error.value = authErrorMessage(cause)
    }
  }

  /** Sends the browser to sign in via the SSO; the SSO returns to the API's own callback. */
  const login = (): void => {
    redirectToLogin()
  }

  /**
   * Signs in with email and password against a local-mode deployment.
   *
   * @param email - The operator's email address.
   * @param password - The operator's password.
   * @returns 'done' when a session now exists, 'totp' when an authenticator code is
   * still needed, 'failed' when the API rejected the attempt — in which case
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
        error.value = 'The server accepted the sign-in but returned no session.'
        return 'failed'
      }

      // Nothing is stored here. The API set the session cookie on the response this
      // call just read, so the very next request carries it.
      await fetchMe()
      // fetchMe clears the user on any failure, so this also catches a session the API
      // will not accept back — signalling success on it would strand the operator.
      if (!isAuthenticated.value) {
        error.value = 'Signed in, but the session was not accepted. Try again.'
        return 'failed'
      }
      return 'done'
    } catch (cause) {
      error.value = authErrorMessage(cause)
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
    if (pendingToken.value === null) {
      error.value = 'That sign-in attempt has expired. Start again.'
      return 'failed'
    }

    loading.value = true
    error.value = null
    try {
      await api.twoFactorLogin(pendingToken.value, code)
      await fetchMe()
      if (!isAuthenticated.value) {
        error.value = 'Signed in, but the session was not accepted. Try again.'
        return 'failed'
      }
      pendingToken.value = null
      return 'done'
    } catch (cause) {
      error.value = authErrorMessage(cause)
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
   * Registration is the step the admin panel never had: on a genuinely fresh local-mode
   * deployment there are no accounts at all, so there is nobody to sign in as and
   * therefore nobody who can claim the Admin role. Nothing is validated here — password
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
      error.value = authErrorMessage(cause)
      return false
    } finally {
      loading.value = false
    }
  }

  /**
   * Claims the Admin role for the signed-in account — the first-run bootstrap.
   *
   * Only succeeds while nobody holds Admin; a second caller gets a 409 whose body
   * says so, and that body is what the operator is shown. Under local mode it also
   * needs the setup token the installation printed to its log, which is what stops
   * whoever registers first on a publicly reachable box from owning it.
   *
   * @param setupToken - The token from the server log, when {@link setupTokenRequired}.
   * @returns True when the role was granted, false otherwise with {@link error} set.
   */
  const claimAdminRole = async (setupToken?: string): Promise<boolean> => {
    loading.value = true
    error.value = null
    try {
      await api.claimAdmin(setupToken)
      setupRequired.value = false
      // Roles changed server-side, so the copy this store holds is now stale.
      await fetchMe()
      return true
    } catch (cause) {
      error.value = authErrorMessage(cause)
      return false
    } finally {
      loading.value = false
    }
  }

  /**
   * Loads the current user from the API. There is no token to check first — whether a
   * session exists is a question only the server can answer now, in either mode.
   *
   * @returns Promise that resolves when the fetch completes; clears state on failure.
   */
  const fetchMe = async (): Promise<void> => {
    try {
      const data = await api.fetchMe()
      user.value = { email: data.email, roles: data.roles }
      emailVerified.value = data.emailVerified
    } catch {
      user.value = null
      emailVerified.value = null
    }
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

    if (mode.value === 'local') {
      // Failure is swallowed on purpose, and only here: the local state above is
      // already cleared, so the operator is signed out of this app whatever the server
      // says, and there is no screen left to show a message on. An expired session
      // answering 401 must not look like a sign-out that did not happen.
      await api.localLogout().catch(() => undefined)
      return
    }

    await logoutSession()
  }

  return {
    user,
    emailVerified,
    mode,
    setupRequired,
    setupTokenRequired,
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
    fetchMe,
  }
})
