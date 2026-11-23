/**
 * Tests for `server/utils/clientMenu.ts`: the widget quick links, carrying a localized `label`,
 * the locale key, the address and the icon the external widget switches on.
 *
 * @module tests/server/utils/clientMenu.test
 */

import { describe, expect, it } from 'vitest'
import { CLIENT_DASHBOARD_PATH, CLIENT_TICKETS_PATH } from '~/constants/clientRoutes'
import en from '~/locales/en/client.json'
import { buildClientMenu } from '~/server/utils/clientMenu'
import { AppLocale } from '~/types/applocale'
import { ClientMenuIcon } from '~/types/clientmenuicon'

/** The icon wire values the external widget expects, in display order (unchanged from the original menu). */
const WIDGET_ICONS: readonly string[] = ['dashboard', 'services', 'domains', 'invoices', 'support']

/** The English sentences the original menu sent as `label`. */
const ORIGINAL_ENGLISH_LABELS: readonly string[] = ['Dashboard', 'My Services', 'My Domains', 'Invoices', 'Support']

describe('buildClientMenu', () => {
  it('lists the five client areas in order with their addresses, keys, labels and icons', () => {
    const menu = buildClientMenu(AppLocale.En)

    expect(menu).toHaveLength(5)
    expect(menu[0]).toEqual({
      label: 'Dashboard',
      labelKey: 'client.nav.dashboard',
      url: CLIENT_DASHBOARD_PATH,
      icon: ClientMenuIcon.Dashboard
    })
    expect(menu[4]).toEqual({
      label: 'Support',
      labelKey: 'client.nav.support',
      url: CLIENT_TICKETS_PATH,
      icon: ClientMenuIcon.Support
    })
  })

  it('keeps the icon values and English labels the external widget was built against', () => {
    const menu = buildClientMenu(AppLocale.En)

    expect(menu.map(item => item.icon)).toEqual(WIDGET_ICONS)
    expect(menu.map(item => item.label)).toEqual(ORIGINAL_ENGLISH_LABELS)
  })

  it.each([AppLocale.En, AppLocale.Ru, AppLocale.Hy])('gives every link a non-empty label in %s', (language) => {
    for (const item of buildClientMenu(language)) {
      expect(item.label).toBeTruthy()
    }
  })

  it('translates the labels: ru, hy and fr differ from en and from each other', () => {
    /**
     * The menu labels, in order, for one language.
     *
     * @param language Language to build the menu in.
     * @returns The labels.
     */
    const labels = (language: AppLocale): string[] => buildClientMenu(language).map(item => item.label)

    expect(labels(AppLocale.Ru)).toEqual(['Панель управления', 'Мои услуги', 'Мои домены', 'Счета', 'Поддержка'])
    expect(labels(AppLocale.Hy)).toEqual(['Վահանակ', 'Իմ ծառայությունները', 'Իմ դոմենները', 'Հաշիվ-ապրանքագրեր', 'Աջակցություն'])
    expect(labels(AppLocale.Fr)).toEqual(['Tableau de bord', 'Mes services', 'Mes domaines', 'Factures', 'Support'])
    expect(labels(AppLocale.Ru)).not.toEqual(labels(AppLocale.En))
  })

  it('reads an unsupported language as English', () => {
    expect(buildClientMenu('xx').map(item => item.label)).toEqual(ORIGINAL_ENGLISH_LABELS)
  })

  it('names only keys the client catalogue defines', () => {
    const nav = en.client.nav as Record<string, string>

    for (const item of buildClientMenu(AppLocale.En)) {
      expect(nav[item.labelKey.replace('client.nav.', '')]).toBeTruthy()
    }
  })

  it('hands out copies, so a caller cannot change the shared menu', () => {
    const first = buildClientMenu(AppLocale.En)
    first[0]!.url = '/changed'

    expect(buildClientMenu(AppLocale.En)[0]?.url).toBe(CLIENT_DASHBOARD_PATH)
  })
})
