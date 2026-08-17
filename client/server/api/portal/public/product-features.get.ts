/**
 * GET /api/portal/public/product-features
 *
 * Returns the specification lines behind the storefront's plan comparison table.
 *
 * Query params: gid (product group), pid (single product).
 *
 * A backend without the endpoint — one running an older build than this client —
 * yields an empty list rather than an error, because the comparison table is an
 * addition to the plans page and must not be able to take the page down with it.
 */
export default defineCachedEventHandler(async (event) => {
  const query = getQuery(event)
  const params = new URLSearchParams()

  if (query.gid) params.set('groupId', String(query.gid))
  if (query.pid) params.set('productId', String(query.pid))

  const qs = params.toString()

  try {
    return await internalApiCall<Record<string, unknown>[]>(
      event, `/product-features${qs ? `?${qs}` : ''}`)
  } catch {
    return []
  }
}, {
  name: 'backend-product-features',
  // A minute, not the hour the products endpoint uses. This is edited far more
  // often than a product is: an operator adds a specification line, reloads the
  // plans page to check it, and sees nothing. Verified against a live backend —
  // the line was in the API and absent from this response. An hour of that reads
  // as a broken feature, and the table is cheap enough to re-fetch.
  maxAge: 60,
  swr: true,
  getKey: (event) => {
    const query = getQuery(event)
    return `product-features:${query.pid ? `p${query.pid}` : (query.gid || 'all')}`
  },
})
