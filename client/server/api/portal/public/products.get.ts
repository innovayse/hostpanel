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
  if (query.gids)  params.set('gids',  String(query.gids))

  // `gid` is the WHMCS name callers use; ProductsController's parameter is
  // `groupId`. Forwarding `gid` verbatim meant the backend never saw a filter and
  // silently returned the whole catalogue, so the hosting page listed SSL
  // certificates, mailboxes and domain registration beside the hosting plans.
  // Both names go out: `groupId` is the one that binds, `gid` stays for any
  // consumer still reading it.
  if (query.gid) {
    params.set('gid', String(query.gid))
    params.set('groupId', String(query.gid))
  }

  const qs = params.toString()
  const products = await internalApiCall<Record<string, unknown>[]>(event, `/products${qs ? `?${qs}` : ''}`)

  // Map backend fields to frontend WHMCS-compatible format
  return products.map(p => {
    const pricing = p.pricing as { monthly?: number; annual?: number } | undefined
    return {
      ...p,
      pid: p.id,
      pricing: {
        USD: {
          prefix: '$',
          suffix: '',
          monthly: pricing?.monthly?.toFixed(2) ?? '-1.00',
          quarterly: '-1.00',
          semiannually: '-1.00',
          annually: pricing?.annual?.toFixed(2) ?? '-1.00',
          biennially: '-1.00',
          triennially: '-1.00',
        },
      },
    }
  })
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
