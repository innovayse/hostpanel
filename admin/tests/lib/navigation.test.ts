/**
 * Unit tests for the navigation config: the sections come in the order an operator works, every
 * section and entry has a label in every language, and no screen is listed twice at the top level.
 */
import { describe, expect, it } from 'vitest'
import { DEFAULT_PLUGIN_ICON, Icon } from '@/lib/icons'
import type { NavSection } from '@/lib/navigation'
import { activeNavPath, isNavItemActive, NAV_SECTIONS, NavSectionId, navTitleKey, withAddons } from '@/lib/navigation'
import { AppRoute } from '@/lib/routes'
import { MessageLanguage, messages } from '@/locales/messages'
import { valueAtPath } from '@/lib/valueAtPath'

describe('NAV_SECTIONS', () => {
  it('lists the sections and their entries in the order an operator works', () => {
    expect(NAV_SECTIONS.map((section) => section.id)).toEqual([
      NavSectionId.Home,
      NavSectionId.Sales,
      NavSectionId.Support,
      NavSectionId.Catalog,
      NavSectionId.Infrastructure,
      NavSectionId.Insights,
      NavSectionId.System,
    ])
    expect(NAV_SECTIONS.map((section) => section.items.map((item) => item.to))).toEqual([
      [AppRoute.Dashboard],
      [AppRoute.Clients, AppRoute.Orders, AppRoute.Billing, AppRoute.Domains],
      [AppRoute.Support],
      [AppRoute.Storefront],
      [AppRoute.Servers, AppRoute.Integrations, AppRoute.Plugins],
      [AppRoute.Reports],
      [AppRoute.Settings, AppRoute.Fraud, AppRoute.Migration],
    ])
  })

  it('draws the dashboard under no heading and every other section under one', () => {
    expect(NAV_SECTIONS.map((section) => section.labelKey)).toEqual([
      undefined,
      'nav.sections.sales',
      'nav.sections.support',
      'nav.sections.catalog',
      'nav.sections.infrastructure',
      'nav.sections.insights',
      'nav.sections.system',
    ])
  })

  it('has a label for every section, entry and sub-menu item in every language', () => {
    const keys = NAV_SECTIONS.flatMap((section) => [
      section.labelKey,
      ...section.items.flatMap((item) => [item.labelKey, item.titleKey, ...(item.children ?? []).flatMap((child) => [child.labelKey, child.titleKey])]),
    ]).filter((key): key is string => key !== undefined)
    for (const language of Object.values(MessageLanguage)) {
      for (const key of keys) {
        expect(typeof valueAtPath(messages[language], key), `${language}: ${key}`).toBe('string')
      }
    }
  })
})

describe('navTitleKey', () => {
  it('gives the full title of an entry that has one and the menu label of one that does not', () => {
    expect(navTitleKey(AppRoute.BillingGatewayLog)).toBe('billing.gatewayLog.gatewayTransactionLog')
    expect(navTitleKey(AppRoute.Orders)).toBe('nav.orders')
  })

  it('finds a sub-menu item as well as a top-level entry', () => {
    expect(navTitleKey(AppRoute.BillingPaymentClaims)).toBe('billing.paymentClaims.heading')
    expect(navTitleKey(AppRoute.Billing)).toBe('nav.billing')
  })

  it('gives a key that resolves to a sentence in every language for the address of every entry and sub-menu item', () => {
    const addresses = NAV_SECTIONS.flatMap((section) => section.items.flatMap((item) => [item.to, ...(item.children ?? []).map((child) => child.to)]))
    for (const language of Object.values(MessageLanguage)) {
      for (const address of addresses) {
        expect(typeof valueAtPath(messages[language], navTitleKey(address)), `${language}: ${address}`).toBe('string')
      }
    }
  })

  it('rejects a path that no entry owns', () => {
    expect(() => navTitleKey('/nowhere')).toThrow('No navigation entry for /nowhere')
  })
})

describe('NAV_SECTIONS icons', () => {
  it('gives every top-level entry its own icon from the shared icon set', () => {
    const icons = NAV_SECTIONS.flatMap((section) => section.items.map((item) => item.icon))

    expect(icons).toHaveLength(14)
    expect(new Set(icons).size).toBe(icons.length)
    for (const icon of icons) {
      expect(Object.values(Icon)).toContain(icon)
    }
  })
})

