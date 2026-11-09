/**
 * Unit tests for the navigation config: the sections come in the order an operator works, every
 * section and entry has a label in every language, and no screen is listed twice at the top level.
 */
import { describe, expect, it } from 'vitest'
import { NAV_SECTIONS, NavSectionId } from '@/lib/navigation'
import { AppRoute } from '@/lib/routes'
import { MessageLanguage, messages } from '@/locales/messages'
import { valueAtPath } from '@/lib/valueAtPath'

describe('NAV_SECTIONS', () => {
  it('lists the sections and their entries in the order an operator works', () => {
    expect(NAV_SECTIONS.map((section) => section.id)).toEqual([
      NavSectionId.Daily,
      NavSectionId.Insights,
      NavSectionId.Setup,
      NavSectionId.System,
    ])
    expect(NAV_SECTIONS.map((section) => section.items.map((item) => item.to))).toEqual([
      [AppRoute.Dashboard, AppRoute.Clients, AppRoute.Orders, AppRoute.Billing, AppRoute.Domains, AppRoute.Support],
      [AppRoute.Reports],
      [AppRoute.Servers, AppRoute.Integrations, AppRoute.Plugins, AppRoute.Storefront],
      [AppRoute.Migration, AppRoute.Settings],
    ])
  })

  it('has a label for every section, entry and sub-menu item in every language', () => {
    const keys = NAV_SECTIONS.flatMap((section) => [
      section.labelKey,
      ...section.items.flatMap((item) => [item.labelKey, ...(item.children ?? []).map((child) => child.labelKey)]),
    ])
    for (const language of Object.values(MessageLanguage)) {
      for (const key of keys) {
        expect(typeof valueAtPath(messages[language], key), `${language}: ${key}`).toBe('string')
      }
    }
  })
})
