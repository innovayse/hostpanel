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
  maxAge: 3600,
  swr: true,
  getKey: (event) => {
    const query = getQuery(event)
    return `product-features:${query.pid ? `p${query.pid}` : (query.gid || 'all')}`
  },
})
