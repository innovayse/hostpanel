/**
 * The product a domain cart line is placed against, as `GET /api/portal/public/products/domain` returns it.
 *
 * @module types/domainproduct
 */

/** The product a domain order line hangs on. */
export interface DomainProduct {
  /** Product primary key; the cart line's product id. */
  id: number
}
