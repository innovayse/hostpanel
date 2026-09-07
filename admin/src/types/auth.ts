/**
 * Wire shapes for `/api/auth/*`.
 *
 * These describe what the API answers with, not what the panel renders; the store maps
 * them onto its own state. They live here rather than beside the endpoint functions so a
 * component or store can name a response type without importing the API surface that
 * produces it.
 */

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
