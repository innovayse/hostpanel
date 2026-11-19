/**
 * Tests for `server/utils/clientMenuLabels.ts`: the server-owned widget sentences cover every
 * language and every icon, and agree with the client catalogue so the two never drift.
 *
 * @module tests/server/utils/clientMenuLabels.test
 */

import { describe, expect, it } from 'vitest'
import en from '~/locales/en/client.json'
import ru from '~/locales/ru/client.json'
import hy from '~/locales/hy/client.json'
import es from '~/locales/es/client.json'
import { CLIENT_MENU_MESSAGES, clientMenuLabel } from '~/server/utils/clientMenuLabels'
import { AppLocale } from '~/types/applocale'
import { ClientMenuIcon } from '~/types/clientmenuicon'

/** The client catalogues by language, to compare the server table against. */
const CATALOGUES: Record<AppLocale, { client: { nav: Record<string, string> } }> = {
  [AppLocale.En]: en,
  [AppLocale.Ru]: ru,
  [AppLocale.Hy]: hy,
  [AppLocale.Es]: es
}

describe('CLIENT_MENU_MESSAGES', () => {
  it.each(Object.values(AppLocale))('has a sentence for every icon in %s, matching the client catalogue', (language) => {
    const nav = CATALOGUES[language].client.nav

    expect(CLIENT_MENU_MESSAGES[language]).toEqual({
      [ClientMenuIcon.Dashboard]: nav['dashboard'],
      [ClientMenuIcon.Services]: nav['services'],
      [ClientMenuIcon.Domains]: nav['domains'],
      [ClientMenuIcon.Invoices]: nav['invoices'],
      [ClientMenuIcon.Support]: nav['support']
    })
  })
})

describe('clientMenuLabel', () => {
  it('returns the sentence in the asked language', () => {
    expect(clientMenuLabel(ClientMenuIcon.Services, AppLocale.Ru)).toBe('Мои услуги')
  })

  it('falls back to English for an unknown language', () => {
    expect(clientMenuLabel(ClientMenuIcon.Services, 'xx')).toBe('My Services')
  })
})
