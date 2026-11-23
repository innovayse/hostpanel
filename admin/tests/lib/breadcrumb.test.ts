/**
 * Unit tests for the breadcrumb builder: the trail comes from the navigation and the route pattern alone, a page of a
 * section extends its section, forms and records add one step, and the last step is the current page with no link.
 */
import { describe, expect, it } from 'vitest'
import { breadcrumbOf } from '@/lib/breadcrumb'
import { NAV_SECTIONS, withAddons } from '@/lib/navigation'
import { AppRoute } from '@/lib/routes'
import { messages } from '@/locales/messages'
import { valueAtPath } from '@/lib/valueAtPath'

/**
 * Resolves a locale key against the English catalogue.
 *
 * @param key - Dotted locale key.
 * @returns The English sentence.
 */
const english = (key: string): string => String(valueAtPath(messages.en, key))

/**
 * Builds the trail of an address and reads it as `label` or `label>label`, with a link marker.
 *
 * @param path - The address.
 * @param pattern - The route pattern it matched.
 * @param crumbKey - The record's `meta.crumb`.
 * @returns The steps as `label` for a link and `label!` for the current page.
 */
const trail = (path: string, pattern: string = path, crumbKey?: string): string[] =>
  breadcrumbOf(path, pattern, NAV_SECTIONS, english, crumbKey).map((step) => (step.to === undefined ? `${step.label}!` : step.label))

describe('breadcrumbOf', () => {
  it('names a top-level page by its section alone', () => {
    expect(trail('/servers')).toEqual(['Servers!'])
    expect(trail('/dashboard')).toEqual(['Dashboard!'])
  })

  it('puts the page of a section after the section', () => {
    expect(trail(AppRoute.SettingsGeneral)).toEqual(['Settings', 'General!'])
    expect(trail(AppRoute.BillingInvoices)).toEqual(['Billing', 'Invoices!'])
  })

  it('adds a step for a form below a page, from the route pattern', () => {
    expect(trail('/settings/slides/create')).toEqual(['Settings', 'Slides', 'New!'])
    expect(trail('/settings/slides/4/edit', '/settings/slides/:id/edit')).toEqual(['Settings', 'Slides', 'Edit!'])
    expect(trail('/clients/add')).toEqual(['Clients', 'List', 'New!'])
  })

  it('names a record page Details, and a tab of the record adds nothing', () => {
    expect(trail('/orders/8', '/orders/:id')).toEqual(['Orders', 'Details!'])
    expect(trail('/clients/7/invoices', '/clients/:id/invoices')).toEqual(['Clients', 'List', 'Details!'])
    expect(trail('/billing/invoices/5/view', '/billing/invoices/:id/:action')).toEqual(['Billing', 'Invoices', 'Details!'])
  })

  it('names the form alone when a record page leads to it', () => {
    expect(trail('/clients/7/tickets/new', '/clients/:id/tickets/new')).toEqual(['Clients', 'List', 'New!'])
  })

  it('leaves out a page the menu files under a section but that lives at another address', () => {
    expect(trail('/integrations/payments')).toEqual(['Integrations!'])
    expect(trail('/integrations/example/configure', '/integrations/:slug/configure')).toEqual(['Integrations', 'Configure!'])
  })

  it('links every step but the last, to the entry it names', () => {
    const steps = breadcrumbOf(AppRoute.SettingsGeneral, AppRoute.SettingsGeneral, NAV_SECTIONS, english)

    expect(steps.map((step) => step.to)).toEqual([AppRoute.Settings, undefined])
  })

  it('uses the route record name for a page outside the navigation, and Admin when it has none', () => {
    expect(trail('/notifications', '/notifications', 'common.notifications')).toEqual(['Notifications!'])
    expect(trail('/nowhere')).toEqual([`${english('common.admin')}!`])
  })

  it('draws the pages plugins add under their own entry, with the label the plugin gave', () => {
    const sections = withAddons(NAV_SECTIONS, [{ key: 'sites:main', label: 'Sites', icon: null, to: '/apps/sites' }])
    const steps = breadcrumbOf('/apps/sites/list', '/apps/:plugin/:page*', sections, english)

    expect(steps.map((step) => step.label)).toEqual(['Sites', 'Details'])
  })
})
