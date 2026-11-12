/**
 * The BFF paths of the account page's endpoints (identity, sign-in address, contacts, team, two-factor,
 * stored payment methods, sign-out), so a test that answers them from a stub BFF route table names an
 * endpoint instead of spelling out the URL.
 *
 * @module tests/helpers/accountPaths
 */

/** The account endpoints that do not take an id. */
export const AccountEndpoint = {
  Me: '/api/portal/client/me',
  EmailChange: '/api/portal/client/email-change',
  Contacts: '/api/portal/client/contacts',
  Users: '/api/portal/client/users',
  InviteUser: '/api/portal/client/users/invite',
  TwoFactorSetup: '/api/portal/auth/2fa-setup',
  TwoFactorEnable: '/api/portal/auth/2fa-enable',
  TwoFactorDisable: '/api/portal/auth/2fa-disable',
  Logout: '/api/portal/auth/logout',
  StoredMethods: '/api/portal/client/stored-payment-methods',
  PaymentMethods: '/api/portal/order/payment-methods',
} as const

/**
 * The path of one sub-contact.
 *
 * @param contactId - The contact id.
 * @returns The BFF path that updates or deletes it.
 */
export const contactPath = (contactId: number): string => `${AccountEndpoint.Contacts}/${contactId}`

/**
 * The path that removes one team member.
 *
 * @param userId - The member's identity user id.
 * @returns The BFF path.
 */
export const userPath = (userId: string): string => `${AccountEndpoint.Users}/${userId}`

/**
 * The path of one stored method's consent.
 *
 * @param consentId - The consent id.
 * @returns The BFF path that revokes it.
 */
export const consentPath = (consentId: number): string => `${AccountEndpoint.StoredMethods}/consents/${consentId}`

/**
 * The path that makes a stored method the default.
 *
 * @param consentId - The consent id.
 * @returns The BFF path.
 */
export const defaultConsentPath = (consentId: number): string => `${consentPath(consentId)}/default`

/**
 * The path that completes a stored-method save session.
 *
 * @param sessionId - The gateway's session id.
 * @returns The BFF path.
 */
export const completeSessionPath = (sessionId: string): string =>
  `${AccountEndpoint.StoredMethods}/sessions/${encodeURIComponent(sessionId)}/complete`
