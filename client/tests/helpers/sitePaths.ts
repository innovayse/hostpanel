/**
 * The public site's page paths the layout tests expect a link to point at, by name, so a test
 * does not spell the same page out again. They are the unlocalised paths the pages hand to
 * `localePath`; a test that expects the localised one wraps them in `localized` (`runtimeStubs.ts`).
 * The customer area's pages are the route constants in `constants/clientRoutes.ts`.
 *
 * @module tests/helpers/sitePaths
 */

/** The public pages the header, the footer and the cart link to. */
export const SitePage = {
  Hosting: '/hosting',
  Products: '/products',
  Checkout: '/checkout',
  Domains: '/domains',
  DomainsTransfer: '/domains/transfer',
  About: '/about',
  Knowledgebase: '/knowledgebase',
  Faq: '/faq',
  Privacy: '/privacy',
  CookiePolicy: '/cookie-policy',
  Terms: '/terms',
  RefundPolicy: '/refund-policy',
} as const
