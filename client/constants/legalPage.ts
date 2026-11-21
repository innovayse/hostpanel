/**
 * The five legal pages an operator can replace with its own text, and where each page's text lives.
 *
 * One table for all five, so the pages, the composable that reads them and the tests name a page
 * and a setting in the same place. The settings are the `portal.legal.*` rows the admin edits (a
 * default row plus a Russian and an Armenian one) and the matching runtime-config fallbacks.
 *
 * @module constants/legalPage
 */

import { PortalSettingKey } from '~/constants/portalSettingKey'
import type { LocalizedSettingSpec } from '~/composables/useLocalizedSetting'

/** A legal page whose text the operator may state itself. */
export enum LegalPage {
  /** The terms of service. */
  Terms = 'terms',
  /** The privacy policy. */
  Privacy = 'privacy',
  /** The refund policy. */
  Refund = 'refund',
  /** The acceptable use policy. */
  AcceptableUse = 'acceptableUse',
  /** The cookie policy. */
  Cookies = 'cookies'
}

/** What one legal page reads. */
export interface LegalPageSource {
  /** Locale key of the page title, used as the heading of text written before the first section. */
  titleKey: string
  /** The default, Russian and Armenian rows holding the operator's text. */
  spec: LocalizedSettingSpec
}

/** The source of each legal page's operator text. */
export const LEGAL_PAGE_SOURCES: Record<LegalPage, LegalPageSource> = {
  [LegalPage.Terms]: {
    titleKey: 'terms.title',
    spec: {
      default: { key: PortalSettingKey.LegalTerms, envKey: 'portalLegalTerms' },
      ru: { key: PortalSettingKey.LegalTermsRu, envKey: 'portalLegalTermsRu' },
      hy: { key: PortalSettingKey.LegalTermsHy, envKey: 'portalLegalTermsHy' }
    }
  },
  [LegalPage.Privacy]: {
    titleKey: 'privacy.title',
    spec: {
      default: { key: PortalSettingKey.LegalPrivacy, envKey: 'portalLegalPrivacy' },
      ru: { key: PortalSettingKey.LegalPrivacyRu, envKey: 'portalLegalPrivacyRu' },
      hy: { key: PortalSettingKey.LegalPrivacyHy, envKey: 'portalLegalPrivacyHy' }
    }
  },
  [LegalPage.Refund]: {
    titleKey: 'refundPolicy.title',
    spec: {
      default: { key: PortalSettingKey.LegalRefund, envKey: 'portalLegalRefund' },
      ru: { key: PortalSettingKey.LegalRefundRu, envKey: 'portalLegalRefundRu' },
      hy: { key: PortalSettingKey.LegalRefundHy, envKey: 'portalLegalRefundHy' }
    }
  },
  [LegalPage.AcceptableUse]: {
    titleKey: 'aup.title',
    spec: {
      default: { key: PortalSettingKey.LegalAcceptableUse, envKey: 'portalLegalAcceptableUse' },
      ru: { key: PortalSettingKey.LegalAcceptableUseRu, envKey: 'portalLegalAcceptableUseRu' },
      hy: { key: PortalSettingKey.LegalAcceptableUseHy, envKey: 'portalLegalAcceptableUseHy' }
    }
  },
  [LegalPage.Cookies]: {
    titleKey: 'cookiePolicy.title',
    spec: {
      default: { key: PortalSettingKey.LegalCookies, envKey: 'portalLegalCookies' },
      ru: { key: PortalSettingKey.LegalCookiesRu, envKey: 'portalLegalCookiesRu' },
      hy: { key: PortalSettingKey.LegalCookiesHy, envKey: 'portalLegalCookiesHy' }
    }
  }
}
