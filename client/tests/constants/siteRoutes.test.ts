/**
 * Unit tests for `constants/siteRoutes.ts`.
 *
 * @module tests/constants/siteRoutes.test
 */

import { describe, expect, it } from 'vitest'
import {
  SITE_ABOUT_PATH,
  SITE_ACCEPTABLE_USE_PATH,
  SITE_ANNOUNCEMENTS_PATH,
  SITE_CART_PATH,
  SITE_CHECKOUT_PATH,
  SITE_CONTACT_PATH,
  SITE_COOKIE_POLICY_PATH,
  SITE_DOMAINS_PATH,
  SITE_DOMAINS_TRANSFER_PATH,
  SITE_FAQ_PATH,
  SITE_HOME_PATH,
  SITE_HOSTING_PATH,
  SITE_HOSTING_PLANS_PATH,
  SITE_KNOWLEDGEBASE_PATH,
  SITE_PAYMENT_RESULT_PATH,
  SITE_PRIVACY_PATH,
  SITE_PRODUCTS_PATH,
  SITE_REFUND_POLICY_PATH,
  SITE_TERMS_PATH,
  siteAnnouncementPath,
  siteBundlePath,
  siteConfigurePath,
  siteFaqCategoryPath,
  siteHostingPlanPath,
  siteKnowledgebaseArticlePath,
  siteKnowledgebaseCategoryPath,
  siteProductFamilyPath,
  siteTrialPath,
} from '~/constants/siteRoutes'

describe('site routes', () => {
  it('serves the home page at /', () => {
    expect(SITE_HOME_PATH).toBe('/')
  })

  it('serves the hosting page at /hosting and its plan list at /hosting#plans', () => {
    expect(SITE_HOSTING_PATH).toBe('/hosting')
    expect(SITE_HOSTING_PLANS_PATH).toBe('/hosting#plans')
  })

  it('serves the catalogue at /products', () => {
    expect(SITE_PRODUCTS_PATH).toBe('/products')
  })

  it('serves the purchase pages at /cart, /checkout and /payment/result', () => {
    expect(SITE_CART_PATH).toBe('/cart')
    expect(SITE_CHECKOUT_PATH).toBe('/checkout')
    expect(SITE_PAYMENT_RESULT_PATH).toBe('/payment/result')
  })

  it('serves the domain pages at /domains and /domains/transfer', () => {
    expect(SITE_DOMAINS_PATH).toBe('/domains')
    expect(SITE_DOMAINS_TRANSFER_PATH).toBe('/domains/transfer')
  })

  it('serves the company pages at /about, /contact and /announcements', () => {
    expect(SITE_ABOUT_PATH).toBe('/about')
    expect(SITE_CONTACT_PATH).toBe('/contact')
    expect(SITE_ANNOUNCEMENTS_PATH).toBe('/announcements')
  })

  it('serves the help pages at /knowledgebase and /faq', () => {
    expect(SITE_KNOWLEDGEBASE_PATH).toBe('/knowledgebase')
    expect(SITE_FAQ_PATH).toBe('/faq')
  })

  it('serves the policies at their own paths', () => {
    expect(SITE_PRIVACY_PATH).toBe('/privacy')
    expect(SITE_COOKIE_POLICY_PATH).toBe('/cookie-policy')
    expect(SITE_TERMS_PATH).toBe('/terms')
    expect(SITE_REFUND_POLICY_PATH).toBe('/refund-policy')
    expect(SITE_ACCEPTABLE_USE_PATH).toBe('/acceptable-use')
  })

  it('builds a family page below the products page', () => {
    expect(siteProductFamilyPath('vps')).toBe('/products/vps')
  })

  it('builds a hosting plan page below the hosting page', () => {
    expect(siteHostingPlanPath('starter')).toBe('/hosting/starter')
  })

  it('builds the configure, bundle and trial pages from an id or slug', () => {
    expect(siteConfigurePath(7)).toBe('/configure/7')
    expect(siteBundlePath('3')).toBe('/bundles/3')
    expect(siteTrialPath('panel')).toBe('/trial/panel')
  })

  it('builds an announcement page below the announcements list', () => {
    expect(siteAnnouncementPath(12)).toBe('/announcements/12')
  })

  it('builds a question category page below the faq page', () => {
    expect(siteFaqCategoryPath('billing')).toBe('/faq/billing')
  })

  it('builds knowledge base article and category pages below the knowledge base', () => {
    expect(siteKnowledgebaseArticlePath(5)).toBe('/knowledgebase/5')
    expect(siteKnowledgebaseCategoryPath('getting-started')).toBe('/knowledgebase/category/getting-started')
  })
})
