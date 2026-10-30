/**
 * Unit tests for `constants/clientRoutes.ts`.
 *
 * @module tests/constants/clientRoutes.test
 */

import { describe, expect, it } from 'vitest'
import {
  CLIENT_DASHBOARD_PATH,
  CLIENT_LOGIN_PATH,
  CLIENT_PUBLIC_ROUTES,
  CLIENT_REDIRECT_QUERY,
  CLIENT_SERVICES_PATH,
  CLIENT_TICKETS_PATH,
  clientAccountTabPath,
  clientDomainPath,
  clientInvoicePath,
  clientInvoicePayPath,
  clientOrderSuccessPath,
  clientServicePath,
  clientServiceSetupPath,
  clientTicketPath,
  SSO_AUTHORIZE_PATH,
} from '~/constants/clientRoutes'
import { AccountTab } from '~/types/accounttab'
import { DomainOrderAction } from '~/types/domainorderaction'

describe('client routes', () => {
  it('keeps the well-known paths', () => {
    expect(CLIENT_LOGIN_PATH).toBe('/client/login')
    expect(CLIENT_DASHBOARD_PATH).toBe('/client/dashboard')
    expect(CLIENT_REDIRECT_QUERY).toBe('redirect')
  })

  it('lists the sign-in page among the public routes and never the dashboard', () => {
    expect(CLIENT_PUBLIC_ROUTES).toContain(CLIENT_LOGIN_PATH)
    expect(CLIENT_PUBLIC_ROUTES).not.toContain(CLIENT_DASHBOARD_PATH)
  })

  it('keeps every public route under the client area', () => {
    expect(CLIENT_PUBLIC_ROUTES.every(route => route.startsWith('/client/'))).toBe(true)
  })

  it('keeps the list pages under the client area and the SSO entry under the BFF', () => {
    expect(CLIENT_SERVICES_PATH).toBe('/client/services')
    expect(CLIENT_TICKETS_PATH).toBe('/client/tickets')
    expect(SSO_AUTHORIZE_PATH).toBe('/api/portal/auth/sso/authorize')
  })

  it('builds the record paths below their list pages', () => {
    expect(clientServicePath(7)).toBe('/client/services/7')
    expect(clientServiceSetupPath('7')).toBe('/client/services/7/setup')
    expect(clientTicketPath(4)).toBe('/client/tickets/4')
    expect(clientInvoicePath(9)).toBe('/client/invoices/9')
    expect(clientInvoicePayPath(9)).toBe('/client/invoices/9/pay')
    expect(clientDomainPath(2)).toBe('/client/domains/2')
  })

  it('builds the account tab and order-success paths', () => {
    expect(clientAccountTabPath(AccountTab.Users)).toBe('/client/account?tab=users')
    expect(clientOrderSuccessPath({ order: 'ORD-0042' })).toBe('/client/order-success?order=ORD-0042')
    expect(clientOrderSuccessPath({ order: 'A 1', invoice: 7, domain: 'a&b.com', action: DomainOrderAction.Transfer }))
      .toBe('/client/order-success?order=A+1&invoice=7&domain=a%26b.com&action=transfer')
    expect(clientOrderSuccessPath({ order: 'ORD-0042', digital: true })).toBe('/client/order-success?order=ORD-0042&digital=1')
    expect(clientOrderSuccessPath({ order: 'ORD-0042', digital: false })).toBe('/client/order-success?order=ORD-0042')
  })
})
