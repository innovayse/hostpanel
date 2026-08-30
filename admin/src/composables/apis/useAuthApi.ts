/**
 * One function per endpoint on the API's auth surface. No state lives here — no
 * `ref`, no caching — so this file reads as a list of what the backend offers.
 *
 * Callers are the auth store, never a `.vue` file.
 */

import { useApi } from '../useApi'

/** Which sign-in mechanism a deployment runs. */
export type AuthMode = 'sso' | 'local'

/** Body of `GET /api/auth/mode`. */
export interface AuthModeResponse {
  /** 'local' when this deployment owns its own accounts, 'sso' when Innovayse SSO does. */
  mode: AuthMode
}

/** Body of `GET /api/auth/setup-required`. */
export interface SetupRequiredResponse {
  /** True while nobody holds the Admin role yet. */
  required: boolean
  /**
   * True when claiming the Admin role also needs the setup token this installation
   * printed to its log. Local-mode deployments only — under SSO the accounts belong to
   * the sign-on service and no token is ever issued.
   */
  tokenRequired: boolean
}

/** Body of a successful `POST /api/auth/register`. */
export interface RegisterResponse {
  /** Identifier of the account that was just created. */
  userId: string
}

/** Body of `GET /api/auth/me`. */
export interface MeResponse {
  /** Email address of the signed-in account. */
  email: string
  /** Roles granted locally in Hostpanel — never read off an SSO token. */
  roles: string[]
  /** Whether the identity provider reports the address as confirmed. */
  emailVerified: boolean
}

/**
 * Body of a successful `POST /api/auth/login`. The endpoint answers one of two
 * shapes, so both sets of fields are optional and the caller discriminates on
 * `twoFactorRequired`.
 */
export interface LocalLoginResponse {
  /** Bearer JWT, present when the account has no second factor enabled. */
  accessToken?: string
  /** Lifetime of {@link accessToken} in seconds. */
  expiresIn?: number
  /** True when a TOTP code is still needed before a token is issued. */
  twoFactorRequired?: boolean
  /**
   * Continuation token for the TOTP step. Stays in the response body and in memory —
   * it must never reach the URL, where it would be bookmarkable and land in history.
   */
  pendingToken?: string
}

/** Body of a successful `POST /api/auth/2fa-login`. */
export interface TwoFactorLoginResponse {
  /** Bearer JWT for the now fully authenticated session. */
  accessToken: string
  /** Lifetime of {@link accessToken} in seconds. */
  expiresIn: number
}

/**
 * Endpoint functions for `/api/auth/*`.
 *
 * Every function rejects with an `ApiError` carrying the response body on failure;
 * turning that into words is `utils/authErrorMessages.ts`'s job, and deciding what to
 * render is the store's.
 *
 * @returns One typed function per auth endpoint the admin panel uses.
 */
export const useAuthApi = () => {
  const { request } = useApi()

  /**
   * Asks how this deployment signs people in. Anonymous, and has to be — a client
   * must read it before it can authenticate.
   *
   * @returns The deployment's auth mode.
   */
  const fetchMode = (): Promise<AuthModeResponse> => request<AuthModeResponse>('/auth/mode')

  /**
   * Asks whether first-run bootstrap is still outstanding, i.e. whether the Admin
   * role is unclaimed.
   *
   * @returns Whether setup is required.
   */
  const fetchSetupRequired = (): Promise<SetupRequiredResponse> =>
    request<SetupRequiredResponse>('/auth/setup-required')

  /**
   * Loads the signed-in account. Whether a session exists is a question only the
   * server can answer, in either mode.
   *
   * @returns Email, locally granted roles, and email-verified flag.
   */
  const fetchMe = (): Promise<MeResponse> => request<MeResponse>('/auth/me')

  /**
   * Signs in with email and password. Local mode only — under SSO this same path is
   * a redirect endpoint and the POST answers 404.
   *
   * @param email - The operator's email address.
   * @param password - The operator's password.
   * @returns Either a bearer token, or a 2FA continuation.
   */
  const localLogin = (email: string, password: string): Promise<LocalLoginResponse> =>
    request<LocalLoginResponse>('/auth/login', {
      method: 'POST',
      body: JSON.stringify({ email, password }),
    })

  /**
   * Completes a sign-in that asked for a second factor.
   *
   * @param pendingToken - The continuation token from {@link localLogin}, held in memory.
   * @param code - The digits the operator's authenticator app currently shows.
   * @returns A bearer token for the completed session.
   */
  const twoFactorLogin = (pendingToken: string, code: string): Promise<TwoFactorLoginResponse> =>
    request<TwoFactorLoginResponse>('/auth/2fa-login', {
      method: 'POST',
      body: JSON.stringify({ pendingToken, code }),
    })

  /**
   * Creates a local account. Local mode only — under SSO this answers 404, because the
   * accounts belong to the sign-on service.
   *
   * @param email - Address the account signs in with.
   * @param password - Chosen password. Its rules are the server's; a rejection comes back
   * as the server's own sentence rather than being pre-empted here.
   * @param firstName - Given name.
   * @param lastName - Family name.
   * @returns The identifier of the created account.
   */
  const register = (
    email: string,
    password: string,
    firstName: string,
    lastName: string,
  ): Promise<RegisterResponse> =>
    request<RegisterResponse>('/auth/register', {
      method: 'POST',
      body: JSON.stringify({ email, password, firstName, lastName }),
    })

  /**
   * Claims the Admin role for the caller. Requires an authenticated session and only
   * succeeds while nobody holds Admin — a second caller gets 409.
   *
   * @param setupToken - The token this installation printed to its log, required in local
   * mode and ignored under SSO. Omitted when {@link SetupRequiredResponse.tokenRequired}
   * said none is wanted.
   * @returns Confirmation that the role was granted.
   */
  const claimAdmin = (setupToken?: string): Promise<{ success: boolean }> =>
    request<{ success: boolean }>('/auth/setup', {
      method: 'POST',
      body: JSON.stringify({ setupToken: setupToken ?? null }),
    })

  /**
   * Ends a local-mode session by asking the API to clear the httpOnly session cookie.
   * The page cannot read that cookie, so it cannot delete it either — signing out has to
   * be a request.
   *
   * The verb carries the distinction, the way it already does for login. Under SSO the
   * shared auth package maps `POST /api/auth/logout`; this controller serves `DELETE` on
   * the same path, so the router tells them apart by method and neither shadows the other.
   * Sending `POST` here would reach the SSO's endpoint under one mode and nothing at all
   * under the other.
   *
   * @returns Confirmation that the cookie was cleared.
   */
  const localLogout = (): Promise<{ success: boolean }> =>
    request<{ success: boolean }>('/auth/logout', { method: 'DELETE' })

  return {
    fetchMode,
    fetchSetupRequired,
    fetchMe,
    localLogin,
    twoFactorLogin,
    register,
    claimAdmin,
    localLogout,
  }
}
