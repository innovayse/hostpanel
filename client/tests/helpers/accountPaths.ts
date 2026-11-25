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
  /** The signed-in account. */
  Me = '/api/portal/client/me',
  /** The service list. */
  Services = '/api/portal/client/services',
  /** The support tickets. */
  Tickets = '/api/portal/client/tickets',
  /** The sign-in address change. */
  EmailChange = '/api/portal/client/email-change',
  /** The sub-contacts. */
  Contacts = '/api/portal/client/contacts',
  /** The account emails. */
  Emails = '/api/portal/client/emails',
  /** The team members. */
  Users = '/api/portal/client/users',
  /** The team invitation. */
  InviteUser = '/api/portal/client/users/invite',
  /** Starts two-factor setup. */
  TwoFactorSetup = '/api/portal/auth/2fa-setup',
  /** Turns two-factor on. */
  TwoFactorEnable = '/api/portal/auth/2fa-enable',
  /** Turns two-factor off. */
  TwoFactorDisable = '/api/portal/auth/2fa-disable',
  /** Signs in. */
  Login = '/api/portal/auth/login',
  /** Completes a two-factor sign-in. */
  TwoFactorLogin = '/api/portal/auth/2fa-login',
  /** Signs out. */
  Logout = '/api/portal/auth/logout',
  /** Registers an account. */
  Register = '/api/portal/auth/register',
  /** Confirms an email address. */
  ConfirmEmail = '/api/portal/auth/confirm-email',
  /** Tells whether the email is verified. */
  EmailVerified = '/api/portal/auth/email-verified',
  /** Resends the verification email. */
  ResendVerification = '/api/portal/auth/resend-verification',
  /** Asks for a password reset. */
  ForgotPassword = '/api/portal/auth/forgot-password',
  /** Sets a new password from a reset link. */
  ResetPassword = '/api/portal/auth/reset-password',
  /** Accepts a team invitation. */
  AcceptInvite = '/api/portal/auth/accept-invite',
  /** The stored payment methods. */
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
  /** Cancels the service. */
  Cancel = 'cancel',
  /** Changes the service password. */
  ChangePassword = 'change-password',
  /** Opens the control panel without a password. */
  PanelSso = 'panel-sso',
  /** The details to sign in by hand to the service's panel. */
  PanelLogin = 'panel-login',
  /** The service invoices. */
  Invoices = 'invoices',
  /** Changes the plan. */
  PlanChange = 'plan-change',
  /** The plans it can change to. */
  PlanChangeOptions = 'plan-change-options',
  /** The service setup. */
  Setup = 'setup',
  /** The SSH access. */
  Ssh = 'ssh',
  /** The SSH keys. */
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
