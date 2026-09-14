/** The filters `GET /api/portal/public/products` accepts. */
export interface PortalProductQuery {
  /** Restrict to one product id. */
  pid?: number | string
  /** Restrict to one product group. */
  gid?: number
  /** Restrict to several product groups, comma-separated. */
  gids?: string
  /**
   * ISO 4217 code an anonymous caller wants prices in — the payer's currency, never the page's
   * language. Ignored by the backend for a signed-in client, whose own currency always wins.
   */
  currency?: string
}
