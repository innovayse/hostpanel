/**
 * Unit tests for `constants/siteRoutes.ts`.
 *
 * @module tests/constants/siteRoutes.test
 */

import { describe, expect, it } from 'vitest'
import {
  SITE_ABOUT_PATH,
  SITE_CHECKOUT_PATH,
  SITE_CONTACT_PATH,
  SITE_COOKIE_POLICY_PATH,
  SITE_DOMAINS_PATH,
  SITE_DOMAINS_TRANSFER_PATH,
  SITE_FAQ_PATH,
  SITE_HOSTING_PATH,
  SITE_KNOWLEDGEBASE_PATH,
  SITE_PRIVACY_PATH,
  SITE_PRODUCTS_PATH,
  SITE_REFUND_POLICY_PATH,
  SITE_TERMS_PATH,
  siteProductFamilyPath,
} from '~/constants/siteRoutes'

describe('site routes', () => {
  it('pins the public page paths the portal is served on', () => {
    expect([
      SITE_HOSTING_PATH,
      SITE_PRODUCTS_PATH,
      SITE_CHECKOUT_PATH,
      SITE_DOMAINS_PATH,
      SITE_DOMAINS_TRANSFER_PATH,
      SITE_ABOUT_PATH,
      SITE_CONTACT_PATH,
      SITE_KNOWLEDGEBASE_PATH,
      SITE_FAQ_PATH,
      SITE_PRIVACY_PATH,
      SITE_COOKIE_POLICY_PATH,
      SITE_TERMS_PATH,
      SITE_REFUND_POLICY_PATH,
    ]).toEqual([
      '/hosting', '/products', '/checkout', '/domains', '/domains/transfer', '/about', '/contact',
      '/knowledgebase', '/faq', '/privacy', '/cookie-policy', '/terms', '/refund-policy',
    ])
  })

  it('builds a family page below the products page', () => {
    expect(siteProductFamilyPath('vps')).toBe('/products/vps')
  })
})
