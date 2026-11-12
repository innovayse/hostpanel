/**
 * One product a bundle is made of, as `GET /api/portal/public/product-bundles` returns it.
 *
 * @module types/catalogbundlecomponent
 */

/** One product a bundle is made of. */
export interface CatalogBundleComponent {
  /** The product's primary key. */
  productId: number
  /** The product's display name. */
  name: string
  /** Whether this is the bundle's primary component. */
  isPrimary: boolean
}
