/**
 * What a sign-in answers with when the account has TOTP switched on.
 *
 * No cookies are set at this point: the account is identified but not signed in until the
 * code is accepted, so the caller must hold the token and post it back with the code.
 */
export interface TwoFactorChallenge {
  /** Always true — it is the discriminant that tells this apart from a completed sign-in. */
  twoFactorRequired: true
  /** Short-lived token that carries the half-finished sign-in to the code step. */
  pendingToken: string
}
