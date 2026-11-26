/**
 * The five legal pages an operator can replace with its own text, and where each page's text lives.
 *
 * One table for all five, so the pages, the composable that reads them and the tests name a page
 * and its settings in the same place. The settings are the `portal.legal.*` rows the admin edits (a
 * default row plus one per other language, named by `utils/localizedSetting.ts`); the storefront reads a
 * page's rows from `GET /api/portal/public/legal/:page`, never from the settings every page carries.
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
  /** The default-language row of the operator's text; the other languages' rows are named from it. */
  key: PortalSettingKey
}

/** The source of each legal page's operator text. */
export const LEGAL_PAGE_SOURCES: Record<LegalPage, LegalPageSource> = {
  [LegalPage.Terms]: {
    titleKey: 'terms.title',
    key: PortalSettingKey.LegalTerms
  },
  [LegalPage.Privacy]: {
    titleKey: 'privacy.title',
    key: PortalSettingKey.LegalPrivacy
  },
  [LegalPage.Refund]: {
    titleKey: 'refundPolicy.title',
    key: PortalSettingKey.LegalRefund
  },
  [LegalPage.AcceptableUse]: {
    titleKey: 'aup.title',
    key: PortalSettingKey.LegalAcceptableUse
  },
  [LegalPage.Cookies]: {
    titleKey: 'cookiePolicy.title',
    key: PortalSettingKey.LegalCookies
  }
}
