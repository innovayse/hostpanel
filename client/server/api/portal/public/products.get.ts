/**
 * GET /api/portal/public/products
 * Returns products from the C# backend.
 *
 * Query params: pid, gid, gids.
 *
 * There is deliberately no `lang`. This route used to forward one, and the public site read
 * `translated_name`, `translated_shortdescription`, `translated_description`,
 * `group_translations` and `group_features` off the result. `ProductDto` carries none of them
 * and `ProductsController` has no locale parameter, so the response was identical in all three
 * languages and every one of those reads fell through to its untranslated fallback. Localised
 * product copy is a backend change — a `ProductTranslation` entity beside the existing
 * `SlideTranslation` — and until it exists this parameter can only mislead its caller.
 */
import type { H3Event } from 'h3'

/**
 * The actual product fetch — pulled out so a signed-in caller can bypass the shared cache
 * below entirely (see the wrapper's note).
 *
 * @param event - The request event.
 * @returns The catalogue, filtered to `pid` when given, priced in the caller's own currency.
 */
async function loadProducts(event: H3Event) {
  const query = getQuery(event)
  const params = new URLSearchParams()

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

  // Anonymous callers may ask for prices in a specific enabled currency (the storefront's
  // currency picker) — forwarded straight through, same as `tld-pricing.get.ts` does.
  // `ProductsController`/`GetProductsHandler` ignore it for a signed-in caller.
  if (query.currency) params.set('currency', String(query.currency))

  const qs = params.toString()
  const all = await internalApiCall<Record<string, unknown>[]>(event, `/products${qs ? `?${qs}` : ''}`)

  // `pid` selects one product, and nothing on the backend honours it:
  // ProductsController takes `groupId` and `activeOnly`, there is no route for a
  // single product, and an unknown query parameter is ignored rather than
  // rejected. So `?pid=1` came back as the whole catalogue and /configure/[id]
  // rendered its first entry — every plan's "Choose plan" button led to the same
  // wrong product, at the wrong price. Filtering here keeps the fix in the layer
  // that invented the parameter.
  const pid = Number(query.pid)
  const products = query.pid && Number.isFinite(pid)
    ? all.filter(p => Number(p.id) === pid)
    : all

  // `pricing` and `prices` are forwarded exactly as `ProductDto` sends them — `pricing` is
  // already the amount in the caller's own currency (their client record's, the base currency
  // for a guest with no `currency` request, or the requested enabled currency otherwise), and
  // relabelling it here used to hardcode `USD`/`$` regardless of what currency it actually was.
  // See the note on `types/portalproduct.ts`.
  return products.map(p => ({
    ...p,
    pid: p.id
  }))
}

const cachedLoadProducts = defineCachedEventHandler(loadProducts, {
  name: 'backend-products',
  maxAge: 3600,
  swr: true,
  // The key carries no locale — a page's language was never the right thing to price by, and
  // this cached path is only ever reached anonymously (see the wrapper below). It DOES carry
  // `currency` now: an anonymous caller can request pricing in any enabled currency, and a
  // response priced in USD must never be served back to a request that asked for AMD (or vice
  // versa) — the entries below are one cache per (filters, currency) pair.
  getKey: (event) => {
    const query = getQuery(event)
    const filters = query.pid ? `p${query.pid}` : (query.gids || query.gid || 'all')
    const currency = query.currency ? String(query.currency).toUpperCase() : 'base'
    return `products:${filters}:${currency}`
  }
})

/**
 * A signed-in caller's products are priced in *their* currency, not the base currency the
 * cached path above answers with — sharing that cache across every signed-in client would
 * hand one client's prices to another the next time the key matched. So a request carrying an
 * auth cookie skips the cache and calls the backend directly; only a guest's request, always
 * priced in the base currency, is safe to share.
 */
export default defineEventHandler(async (event) => {
  if (getCookie(event, 'auth_token')) {
    return await loadProducts(event)
  }
  return await cachedLoadProducts(event)
})
