/**
 * Endpoints under `/api/portal/auth` — signing in, signing out, and everything that changes
 * which credentials work.
 *
 * Stateless by contract: no `ref`, no `computed`, no caching. Who is signed in is
 * `stores/auth.ts`'s business; this file only knows which URL answers which question.
 *
 * @module composables/apis/useAuthApi
 */

import { apiFetch } from '~/composables/useApi'
import type { ClientUser } from '~/types/clientuser'
import type { LoginResult } from '~/types/loginresult'
import type { RegisterPayload } from '~/types/registerpayload'

/**
 * The `/api/portal/auth` surface, one function per endpoint.
 *
 * @returns The authentication endpoint functions.
 */
export function useAuthApi() {
  /**
   * Signs in with email and password.
   *
   * @param email - Account email.
   * @param password - Account password.
   * @returns The signed-in account, or the second-factor challenge when TOTP is switched on.
   * @throws Whatever `apiFetch` throws — a rejected credential is an error here, not a value.
   */
  const login = (email: string, password: string): Promise<LoginResult> =>
    apiFetch<LoginResult>('/api/portal/auth/login', {
      method: 'POST',
      body: { email, password }
    })

  /**
   * Finishes a sign-in that stopped at the second factor.
   *
   * @param pendingToken - Token the challenge answered with.
   * @param code - TOTP code the visitor read off their authenticator.
   * @returns The signed-in account.
   * @throws Whatever `apiFetch` throws — a wrong code included.
   */
  const loginWithTwoFactor = (pendingToken: string, code: string): Promise<ClientUser> =>
    apiFetch<ClientUser>('/api/portal/auth/2fa-login', {
      method: 'POST',
      body: { pendingToken, code }
    })

  /**
   * Creates a client account. Does not sign the new account in.
   *
   * @param payload - The registration fields.
   * @returns Whatever the endpoint answers with; the caller sends the visitor to sign in.
   * @throws Whatever `apiFetch` throws.
   */
  const register = (payload: RegisterPayload): Promise<unknown> =>
    apiFetch('/api/portal/auth/register', { method: 'POST', body: payload })

  /**
   * Clears this product's session cookies and invalidates the refresh token.
   *
   * @returns Nothing.
   * @throws Whatever `apiFetch` throws.
   */
  const logout = (): Promise<unknown> =>
    apiFetch('/api/portal/auth/logout', { method: 'POST' })

  /**
   * Clears this product's session cookies in SSO mode.
   *
   * This is only half of an SSO sign-out: the identity provider's own session survives it,
   * because the end-session endpoint has to be reached as a top-level navigation rather than
   * as a fetch. The store performs that navigation after calling this.
   *
   * @returns Nothing.
   * @throws Whatever `apiFetch` throws.
   */
  const ssoLogout = (): Promise<unknown> =>
    apiFetch('/api/portal/auth/sso/logout', { method: 'POST' })

  /**
   * Asks for a password-reset mail.
   *
   * @param email - Address to send the reset link to.
   * @returns Nothing.
   * @throws Whatever `apiFetch` throws.
   */
  const requestPasswordReset = (email: string): Promise<unknown> =>
    apiFetch('/api/portal/auth/forgot-password', { method: 'POST', body: { email } })

  /**
   * Sets a new password from a reset link.
   *
   * @param email - Address the link was issued for.
   * @param token - Reset token from the link.
   * @param newPassword - The password to set.
   * @returns Nothing.
   * @throws Whatever `apiFetch` throws.
   */
  const resetPassword = (email: string, token: string, newPassword: string): Promise<unknown> =>
    apiFetch('/api/portal/auth/reset-password', {
      method: 'POST',
      body: { email, token, newPassword }
    })

  /**
   * Confirms an email address from a confirmation link.
   *
   * @param email - Address being confirmed.
   * @param token - Confirmation token from the link.
   * @returns Nothing.
   * @throws Whatever `apiFetch` throws.
   */
  const confirmEmail = (email: string, token: string): Promise<unknown> =>
    apiFetch('/api/portal/auth/confirm-email', { method: 'POST', body: { email, token } })

  /**
   * Accepts a client invitation on behalf of whoever is signed in.
   *
   * No password travels with it. The backend command carries a token and nothing else and
   * resolves the accepting account from the credential — a field naming the subject would
   * let whoever holds the token link a different account. The page's password fields went
   * with it: nothing in this product sets a password from an invitation token.
   *
   * @param token - Invitation token from the link.
   * @returns Nothing the caller reads; success is the absence of a throw.
   * @throws Whatever `apiFetch` throws — an expired or already-accepted invitation included,
   *         and a `403` carrying `INVITE_SIGN_IN_REQUIRED` when nobody is signed in yet.
   */
  const acceptInvite = (token: string): Promise<unknown> =>
    apiFetch('/api/portal/auth/accept-invite', { method: 'POST', body: { token } })

  /**
   * Starts TOTP enrolment, returning the secret and its QR payload.
   *
   * @returns The shared secret and the `otpauth:` URI an authenticator scans.
   * @throws Whatever `apiFetch` throws.
   */
  const setupTwoFactor = (): Promise<{ secret: string, qrCodeUri: string }> =>
    apiFetch<{ secret: string, qrCodeUri: string }>('/api/portal/auth/2fa-setup', { method: 'POST' })

  /**
   * Switches TOTP on, confirming enrolment with a first code.
   *
   * @param code - TOTP code proving the authenticator was set up.
   * @returns Whatever the endpoint answers with.
   * @throws Whatever `apiFetch` throws.
   */
  const enableTwoFactor = (code: string): Promise<unknown> =>
    apiFetch('/api/portal/auth/2fa-enable', { method: 'POST', body: { code } })

  /**
   * Switches TOTP off.
   *
   * @param code - Current TOTP code, proving the request came from the enrolled device.
   * @returns Whatever the endpoint answers with.
   * @throws Whatever `apiFetch` throws.
   */
  const disableTwoFactor = (code: string): Promise<unknown> =>
    apiFetch('/api/portal/auth/2fa-disable', { method: 'POST', body: { code } })

  return {
    login,
    loginWithTwoFactor,
    register,
    logout,
    ssoLogout,
    requestPasswordReset,
    resetPassword,
    confirmEmail,
    acceptInvite,
    setupTwoFactor,
    enableTwoFactor,
    disableTwoFactor
  }
}
