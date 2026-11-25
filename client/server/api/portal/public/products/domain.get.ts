/**
 * GET /api/portal/public/products/domain
 *
 * Returns the product a domain cart line is placed against: the backend finds it by its product type
 * (`ProductType.Domain`), not by anything in the product's name, and answers `DOMAIN_PRODUCT_NOT_CONFIGURED`
 * when the operator sells no domain product, which passes through with its own sentence.
 *
 * Not cached: a product the operator has just switched off must stop being offered at once.
 *
 * @module server/api/portal/public/products/domain.get
 */
import type { DomainProduct } from '~/types/domainproduct'

export default defineEventHandler(async (event) =>
  await internalApiCall<DomainProduct>(event, '/public/products/domain'))
