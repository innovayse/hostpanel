/**
 * GET /api/portal/public/product-bundles
 *
 * The catalogue's active product bundles: what each is made of, whether it includes a domain and
 * how it is discounted. The names and descriptions are the operator's own text and come back as
 * written. The backend validates a bundle and applies its discount when the order is placed; the
 * storefront only shows an estimate.
 *
 * @module server/api/portal/public/product-bundles.get
 */

import type { CatalogBundle } from '~/types/catalogbundle'

/** Handler: the backend's active product bundles, passed through as sent. */
export default defineEventHandler(async (event): Promise<CatalogBundle[]> =>
  await internalApiCall<CatalogBundle[]>(event, '/public/product-bundles'))