describe('withAddons', () => {
  it('leaves the sections as they are while no plugin declares a menu item', () => {
    expect(withAddons(NAV_SECTIONS, []).map((section) => section.id)).not.toContain(NavSectionId.Addons)
  })

  it('puts the plugins entries in an Add-ons section before System, with their own labels and a built-in icon as markup', () => {
    const sections = withAddons(NAV_SECTIONS, [
      { key: 'sites:main', label: 'Sites', icon: 'unknown-icon', to: '/apps/sites' },
      { key: 'sites:usage', label: 'Usage', icon: null, to: '/apps/sites/usage' },
    ])

    expect(sections.map((section) => section.id)).toEqual([
      NavSectionId.Home,
      NavSectionId.Sales,
      NavSectionId.Support,
      NavSectionId.Catalog,
      NavSectionId.Infrastructure,
      NavSectionId.Insights,
      NavSectionId.Addons,
      NavSectionId.System,
    ])
    const addons = sections[6]
    expect(addons?.labelKey).toBe('nav.sections.addons')
    expect(addons?.items).toEqual([
      { to: '/apps/sites', label: 'Sites', icon: DEFAULT_PLUGIN_ICON },
      { to: '/apps/sites/usage', label: 'Usage', icon: DEFAULT_PLUGIN_ICON },
    ])
  })

  it('labels the Add-ons section in every language', () => {
    for (const language of Object.values(MessageLanguage)) {
      expect(typeof valueAtPath(messages[language], 'nav.sections.addons'), language).toBe('string')
    }
  })
})

describe('activeNavPath', () => {
  it('picks the create child over its parent entry', () => {
    expect(activeNavPath(NAV_SECTIONS, AppRoute.SupportTicketsNew)).toBe(AppRoute.SupportTicketsNew)
    expect(activeNavPath(NAV_SECTIONS, AppRoute.SupportTickets)).toBe(AppRoute.SupportTickets)
  })

  it('does not match a sibling whose name only starts with the path', () => {
    expect(activeNavPath(NAV_SECTIONS, `${AppRoute.SupportTickets}-archive`)).toBe(AppRoute.Support)
    expect(activeNavPath(NAV_SECTIONS, `${AppRoute.Clients}X`)).toBeNull()
  })

  it('keeps a page below an entry on that entry, ids and deeper pages included', () => {
    expect(activeNavPath(NAV_SECTIONS, `${AppRoute.SupportTickets}/42`)).toBe(AppRoute.SupportTickets)
    expect(activeNavPath(NAV_SECTIONS, `${AppRoute.Clients}/7/services/3`)).toBe(AppRoute.Clients)
  })

  it('ignores a query string and a hash', () => {
    expect(activeNavPath(NAV_SECTIONS, `${AppRoute.SupportTicketsNew}?clientId=5`)).toBe(AppRoute.SupportTicketsNew)
    expect(activeNavPath(NAV_SECTIONS, `${AppRoute.SupportTickets}#top`)).toBe(AppRoute.SupportTickets)
  })

  it('lets a sub-menu page win over a top-level entry it sits under in the path', () => {
    expect(activeNavPath(NAV_SECTIONS, AppRoute.IntegrationsPayments)).toBe(AppRoute.IntegrationsPayments)
    expect(activeNavPath(NAV_SECTIONS, AppRoute.Integrations)).toBe(AppRoute.Integrations)
  })

  it('returns null for an address no entry owns', () => {
    expect(activeNavPath(NAV_SECTIONS, '/nowhere')).toBeNull()
  })

  it('marks exactly one top-level entry and one page for the address of every entry and sub-menu page, create pages included', () => {
    const addresses = NAV_SECTIONS.flatMap((section) => section.items.flatMap((item) => [item.to, ...(item.children ?? []).map((child) => child.to)]))
    for (const address of addresses) {
      for (const path of [address, `${address}/12`, `${address}?page=2`]) {
        const active = activeNavPath(NAV_SECTIONS, path)
        const items = NAV_SECTIONS.flatMap((section) => section.items).filter((item) => isNavItemActive(item, active))
        const pages = NAV_SECTIONS.flatMap((section) => section.items.flatMap((item) => item.children ?? [])).filter((child) => child.to === active)
        expect(items, path).toHaveLength(1)
        expect(pages.length, path).toBeLessThanOrEqual(1)
      }
    }
  })

  it('treats a plugin entry nested in another as the page on screen', () => {
    const sections: NavSection[] = withAddons([], [
      { key: 'sites', to: '/apps/sites', label: 'Sites', icon: null },
      { key: 'usage', to: '/apps/sites/usage', label: 'Usage', icon: null },
    ])
    expect(activeNavPath(sections, '/apps/sites/usage')).toBe('/apps/sites/usage')
    expect(activeNavPath(sections, '/apps/sites')).toBe('/apps/sites')
  })
})
