/**
 * The five legal pages an operator can replace with its own text, and where each page's text lives.
 *
 * One table for all five, so the pages, the composable that reads them and the tests name a page
 * and its settings in the same place. The settings are the `portal.legal.*` rows the admin edits (a
 * default row plus a Russian and an Armenian one); the storefront reads a page's three rows from
 * `GET /api/portal/public/legal/:page`, never from the settings every page carries.
 *
 * @module constants/legalPage
 */

import { PortalSettingKey } from '~/constants/portalSettingKey'

/** A legal page whose text the operator may state itself; the value is the page's segment in the legal text read. */
export enum LegalPage {
  /** The terms of service. */
  Terms = 'terms',
  /** The privacy policy. */
  Privacy = 'privacy',
  /** The refund policy. */
  Refund = 'refund',
  /** The acceptable use policy. */
  AcceptableUse = 'acceptable_use',
  /** The cookie policy. */
  Cookies = 'cookies'
}

/** What one legal page reads. */
export interface LegalPageSource {
  /** Locale key of the page title, used as the heading of text written before the first section. */
  titleKey: string
  /** The default-language row and the Russian and Armenian rows holding the operator's text. */
  keys: { default: PortalSettingKey, ru: PortalSettingKey, hy: PortalSettingKey }
}

/** The source of each legal page's operator text. */
export const LEGAL_PAGE_SOURCES: Record<LegalPage, LegalPageSource> = {
  [LegalPage.Terms]: {
    titleKey: 'terms.title',
    keys: { default: PortalSettingKey.LegalTerms, ru: PortalSettingKey.LegalTermsRu, hy: PortalSettingKey.LegalTermsHy }
  },
  [LegalPage.Privacy]: {
    titleKey: 'privacy.title',
    keys: { default: PortalSettingKey.LegalPrivacy, ru: PortalSettingKey.LegalPrivacyRu, hy: PortalSettingKey.LegalPrivacyHy }
  },
  [LegalPage.Refund]: {
    titleKey: 'refundPolicy.title',
    keys: { default: PortalSettingKey.LegalRefund, ru: PortalSettingKey.LegalRefundRu, hy: PortalSettingKey.LegalRefundHy }
  },
  [LegalPage.AcceptableUse]: {
    titleKey: 'aup.title',
    keys: { default: PortalSettingKey.LegalAcceptableUse, ru: PortalSettingKey.LegalAcceptableUseRu, hy: PortalSettingKey.LegalAcceptableUseHy }
  },
  [LegalPage.Cookies]: {
    titleKey: 'cookiePolicy.title',
    keys: { default: PortalSettingKey.LegalCookies, ru: PortalSettingKey.LegalCookiesRu, hy: PortalSettingKey.LegalCookiesHy }
  }
}
