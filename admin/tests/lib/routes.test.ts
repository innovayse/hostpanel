/**
 * Unit tests for `AppRoute`: every path is a root-relative screen path, written once.
 */
import { describe, expect, it } from 'vitest'
import {
  AppRoute,
  ClientSection,
  clientItemPath,
  clientPath,
  clientSectionPath,
  clientTicketNewPath,
  createPath,
  editPath,
  emailCampaignPath,
  integrationCategoryPath,
  integrationConfigurePath,
  invoiceEditPath,
  invoicePath,
  invoiceViewPath,
  orderPath,
  pluginPagePath,
  pluginPath,
  quoteAddPath,
  quotePath,
  reportPagePath,
  supportTicketPath,
  transactionEditPath,
} from '@/lib/routes'

describe('AppRoute', () => {
  it('holds root-relative paths without a trailing slash', () => {
    for (const path of Object.values(AppRoute)) {
      expect(path.startsWith('/')).toBe(true)
      expect(path.endsWith('/')).toBe(false)
    }
  })

  it('holds no path twice', () => {
    const paths = Object.values(AppRoute)

    expect(new Set(paths).size).toBe(paths.length)
  })

  it('names the screens the guards and sign-in flow send people to', () => {
    expect(AppRoute.Login).toBe('/login')
    expect(AppRoute.Dashboard).toBe('/dashboard')
    expect(AppRoute.Setup).toBe('/setup')
  })
})

describe('route builders', () => {
  it('builds the create and edit paths under a list screen', () => {
    expect(createPath(AppRoute.StorefrontFaqs)).toBe('/storefront/faqs/create')
    expect(editPath(AppRoute.SettingsSlides, 7)).toBe('/settings/slides/7/edit')
    expect(editPath(AppRoute.SupportNetworkIssues, '3')).toBe('/support/network-issues/3/edit')
  })

  it('builds the client paths', () => {
    expect(clientPath(5)).toBe('/clients/5')
    expect(clientSectionPath(5, ClientSection.BillableItems)).toBe('/clients/5/billable-items')
    expect(clientItemPath(5, ClientSection.Services, 9)).toBe('/clients/5/services/9')
    expect(clientTicketNewPath(5)).toBe('/clients/5/tickets/new')
  })

  it('builds the billing, order and support paths', () => {
    expect(invoicePath(4)).toBe('/billing/4')
    expect(invoiceViewPath(4)).toBe('/billing/invoices/4/view')
    expect(invoiceEditPath(4)).toBe('/billing/invoices/4/edit')
    expect(quotePath(2)).toBe('/billing/quotes/2')
    expect(pluginPath('example-pay')).toBe('/plugins/example-pay')
    expect(quoteAddPath()).toBe('/billing/quotes/add')
    expect(quoteAddPath(8)).toBe('/billing/quotes/add?clientId=8')
    expect(transactionEditPath(6)).toBe('/billing/transactions/6/edit')
    expect(orderPath(1)).toBe('/orders/1')
    expect(supportTicketPath(3)).toBe('/support/tickets/3')
    expect(emailCampaignPath(8)).toBe('/support/email-campaigns/8')
  })

  it('builds the integration and report paths', () => {
    expect(integrationCategoryPath('payments')).toBe('/integrations/payments')
    expect(integrationConfigurePath('bank-transfer')).toBe('/integrations/bank-transfer/configure')
    expect(reportPagePath('tax')).toBe('/reports/tax')
  })
})

describe('pluginPagePath', () => {
  it('builds the address of a plugin page and of its root page', () => {
    expect(pluginPagePath('sites', 'sites/7')).toBe('/apps/sites/sites/7')
    expect(pluginPagePath('sites', '')).toBe('/apps/sites')
  })
})
