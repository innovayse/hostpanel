/**
 * The sentences of the single sign-on account widget menu, owned by the server.
 *
 * The server may share only `types/` and `utils/transportLanguage.ts` with the client, so it
 * cannot read the client's locale catalogues; the widget (served by another product) reads a
 * plain `label`, so the server keeps its own table, keyed by icon and by language.
 *
 * @module server/utils/clientMenuLabels
 */

import { AppLocale } from '~/types/applocale'
import { ClientMenuIcon } from '~/types/clientmenuicon'

/** The sentence of every menu link in one language, keyed by the link's icon. */
export type ClientMenuMessages = Readonly<Record<ClientMenuIcon, string>>

/** The widget menu sentences in every language the portal is published in. */
export const CLIENT_MENU_MESSAGES: Readonly<Record<AppLocale, ClientMenuMessages>> = {
  [AppLocale.En]: {
    [ClientMenuIcon.Dashboard]: 'Dashboard',
    [ClientMenuIcon.Services]: 'My Services',
    [ClientMenuIcon.Domains]: 'My Domains',
    [ClientMenuIcon.Invoices]: 'Invoices',
    [ClientMenuIcon.Support]: 'Support'
  },
  [AppLocale.Ru]: {
    [ClientMenuIcon.Dashboard]: 'Панель управления',
    [ClientMenuIcon.Services]: 'Мои услуги',
    [ClientMenuIcon.Domains]: 'Мои домены',
    [ClientMenuIcon.Invoices]: 'Счета',
    [ClientMenuIcon.Support]: 'Поддержка'
  },
  [AppLocale.Hy]: {
    [ClientMenuIcon.Dashboard]: 'Վահանակ',
    [ClientMenuIcon.Services]: 'Իմ ծառայությունները',
    [ClientMenuIcon.Domains]: 'Իմ դոմենները',
    [ClientMenuIcon.Invoices]: 'Հաշիվ-ապրանքագրեր',
    [ClientMenuIcon.Support]: 'Աջակցություն'
  },
  [AppLocale.Fr]: {
    [ClientMenuIcon.Dashboard]: 'Tableau de bord',
    [ClientMenuIcon.Services]: 'Mes services',
    [ClientMenuIcon.Domains]: 'Mes domaines',
    [ClientMenuIcon.Invoices]: 'Factures',
    [ClientMenuIcon.Support]: 'Support'
  }
}

/**
 * Looks up the sentence of one menu link in one language.
 *
 * @param icon - The link's icon, which identifies the link.
 * @param language - A language code; anything unsupported reads as the default language.
 * @returns The sentence in that language.
 */
export const clientMenuLabel = (icon: ClientMenuIcon, language: string): string => {
  const messages = CLIENT_MENU_MESSAGES[language as AppLocale] ?? CLIENT_MENU_MESSAGES[AppLocale.En]
  return messages[icon]
}
