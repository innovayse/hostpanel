/**
 * The public site's page paths, unlocalised, in one place so the header, the footer, the catalogue
 * helpers and the tests agree on where a page lives. They are what a page hands to `localePath`;
 * the customer area's paths are in `constants/clientRoutes.ts`.
 *
 * @module constants/siteRoutes
 */

/** The hosting family's page. */
export const SITE_HOSTING_PATH = '/hosting'

/** The all-products page; a family's page lives below it. */
export const SITE_PRODUCTS_PATH = '/products'

/** The checkout page. */
export const SITE_CHECKOUT_PATH = '/checkout'

/** The domain search page. */
export const SITE_DOMAINS_PATH = '/domains'

/** The domain transfer page. */
export const SITE_DOMAINS_TRANSFER_PATH = '/domains/transfer'

/** The about page. */
export const SITE_ABOUT_PATH = '/about'

/** The contact page. */
export const SITE_CONTACT_PATH = '/contact'

/** The knowledge base. */
export const SITE_KNOWLEDGEBASE_PATH = '/knowledgebase'

/** The frequently asked questions page. */
export const SITE_FAQ_PATH = '/faq'

/** The privacy policy. */
export const SITE_PRIVACY_PATH = '/privacy'

/** The cookie policy. */
export const SITE_COOKIE_POLICY_PATH = '/cookie-policy'

/** The terms of service. */
export const SITE_TERMS_PATH = '/terms'

/** The refund policy. */
export const SITE_REFUND_POLICY_PATH = '/refund-policy'

/**
 * Path of one product family's page.
 *
 * @param key - The family key.
 * @returns `/products/<key>`.
 */
export const siteProductFamilyPath = (key: string): string => `${SITE_PRODUCTS_PATH}/${key}`
