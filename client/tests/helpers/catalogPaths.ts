/**
 * The public catalogue and currency reads a home section starts, by name, so a test that answers
 * them from a stub BFF route table does not repeat the paths.
 *
 * @module tests/helpers/catalogPaths
 */

/** The public reads of the catalogue, the billing currencies and the TLD prices, and the availability check. */
export const CatalogPath = {
  Products: '/api/portal/public/products',
  ProductGroups: '/api/portal/public/product-groups',
  ProductFeatures: '/api/portal/public/product-features',
  Slides: '/api/portal/public/slides',
  BillingCurrencies: '/api/portal/public/billing-currencies',
  TldPricing: '/api/portal/public/tld-pricing',
  DomainCheck: '/api/portal/public/domain-check',
} as const
