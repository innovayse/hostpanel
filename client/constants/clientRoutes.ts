/**
 * The client area's well-known paths, in one place so the route middleware, the session-expiry
 * plugin and the sign-in page agree on what the login page is and which pages need no session.
 *
 * @module constants/clientRoutes
 */

import type { AccountTab } from '~/types/accounttab'
import type { OrderSuccessQuery } from '~/types/ordersuccessquery'

/** The root of the customer area; every client page lives below it. */
export const CLIENT_AREA_PATH = '/client'

/** Where the account centre lives on a host it shares with other products. */
export const ACCOUNT_APP_PATH = '/account'

/** The sign-in page. */
export const CLIENT_LOGIN_PATH = '/client/login'

/** Where a signed-in customer lands when nothing more specific was asked for. */
export const CLIENT_DASHBOARD_PATH = '/client/dashboard'

/** The registration page. */
export const CLIENT_REGISTER_PATH = '/client/register'

/** The page that starts a password reset. */
export const CLIENT_FORGOT_PASSWORD_PATH = '/client/forgot-password'

/** The page that completes a password reset. */
export const CLIENT_RESET_PASSWORD_PATH = '/client/reset-password'

/** The page that confirms an e-mail address. */
export const CLIENT_CONFIRM_EMAIL_PATH = '/client/confirm-email'

/** The page a sign-in address change link opens, on the new address. */
export const CLIENT_CONFIRM_EMAIL_CHANGE_PATH = '/client/confirm-email-change'

/** The customer's services list. */
export const CLIENT_SERVICES_PATH = '/client/services'

/** The customer's domains list. */
export const CLIENT_DOMAINS_PATH = '/client/domains'

/** The customer's invoices list. */
export const CLIENT_INVOICES_PATH = '/client/invoices'

/** The customer's support tickets list. */
export const CLIENT_TICKETS_PATH = '/client/tickets'

/** The customer's account page. */
export const CLIENT_ACCOUNT_PATH = '/client/account'

/** The older address of the page that starts a password reset. */
export const CLIENT_FORGOT_PATH = '/client/forgot'

/** The form that opens a new support ticket. */
export const CLIENT_TICKET_NEW_PATH = '/client/tickets/new'

/** The domain transfer page. */
export const CLIENT_DOMAIN_TRANSFER_PATH = '/client/domains/transfer'

/** The customer's e-mail history; forwards to the account page. */
export const CLIENT_EMAILS_PATH = '/client/emails'

/** The page shown after an order was placed. */
export const CLIENT_ORDER_SUCCESS_PATH = '/client/order-success'

/** The Nitro route that starts the single sign-on authorisation. */
export const SSO_AUTHORIZE_PATH = '/api/portal/auth/sso/authorize'

/** The query parameter that carries the page to return to after signing in. */
export const CLIENT_REDIRECT_QUERY = 'redirect'

/**
 * The query parameter that asks a page to show the sign-in prompt. The sign-in callback adds it
 * when the identity provider found no session to sign the visitor in with.
 */
export const SIGN_IN_PROMPT_QUERY = 'signin'

/** The value of `SIGN_IN_PROMPT_QUERY` that shows the prompt. */
export const SIGN_IN_PROMPT_VALUE = 'required'

/**
 * The query parameter that names the account a visitor arrives for (`?authuser=<email>`), as every
 * product of a shared sign-on adds to the links it hands over. It is passed on to the identity
 * provider as `login_hint`.
 */
export const LOGIN_HINT_QUERY = 'authuser'

/** The client pages that need no session; a path equal to one of these, or below it, is public. */
export const CLIENT_PUBLIC_ROUTES: readonly string[] = [
  CLIENT_LOGIN_PATH,
  CLIENT_REGISTER_PATH,
  CLIENT_FORGOT_PASSWORD_PATH,
  CLIENT_RESET_PASSWORD_PATH,
  CLIENT_CONFIRM_EMAIL_PATH
]

/**
 * Path of one service's detail page.
 *
 * @param id - Service id.
 * @returns `/client/services/<id>`.
 */
export const clientServicePath = (id: number | string): string => `${CLIENT_SERVICES_PATH}/${id}`

/**
 * Path of one service's first-time setup wizard.
 *
 * @param id - Service id.
 * @returns `/client/services/<id>/setup`.
 */
export const clientServiceSetupPath = (id: number | string): string => `${clientServicePath(id)}/setup`

/**
 * Path of one support ticket.
 *
 * @param id - Ticket id.
 * @returns `/client/tickets/<id>`.
 */
export const clientTicketPath = (id: number | string): string => `${CLIENT_TICKETS_PATH}/${id}`

/**
 * Path of one invoice.
 *
 * @param id - Invoice id.
 * @returns `/client/invoices/<id>`.
 */
export const clientInvoicePath = (id: number | string): string => `${CLIENT_INVOICES_PATH}/${id}`

/**
 * Path of the page that pays one invoice.
 *
 * @param id - Invoice id.
 * @returns `/client/invoices/<id>/pay`.
 */
export const clientInvoicePayPath = (id: number | string): string => `${clientInvoicePath(id)}/pay`

/**
 * Path of one domain's management page.
 *
 * @param id - Domain id.
 * @returns `/client/domains/<id>`.
 */
export const clientDomainPath = (id: number | string): string => `${CLIENT_DOMAINS_PATH}/${id}`

/**
 * Path of one e-mail history entry.
 *
 * @param id - E-mail id.
 * @returns `/client/emails/<id>`.
 */
export const clientEmailPath = (id: number | string): string => `${CLIENT_EMAILS_PATH}/${id}`

/**
 * Path of the account page opened on a tab.
 *
 * @param tab - The tab to open, an `AccountTab` value.
 * @returns `/client/account?tab=<tab>`.
 */
export const clientAccountTabPath = (tab: AccountTab): string => `${CLIENT_ACCOUNT_PATH}?tab=${tab}`

/**
 * Path of the order-success page for an order; every query value is encoded.
 *
 * @param query - The order number and, when known, the invoice, the domain bought and whether the order was only digital.
 * @returns `/client/order-success?order=<order>` plus the optional values.
 */
export const clientOrderSuccessPath = (query: OrderSuccessQuery): string => {
  const params = new URLSearchParams({ order: query.order })
  if (query.invoice) {
    params.set('invoice', String(query.invoice))
  }
  if (query.domain) {
    params.set('domain', query.domain)
  }
  if (query.action) {
    params.set('action', query.action)
  }
  if (query.digital) {
    params.set('digital', '1')
  }
  return `${CLIENT_ORDER_SUCCESS_PATH}?${params.toString()}`
}
