/** The filters `GET /api/portal/public/products` accepts. */
export interface PortalProductQuery {
  /** Locale to return names and descriptions in, e.g. "en". */
  lang?: string
  /** Restrict to one product id. */
  pid?: number | string
  /** Restrict to one product group. */
  gid?: number
  /** Restrict to several product groups, comma-separated. */
  gids?: string
}
