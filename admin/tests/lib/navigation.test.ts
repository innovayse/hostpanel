/**
 * Unit tests for the navigation config: the sections come in the order an operator works, every
 * section and entry has a label in every language, and no screen is listed twice at the top level.
 */
import { describe, expect, it } from 'vitest'
import { Icon } from '@/lib/icons'
import { NAV_SECTIONS, NavSectionId, withAddons } from '@/lib/navigation'
import { DEFAULT_PLUGIN_ICON } from '@/lib/pluginView'
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
      ...section.items.flatMap((item) => [item.labelKey, ...(item.children ?? []).map((child) => child.labelKey)]),
    ]).filter((key): key is string => key !== undefined)
    for (const language of Object.values(MessageLanguage)) {
      for (const key of keys) {
        expect(typeof valueAtPath(messages[language], key), `${language}: ${key}`).toBe('string')
      }
    }
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
      { to: '/apps/sites', label: 'Sites', icon: `<path d="${DEFAULT_PLUGIN_ICON}"/>` },
      { to: '/apps/sites/usage', label: 'Usage', icon: `<path d="${DEFAULT_PLUGIN_ICON}"/>` },
    ])
  })

  it('labels the Add-ons section in every language', () => {
    for (const language of Object.values(MessageLanguage)) {
      expect(typeof valueAtPath(messages[language], 'nav.sections.addons'), language).toBe('string')
    }
  })
})
