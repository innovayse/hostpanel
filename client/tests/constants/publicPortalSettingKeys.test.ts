/**
 * Unit tests for `constants/publicPortalSettingKeys.ts`: the list must equal the backend's
 * public key set (`PortalSettingKeys.Public`, which is every key of `PortalSettingKeys.Defaults` except the operator legal texts, read per page from `GET /api/portal/public/legal/:page`),
 * hard-coded here so a drift on either side fails the build.
 *
 * @module tests/constants/publicPortalSettingKeys.test
 */

import { describe, expect, it } from 'vitest'
import { PUBLIC_PORTAL_SETTING_KEYS } from '~/constants/publicPortalSettingKeys'

/** The backend's `PortalSettingKeys.Public` set, copied by hand from `PortalSettingKeys.cs`. */
const BACKEND_PUBLIC_KEYS: readonly string[] = [
  'portal.about.text',
  'portal.about.text.de',
  'portal.about.text.es',
  'portal.about.text.fr',
  'portal.about.text.hy',
  'portal.about.text.ru',
  'portal.analytics.gtm_id',
  'portal.apps.account',
  'portal.apps.calendar',
  'portal.apps.docs',
  'portal.apps.enabled',
  'portal.apps.erp',
  'portal.apps.hostpanel',
  'portal.apps.mail',
  'portal.apps.sheets',
  'portal.apps.tasks',
  'portal.brand.accent',
  'portal.brand.font_body',
  'portal.brand.font_heading',
  'portal.brand.primary',
  'portal.chat.base_url',
  'portal.chat.provider',
  'portal.chat.website_token',
  'portal.chat.website_token.de',
  'portal.chat.website_token.es',
  'portal.chat.website_token.fr',
  'portal.chat.website_token.hy',
  'portal.chat.website_token.ru',
  'portal.contact.address',
  'portal.contact.address.de',
  'portal.contact.address.es',
  'portal.contact.address.fr',
  'portal.contact.address.hy',
  'portal.contact.address.ru',
  'portal.contact.email',
  'portal.contact.hours',
  'portal.contact.hours.de',
  'portal.contact.hours.es',
  'portal.contact.hours.fr',
  'portal.contact.hours.hy',
  'portal.contact.hours.ru',
  'portal.contact.map_url',
  'portal.contact.phone',
  'portal.contact.telegram',
  'portal.contact.timezone',
  'portal.contact.whatsapp',
  'portal.favicon',
  'portal.features.backups',
  'portal.features.caching',
  'portal.features.cdn',
  'portal.features.firewall',
  'portal.features.malware',
  'portal.features.storage',
  'portal.hero.badge',
  'portal.legal.tax_id',
  'portal.logo',
  'portal.logo.dark',
  'portal.logo.mark',
  'portal.migration.free',
  'portal.newsletter.action_url',
  'portal.site.name',
  'portal.site.tagline',
  'portal.social.facebook',
  'portal.social.instagram',
  'portal.social.linkedin',
  'portal.social.twitter',
  'portal.social.youtube',
  'portal.stats.clients',
  'portal.stats.regions',
  'portal.stats.uptime',
  'portal.template',
  'portal.theme.default',
  'portal.theme.user_toggle',
  'portal.sections.stats',
  'portal.sections.partners',
  'portal.sections.services',
  'portal.sections.process',
  'portal.sections.plans',
  'portal.sections.comparison',
  'portal.sections.dashboard',
  'portal.sections.use_cases',
  'portal.sections.migration',
  'portal.sections.projects',
  'portal.sections.testimonials',
  'portal.sections.faq',
  'portal.sections.cta',
  'portal.sections.locations',
  'portal.sections.performance',
  'portal.sections.security',
  'portal.sections.apps',
  'portal.sections.guarantees',
  'portal.sections.support',
  'portal.sections.trusted',
  'portal.stats.response_time',
  'portal.stats.sites_hosted',
  'portal.stats.support_response',
  'portal.features.ssl',
  'portal.features.ddos',
  'portal.installer.app_count',
  'portal.guarantee.money_back_days',
  'portal.guarantee.no_hidden_fees',
  'portal.support.response.chat',
  'portal.support.response.ticket',
  'portal.support.response.email',
  'portal.support.response.phone'
]

describe('PUBLIC_PORTAL_SETTING_KEYS', () => {
  it('equals the backend public key set', () => {
    expect([...PUBLIC_PORTAL_SETTING_KEYS].sort()).toEqual([...BACKEND_PUBLIC_KEYS].sort())
  })

  it('lists no key twice', () => {
    expect(new Set(PUBLIC_PORTAL_SETTING_KEYS).size).toBe(PUBLIC_PORTAL_SETTING_KEYS.length)
  })

  it('never lists a credential or integration key', () => {
    expect(PUBLIC_PORTAL_SETTING_KEYS.every(key => key.startsWith('portal.'))).toBe(true)
  })
})
