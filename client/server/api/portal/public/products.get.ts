/**
 * GET /api/portal/public/products
 * Returns products from the C# backend.
 *
 * Query params: lang, pid, gid, gids
 */
export default defineCachedEventHandler(async (event) => {
  const query = getQuery(event)
  const params = new URLSearchParams()

  if (query.lang)  params.set('lang',  String(query.lang))
  if (query.pid)   params.set('pid',   String(query.pid))
  if (query.gid)   params.set('gid',   String(query.gid))
  if (query.gids)  params.set('gids',  String(query.gids))

  const qs = params.toString()
  return await internalApiCall<unknown[]>(event, `/products${qs ? `?${qs}` : ''}`)
}, {
  name: 'backend-products',
  maxAge: 3600,
  swr: true,
  getKey: (event) => {
    const query = getQuery(event)
    const locale = (query.lang as string) ?? getHeader(event, 'x-locale') ?? 'en'
    const filters = query.pid ? `p${query.pid}` : (query.gids || query.gid || 'all')
    return `products:${locale}:${filters}`
  }
})
