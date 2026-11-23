/**
 * The quick-link menu the single sign-on account widget shows for a signed-in customer.
 *
 * Each link carries a `label` already in the reader's language (the widget reads `label` and
 * `icon`) from the server-owned table in `clientMenuLabels`, plus the `client.nav.*` key. The
 * server may share only `types/` and `utils/transportLanguage.ts` with the client, so it never
 * reads the client's locale catalogues.
 *
 * @module server/utils/clientMenu
 */

import {
  CLIENT_DASHBOARD_PATH,
  CLIENT_DOMAINS_PATH,
  CLIENT_INVOICES_PATH,
  CLIENT_SERVICES_PATH,
  CLIENT_TICKETS_PATH
} from '~/constants/clientRoutes'
import { ClientMenuIcon } from '~/types/clientmenuicon'
import type { ClientMenuItem } from '~/types/clientmenuitem'
import { clientMenuLabel } from '~/server/utils/clientMenuLabels'

/** The links in display order: the `client.nav` key naming each, its address and its icon. */
const MENU: readonly Omit<ClientMenuItem, 'label'>[] = [
  { labelKey: 'client.nav.dashboard', url: CLIENT_DASHBOARD_PATH, icon: ClientMenuIcon.Dashboard },
  { labelKey: 'client.nav.services', url: CLIENT_SERVICES_PATH, icon: ClientMenuIcon.Services },
  { labelKey: 'client.nav.domains', url: CLIENT_DOMAINS_PATH, icon: ClientMenuIcon.Domains },
  { labelKey: 'client.nav.invoices', url: CLIENT_INVOICES_PATH, icon: ClientMenuIcon.Invoices },
  { labelKey: 'client.nav.support', url: CLIENT_TICKETS_PATH, icon: ClientMenuIcon.Support }
]

/**
 * Builds the widget menu in one language.
 *
 * @param language - The reader's language code (`en`, `ru`, `hy`, `de`, `es` or `fr`); unsupported reads as English.
 * @returns The quick links in display order, each with its localized `label`, key, address and icon.
 */
export const buildClientMenu = (language: string): ClientMenuItem[] => MENU.map(item => ({
  ...item,
  label: clientMenuLabel(item.icon, language)
}))
