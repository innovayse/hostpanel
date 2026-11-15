/**
 * The public site's page paths, unlocalised, in one place so the header, the footer, the catalogue
 * helpers and the tests agree on where a page lives. They are what a page hands to `localePath`;
 * the customer area's paths are in `constants/clientRoutes.ts`.
 *
 * @module constants/siteRoutes
 */

/** The hosting family's page. */
export const SITE_HOSTING_PATH = '/hosting'

/** The hosting page, scrolled to its plan list. */
export const SITE_HOSTING_PLANS_PATH = `${SITE_HOSTING_PATH}#plans`

/** The all-products page; a family's page lives below it. */
export const SITE_PRODUCTS_PATH = '/products'

/** The checkout page. */
export const SITE_CHECKOUT_PATH = '/checkout'

/** The cart page. */
export const SITE_CART_PATH = '/cart'

/** The page a payer returns to from a payment gateway; the caller adds the order or invoice query. */
export const SITE_PAYMENT_RESULT_PATH = '/payment/result'

/** The domain search page. */
export const SITE_DOMAINS_PATH = '/domains'

/** The domain transfer page. */
export const SITE_DOMAINS_TRANSFER_PATH = '/domains/transfer'

/** The about page. */
export const SITE_ABOUT_PATH = '/about'

/** The contact page. */
export const SITE_CONTACT_PATH = '/contact'

/** The announcements list. */
export const SITE_ANNOUNCEMENTS_PATH = '/announcements'

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

/** The acceptable use policy. */
export const SITE_ACCEPTABLE_USE_PATH = '/acceptable-use'

/** The refund policy. */
export const SITE_REFUND_POLICY_PATH = '/refund-policy'

/**
 * Path of one product family's page.
 *
 * @param key - The family key.
 * @returns `/products/<key>`.
 */
export const siteProductFamilyPath = (key: string): string => `${SITE_PRODUCTS_PATH}/${key}`

/**
 * Path of one hosting plan's page.
 *
 * @param slug - The plan's slug.
 * @returns `/hosting/<slug>`.
 */
export const siteHostingPlanPath = (slug: string): string => `${SITE_HOSTING_PATH}/${slug}`

/**
 * Path of the configure page of one plan.
 *
 * @param id - The plan's product id.
 * @returns `/configure/<id>`.
 */
export const siteConfigurePath = (id: number | string): string => `/configure/${id}`

/**
 * Path of one bundle's page.
 *
 * @param id - The bundle id.
 * @returns `/bundles/<id>`.
 */
export const siteBundlePath = (id: number | string): string => `/bundles/${id}`

/**
 * Path of the trial page of one product.
 *
 * @param slug - The product's slug.
 * @returns `/trial/<slug>`.
 */
export const siteTrialPath = (slug: string): string => `/trial/${slug}`

/**
 * Path of one announcement.
 *
 * @param id - The announcement id.
 * @returns `/announcements/<id>`.
 */
export const siteAnnouncementPath = (id: number | string): string => `${SITE_ANNOUNCEMENTS_PATH}/${id}`

/**
 * Path of one frequently asked questions category.
 *
 * @param slug - The category's slug.
 * @returns `/faq/<slug>`.
 */
export const siteFaqCategoryPath = (slug: string): string => `${SITE_FAQ_PATH}/${slug}`

/**
 * Path of one knowledge base article.
 *
 * @param id - The article id.
 * @returns `/knowledgebase/<id>`.
 */
export const siteKnowledgebaseArticlePath = (id: number | string): string => `${SITE_KNOWLEDGEBASE_PATH}/${id}`

/**
 * Path of one knowledge base category.
 *
 * @param slug - The category's slug.
 * @returns `/knowledgebase/category/<slug>`.
 */
export const siteKnowledgebaseCategoryPath = (slug: string): string => `${SITE_KNOWLEDGEBASE_PATH}/category/${slug}`
