/**
 * The BFF paths of the account endpoints (identity, services, tickets, sign-in address, contacts,
 * team, two-factor, stored payment methods) and of the sign-in surface (sign-in, sign-out,
 * registration, email confirmation, password reset, invitations), so a test that answers them
 * from a stub BFF route table names an endpoint instead of spelling out the URL.
 *
 * @module tests/helpers/accountPaths
 */

/** The account endpoints that do not take an id. */
export enum AccountEndpoint {
  Me = '/api/portal/client/me',
  Services = '/api/portal/client/services',
  Tickets = '/api/portal/client/tickets',
  EmailChange = '/api/portal/client/email-change',
  Contacts = '/api/portal/client/contacts',
  Emails = '/api/portal/client/emails',
  Users = '/api/portal/client/users',
  InviteUser = '/api/portal/client/users/invite',
  TwoFactorSetup = '/api/portal/auth/2fa-setup',
  TwoFactorEnable = '/api/portal/auth/2fa-enable',
  TwoFactorDisable = '/api/portal/auth/2fa-disable',
  Login = '/api/portal/auth/login',
  TwoFactorLogin = '/api/portal/auth/2fa-login',
  Logout = '/api/portal/auth/logout',
  Register = '/api/portal/auth/register',
  ConfirmEmail = '/api/portal/auth/confirm-email',
  EmailVerified = '/api/portal/auth/email-verified',
  ResendVerification = '/api/portal/auth/resend-verification',
  ForgotPassword = '/api/portal/auth/forgot-password',
  ResetPassword = '/api/portal/auth/reset-password',
  AcceptInvite = '/api/portal/auth/accept-invite',
  StoredMethods = '/api/portal/client/stored-payment-methods',
}

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

/** The actions on one service, each the path below the service itself. */
export enum ServiceAction {
  Cancel = 'cancel',
  ChangePassword = 'change-password',
  CpanelSso = 'cpanel-sso',
  Invoices = 'invoices',
  PlanChange = 'plan-change',
  PlanChangeOptions = 'plan-change-options',
  Setup = 'setup',
  Ssh = 'ssh',
  SshKeys = 'ssh/keys',
}

/**
 * The path of one service, or of an action on it.
 *
 * @param serviceId - The service id.
 * @param action - The action below the service; omit for the service itself.
 * @returns The BFF path.
 */
export const servicePath = (serviceId: number | string, action?: ServiceAction): string =>
  action ? `${AccountEndpoint.Services}/${serviceId}/${action}` : `${AccountEndpoint.Services}/${serviceId}`
