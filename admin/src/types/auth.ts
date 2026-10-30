/**
 * Wire shapes for `/api/auth/*`.
 *
 * These describe what the API answers with, not what the panel renders; the store maps
 * them onto its own state. They live here rather than beside the endpoint functions so a
 * component or store can name a response type without importing the API surface that
 * produces it.
 */

/** Closed set of sign-in mechanisms a deployment can run. */
export const AuthMode = {
  /** Accounts belong to the sign-on service. */
  Sso: 'sso',
  /** The deployment owns its accounts (standalone install). */
  Local: 'local',
} as const

/** Which sign-in mechanism a deployment runs. */
export type AuthMode = (typeof AuthMode)[keyof typeof AuthMode]

/** Closed set of results of a sign-in attempt, as the auth store reports them. */
export const SignInResult = {
  /** A session now exists. */
  Done: 'done',
  /** An authenticator code is still needed. */
  Totp: 'totp',
  /** The API rejected the attempt; the sentence is in the store's `error`. */
  Failed: 'failed',
} as const

/** Body of `GET /api/auth/mode`. */
export interface AuthModeResponse {
  /** 'local' when this deployment owns its own accounts, 'sso' when single sign-on does. */
  mode: AuthMode
}

/** Body of `GET /api/auth/setup-required`. */
export interface SetupRequiredResponse {
  /**
   * True while nobody holds the Admin role yet. Claiming it always needs the setup token
   * this installation printed to its log.
   */
  required: boolean
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
  /** Roles granted locally in Hostpanel, never read off an SSO token. */
  roles: string[]
  /** Whether the identity provider reports the address as confirmed. */
  emailVerified: boolean
  /** The client account the caller acts for; null when they act for none. Optional because the admin never reads it. */
  clientId?: number | null
  /** The account in another system the client is linked to; null when there is none. Optional because the admin never reads it. */
  externalAccount?: { provider: string, id: string } | null
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
   * Continuation token for the TOTP step. Stays in the response body and in memory:
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

/** One of the {@link SignInResult} values. */
export type SignInResult = (typeof SignInResult)[keyof typeof SignInResult]

/** Closed set of how far a sign-out reaches. */
export const LogoutScope = {
  /** Ends this browser's session only. */
  This: 'this',
  /** Ends every session of the account. */
  All: 'all',
} as const

/** One of the {@link LogoutScope} values. */
export type LogoutScope = (typeof LogoutScope)[keyof typeof LogoutScope]
